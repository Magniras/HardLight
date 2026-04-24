using System.Linq;
using Content.Server._HL.Shipyard;
using NUnit.Framework;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
#nullable enable

namespace Content.Tests.Server._HL.Shipyard;

/// <summary>
/// Regression tests for stale-EntityUid leakage out of ship saves.
///
/// Production logs from ship loads showed thousands of
/// <c>system.entity_lookup: Encountered deleted entity 0</c> errors per loaded ship,
/// caused by saved YAML carrying entity-ref fields whose targets did not exist in the
/// new world. Those refs deserialized to <c>EntityUid.Invalid</c> (uid 0), which then
/// got added to a <c>TransformComponent._children</c> set during MapInit, causing the
/// engine's <c>RecursiveAdd</c> to spam an error on every spatial query.
///
/// These tests pin the two parts of <see cref="ShipSaveYamlSanitizer"/> that prevent it:
///   1. Runtime-only components (<c>Actions</c>, <c>Projectile</c>, <c>ItemToggleActiveSound</c>,
///      <c>Blocking</c>, <c>Turnstile</c>, <c>NetworkConfigurator</c>, <c>SubdermalImplant</c>)
///      are stripped wholesale.
///   2. <c>ContainerContainer</c> and <c>Storage</c> entries that reference a uid not declared
///      anywhere in the save are pruned.
/// </summary>
[TestFixture, TestOf(typeof(ShipSaveYamlSanitizer))]
[Parallelizable(ParallelScope.All)]
public sealed class ShipSaveDanglingRefTest
{
    private static MappingDataNode BuildEntityWithComponent(string uid, string componentType, MappingDataNode? extraFields = null)
    {
        var comp = new MappingDataNode();
        comp["type"] = new ValueDataNode(componentType);
        if (extraFields != null)
        {
            foreach (var (key, value) in extraFields)
                comp[key] = value;
        }

        var comps = new SequenceDataNode();
        comps.Add(comp);

        var entity = new MappingDataNode();
        entity["uid"] = new ValueDataNode(uid);
        entity["components"] = comps;
        return entity;
    }

    private static MappingDataNode BuildSave(params MappingDataNode[] entities)
    {
        var entityList = new SequenceDataNode();
        foreach (var ent in entities)
            entityList.Add(ent);

        var protoGroup = new MappingDataNode();
        protoGroup["entities"] = entityList;

        var protoSeq = new SequenceDataNode();
        protoSeq.Add(protoGroup);

        var root = new MappingDataNode();
        root["entities"] = protoSeq;
        return root;
    }

    private static bool SaveContainsComponent(MappingDataNode root, string componentType)
    {
        if (!root.TryGet("entities", out SequenceDataNode? protoSeq) || protoSeq == null)
            return false;

        foreach (var protoNode in protoSeq)
        {
            if (protoNode is not MappingDataNode protoMap) continue;
            if (!protoMap.TryGet("entities", out SequenceDataNode? entitiesSeq) || entitiesSeq == null) continue;

            foreach (var entityNode in entitiesSeq)
            {
                if (entityNode is not MappingDataNode entMap) continue;
                if (!entMap.TryGet("components", out SequenceDataNode? comps) || comps == null) continue;

                foreach (var compNode in comps)
                {
                    if (compNode is not MappingDataNode compMap) continue;
                    if (!compMap.TryGet("type", out ValueDataNode? t) || t == null) continue;
                    if (t.Value == componentType) return true;
                }
            }
        }

        return false;
    }

