using System.Linq;
using System.Numerics;
using Content.Server.Shuttles.Save;
using Content.Tests;
using Content.Shared.Actions;
using Content.Shared.CCVar;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Light.Components;
using Content.Shared.Shuttles.Save;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
#nullable enable
namespace Content.IntegrationTests.Tests.Shuttle;

/// <summary>
/// Regression test: ensure the refactored ShipSerializationSystem actually serializes entities
/// (previously only tiles were saved due to incorrect YAML parsing).
/// </summary>
public sealed class ShipSerializationTest : ContentUnitTest
{
    private static readonly string[] ActionComponentTypes =
    {
        nameof(InstantActionComponent),
        nameof(EntityTargetActionComponent),
        nameof(WorldTargetActionComponent),
        nameof(EntityWorldTargetActionComponent),
    };

    [Test]
    public async Task RefactoredSerializer_SerializesEntities()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var map = await pair.CreateTestMap();

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var shipSer = entManager.System<ShipSerializationSystem>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var mapSys = entManager.System<SharedMapSystem>();
        const string shipName = "TestShip";

        await server.WaitAssertion(() =>
        {
            // Ensure we use the refactored path.
            cfg.SetCVar(CCVars.ShipyardUseLegacySerializer, false);

            // Create a fresh grid separate from default test map grid (remove initial grid to minimize noise).
            entManager.DeleteEntity(map.Grid);
            var gridEnt = mapManager.CreateGridEntity(map.MapId);
            var gridUid = gridEnt.Owner;
            var gridComp = gridEnt.Comp;

            entManager.RunMapInit(gridUid, entManager.GetComponent<MetaDataComponent>(gridUid));

            // Lay down tiles so spawned entities can anchor if needed.
            mapSys.SetTile(gridUid, gridComp, Vector2i.Zero, new Tile(1));
            mapSys.SetTile(gridUid, gridComp, new Vector2i(1, 0), new Tile(1));

            // Spawn a couple of simple prototypes that should serialize (avoid ones filtered like vending machines).
            var coords = new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f));
            var ent1 = entManager.SpawnEntity("AirlockShuttle", coords);
            var ent2 = entManager.SpawnEntity("ChairBrass", new EntityCoordinates(gridUid, new Vector2(1.5f, 0.5f)));

            Assert.Multiple(() =>
            {
                // Sanity: they exist and are children of the grid.
                Assert.That(entManager.EntityExists(ent1));
                Assert.That(entManager.EntityExists(ent2));
                Assert.That(entManager.GetComponent<TransformComponent>(ent1).ParentUid, Is.EqualTo(gridUid));
                Assert.That(entManager.GetComponent<TransformComponent>(ent2).ParentUid, Is.EqualTo(gridUid));
            });

            var playerId = new NetUserId(Guid.NewGuid());
            var data = shipSer.SerializeShip(gridUid, playerId, shipName);

            Assert.That(data.Grids, Has.Count.EqualTo(1), "Expected exactly one grid serialized");
            var g = data.Grids[0];

            Assert.Multiple(() =>
            {
                // Tiles: we placed exactly two non-space tiles.
                Assert.That(g.Tiles, Has.Count.EqualTo(2), "Expected two non-space tiles");

                // Entities: expect at least the two we spawned, though additional infrastructure entities (grid, etc.) may appear.
                // We only store entities with valid prototypes; ensure count >=2 and contains our prototypes.
                Assert.That(g.Entities, Has.Count.GreaterThanOrEqualTo(2), $"Expected at least 2 entities, got {g.Entities.Count}");
            });