    [Test]
    [TestCase("Actions",                TestName = "ActionsStripped")]
    [TestCase("Projectile",             TestName = "ProjectileStripped")]
    [TestCase("ItemToggleActiveSound",  TestName = "ItemToggleActiveSoundStripped")]
    [TestCase("Blocking",               TestName = "BlockingStripped")]
    [TestCase("Turnstile",              TestName = "TurnstileStripped")]
    [TestCase("SubdermalImplant",       TestName = "SubdermalImplantStripped")]
    public void RuntimeOnlyComponentRemovedFromShipSave(string componentType)
    {
        // Two entities: one carrying the runtime-only component (which should be stripped or
        // dropped entirely), and a sibling carrying a Sprite so the proto group always has
        // at least one survivor (mirrors how these components appear in real ship saves).
        var target = BuildEntityWithComponent("1", componentType);
        var sibling = BuildEntityWithComponent("2", "Sprite");
        var root = BuildSave(target, sibling);

        ShipSaveYamlSanitizer.SanitizeShipSaveNode(root, null!);

        Assert.That(SaveContainsComponent(root, componentType), Is.False,
            $"Component '{componentType}' must be stripped from ship saves to prevent stale EntityUid leakage.");
    }

    [Test]
    public void ContainerContainerRefsToUndeclaredEntitiesArePruned()
    {
        // Entity 1 holds a ContainerContainer with two contained ents:
        //   uid 2 — declared in this save  (should survive)
        //   uid 999 — NOT declared (dangling, should be pruned)
        var ents = new SequenceDataNode();
        ents.Add(new ValueDataNode("2"));
        ents.Add(new ValueDataNode("999"));

        var container = new MappingDataNode();
        container["ents"] = ents;

        var containers = new MappingDataNode();
        containers["test_slot"] = container;

        var ccFields = new MappingDataNode();
        ccFields["containers"] = containers;
        var holder = BuildEntityWithComponent("1", "ContainerContainer", ccFields);
        var contained = BuildEntityWithComponent("2", "Sprite");

        var root = BuildSave(holder, contained);

        ShipSaveYamlSanitizer.SanitizeShipSaveNode(root, null!);

        // Pull the surviving ents list back out and verify only "2" remains.
        Assert.That(holder.TryGet("components", out SequenceDataNode? compsAfter), Is.True);
        var ccComp = compsAfter!
            .OfType<MappingDataNode>()
            .First(c => c.TryGet("type", out ValueDataNode? t) && t!.Value == "ContainerContainer");
        Assert.That(ccComp.TryGet("containers", out MappingDataNode? containersAfter), Is.True);
        Assert.That(containersAfter![("test_slot")] is MappingDataNode, Is.True);
        var slotAfter = (MappingDataNode)containersAfter[("test_slot")];
        Assert.That(slotAfter.TryGet("ents", out SequenceDataNode? entsAfter), Is.True);
        var surviving = entsAfter!.Select(n => ((ValueDataNode)n).Value).ToList();

        Assert.That(surviving, Does.Contain("2"));
        Assert.That(surviving, Does.Not.Contain("999"),
            "ContainerContainer entry pointing at undeclared uid 999 must be pruned to avoid EntityUid.Invalid lookup spam.");
    }

    [Test]
    public void StorageRefsToUndeclaredEntitiesArePruned()
    {
        // Entity 1 has a Storage with two storedItems:
        //   uid "2" — declared (survives)
        //   uid "999" — undeclared (dangling, gets pruned)
        var storedItems = new MappingDataNode();
        storedItems["2"] = new MappingDataNode();
        storedItems["999"] = new MappingDataNode();

        var storageFields = new MappingDataNode();
        storageFields["storedItems"] = storedItems;
        var holder = BuildEntityWithComponent("1", "Storage", storageFields);
        var contained = BuildEntityWithComponent("2", "Sprite");

        var root = BuildSave(holder, contained);

        ShipSaveYamlSanitizer.SanitizeShipSaveNode(root, null!);

        var compsAfter = (SequenceDataNode)holder["components"];
        var storage = compsAfter
            .OfType<MappingDataNode>()
            .First(c => c.TryGet("type", out ValueDataNode? t) && t!.Value == "Storage");
        var storedAfter = (MappingDataNode)storage["storedItems"];

        Assert.That(storedAfter.Has("2"), Is.True);
        Assert.That(storedAfter.Has("999"), Is.False,
            "Storage entry pointing at undeclared uid 999 must be pruned to avoid EntityUid.Invalid lookup spam.");
    }
}