            var protos = g.Entities.Select(e => e.Prototype).ToHashSet();
            Assert.Multiple(() =>
            {
                Assert.That(protos, Does.Contain("AirlockShuttle"), "Serialized entities missing AirlockShuttle prototype");
                Assert.That(protos, Does.Contain("ChairBrass"), "Serialized entities missing ChairBrass prototype");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RefactoredSerializer_RebuildsRuntimeActionsOnLoad()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var sourceMap = await pair.CreateTestMap();
        var targetMap = await pair.CreateTestMap();

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var shipSer = entManager.System<ShipSerializationSystem>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var mapSys = entManager.System<SharedMapSystem>();
        var xformSys = entManager.System<SharedTransformSystem>();
        EntityUid sourceGridUid = default;
        EntityUid flashlight = default;
        ShipGridData data = null!;
        EntityUid restoredGrid = default;

        await server.WaitPost(() =>
        {
            cfg.SetCVar(CCVars.ShipyardUseLegacySerializer, false);

            entManager.DeleteEntity(sourceMap.Grid);
            var sourceGrid = mapManager.CreateGridEntity(sourceMap.MapId);
            sourceGridUid = sourceGrid.Owner;
            var sourceGridComp = sourceGrid.Comp;

            entManager.RunMapInit(sourceGridUid, entManager.GetComponent<MetaDataComponent>(sourceGridUid));
            mapSys.SetTile(sourceGridUid, sourceGridComp, Vector2i.Zero, new Tile(1));

            flashlight = entManager.SpawnEntity("FlashlightLantern", new EntityCoordinates(sourceGridUid, new Vector2(0.5f, 0.5f)));
            Assert.That(xformSys.AnchorEntity(flashlight, entManager.GetComponent<TransformComponent>(flashlight)), Is.True);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(entManager.TryGetComponent(flashlight, out HandheldLightComponent? lightComp));
            Assert.That(entManager.GetComponent<TransformComponent>(flashlight).ParentUid, Is.EqualTo(sourceGridUid));

            Assert.Multiple(() =>
            {
                Assert.That(lightComp!.ToggleActionEntity, Is.Not.Null);
                Assert.That(lightComp.SelfToggleActionEntity, Is.Not.Null);
            });

            data = shipSer.SerializeShip(sourceGridUid, new NetUserId(Guid.NewGuid()), "ActionShip");
            var gridData = data.Grids.Single();

            Assert.That(gridData.Entities.SelectMany(entity => entity.Components)
                    .Any(component => ActionComponentTypes.Contains(component.Type)),
                Is.False,
                "Ship serialization should not persist generated action entities.");

            Assert.That(gridData.Entities.SelectMany(entity => entity.Components)
                    .Any(component => (component.YamlData?.Contains("toggleActionEntity", StringComparison.OrdinalIgnoreCase) ?? false)
                        || (component.YamlData?.Contains("selfToggleActionEntity", StringComparison.OrdinalIgnoreCase) ?? false)),
                Is.False,
                "Ship serialization should scrub runtime action entity references from component YAML.");

            restoredGrid = shipSer.ReconstructShipOnMap(data, targetMap.MapId, Vector2.Zero);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var foundRestoredLight = false;
            EntityUid restoredLight = default;
            HandheldLightComponent? restoredLightComp = null;
            var lightQuery = entManager.EntityQueryEnumerator<HandheldLightComponent, TransformComponent>();
            while (lightQuery.MoveNext(out var uid, out var comp, out var xform))
            {
                if (xform.GridUid != restoredGrid)
                    continue;

                restoredLight = uid;
                restoredLightComp = comp;
                foundRestoredLight = true;
                break;
            }

            Assert.That(foundRestoredLight, Is.True, "Expected reconstructed ship to contain the flashlight.");
            Assert.That(restoredLightComp, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(restoredLightComp!.ToggleActionEntity, Is.Not.Null);
                Assert.That(restoredLightComp.SelfToggleActionEntity, Is.Not.Null);
                Assert.That(entManager.EntityExists(restoredLightComp.ToggleActionEntity!.Value), Is.True);
                Assert.That(entManager.EntityExists(restoredLightComp.SelfToggleActionEntity!.Value), Is.True);
            });

            var actionCount = 0;
            var actionQuery = entManager.EntityQueryEnumerator<InstantActionComponent, TransformComponent>();
            while (actionQuery.MoveNext(out _, out _, out var xform))
            {
                if (xform.ParentUid == restoredLight)
                    actionCount++;
            }

            Assert.That(actionCount, Is.EqualTo(2), "Reconstructed flashlight should recreate exactly its two runtime actions.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RefactoredSerializer_RestoresSolutionAppearance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var sourceMap = await pair.CreateTestMap();
        var targetMap = await pair.CreateTestMap();

        var entManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var shipSer = entManager.System<ShipSerializationSystem>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var mapSys = entManager.System<SharedMapSystem>();
        var appearanceSystem = entManager.System<SharedAppearanceSystem>();
        var solutionSystem = entManager.System<SharedSolutionContainerSystem>();
        var xformSys = entManager.System<SharedTransformSystem>();
        EntityUid sourceGridUid = default;
        EntityUid beaker = default;
        float originalFill = 0f;
        ShipGridData data = null!;
        EntityUid restoredGrid = default;

        await server.WaitPost(() =>
        {
            cfg.SetCVar(CCVars.ShipyardUseLegacySerializer, false);

            entManager.DeleteEntity(sourceMap.Grid);
            var sourceGrid = mapManager.CreateGridEntity(sourceMap.MapId);
            sourceGridUid = sourceGrid.Owner;
            var sourceGridComp = sourceGrid.Comp;

            entManager.RunMapInit(sourceGridUid, entManager.GetComponent<MetaDataComponent>(sourceGridUid));
            mapSys.SetTile(sourceGridUid, sourceGridComp, Vector2i.Zero, new Tile(1));

            beaker = entManager.SpawnEntity("Beaker", new EntityCoordinates(sourceGridUid, new Vector2(0.5f, 0.5f)));
            Assert.That(xformSys.AnchorEntity(beaker, entManager.GetComponent<TransformComponent>(beaker)), Is.True);
            Assert.That(entManager.TryGetComponent(beaker, out AppearanceComponent? beakerAppearance));
            Assert.That(solutionSystem.TryGetSolution(beaker, "beaker", out var solutionEnt, out var solution));

            solution!.AddSolution(new Solution("Water", FixedPoint2.New(10)), protoManager);
            solutionSystem.UpdateChemicals(solutionEnt!.Value, false);

            Assert.That(appearanceSystem.TryGetData(beaker, SolutionContainerVisuals.FillFraction, out originalFill, beakerAppearance), Is.True);
            Assert.That(originalFill, Is.GreaterThan(0f));
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(entManager.GetComponent<TransformComponent>(beaker).ParentUid, Is.EqualTo(sourceGridUid));

            data = shipSer.SerializeShip(sourceGridUid, new NetUserId(Guid.NewGuid()), "SolutionShip");
            restoredGrid = shipSer.ReconstructShipOnMap(data, targetMap.MapId, Vector2.Zero);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var foundRestoredBeaker = false;
            AppearanceComponent? restoredAppearance = null;
            EntityUid restoredBeaker = default;
            var beakerQuery = entManager.EntityQueryEnumerator<AppearanceComponent, TransformComponent, SolutionContainerManagerComponent>();
            while (beakerQuery.MoveNext(out var uid, out var appearance, out var xform, out var solutionManager))
            {
                if (xform.GridUid != restoredGrid)
                    continue;

                if (!solutionSystem.TryGetSolution((uid, solutionManager), "beaker", out _, out var restoredCandidate))
                    continue;

                restoredBeaker = uid;
                restoredAppearance = appearance;
                foundRestoredBeaker = true;
                break;
            }

            Assert.That(foundRestoredBeaker, Is.True, "Expected reconstructed ship to contain the beaker.");
            Assert.That(restoredAppearance, Is.Not.Null);
            Assert.That(appearanceSystem.TryGetData(restoredBeaker, SolutionContainerVisuals.FillFraction, out float restoredFill, restoredAppearance), Is.True);
            Assert.That(restoredFill, Is.EqualTo(originalFill).Within(0.001f), "Restored beaker should retain its fill-level appearance data.");

            Assert.That(solutionSystem.TryGetSolution(restoredBeaker, "beaker", out _, out var restoredSolution));
            Assert.That(restoredSolution!.Volume.Float(), Is.EqualTo(10f).Within(0.001f));
        });

        await pair.CleanReturnAsync();
    }
}
