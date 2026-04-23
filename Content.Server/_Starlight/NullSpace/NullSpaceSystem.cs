using Content.Shared.Eye;
using Content.Shared.Body.Components;
using Robust.Server.GameObjects;
using Content.Server.Atmos.Components;
using Content.Server.Temperature.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using System.Linq;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared._Starlight.NullSpace;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Carrying;
using Content.Server.Body.Systems;
using Content.Server.Hands.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Robust.Shared.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Content.Shared._Starlight;
using Robust.Server.Containers;
using Robust.Shared.Containers;

namespace Content.Server._Starlight.NullSpace;

public sealed class EtherealSystem : SharedEtherealSystem
{
    private sealed class HiddenEquipmentState
    {
        public Dictionary<string, EntityUid> HiddenSlots = new();
        public Dictionary<string, EntityUid> HiddenHands = new();
        public string? HiddenActiveHand;
    }

    private static readonly SoundPathSpecifier NullSpaceCutoffSound = new("/Audio/_HL/Effects/ma cutoff.ogg");

    [Dependency] private readonly VisibilitySystem _visibilitySystem = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly EyeSystem _eye = default!;
    [Dependency] private readonly NpcFactionSystem _factions = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly InternalsSystem _internals = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly CarryingSystem _carrying = default!;

    private const string HiddenEquipmentContainerId = "nullspace-hidden-equipment";
    private readonly Dictionary<EntityUid, HiddenEquipmentState> _hiddenEquipment = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NullSpaceComponent, AtmosExposedGetAirEvent>(OnExpose);
        SubscribeLocalEvent<BluespacePulseActionEvent>(OnBluespacePulse);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<NullSpaceComponent, KnockedDownComponent>();
        while (query.MoveNext(out var uid, out _, out _))
            RemCompDeferred<NullSpaceComponent>(uid);
    }

    public override void OnStartup(EntityUid uid, NullSpaceComponent component, MapInitEvent args)
    {
        base.OnStartup(uid, component, args);

        var visibility = EnsureComp<VisibilityComponent>(uid);
        _visibilitySystem.RemoveLayer((uid, visibility), (int)VisibilityFlags.Normal, false);
        _visibilitySystem.AddLayer((uid, visibility), (int)VisibilityFlags.NullSpace, false);
        _visibilitySystem.RefreshVisibility(uid, visibility);

        if (TryComp<EyeComponent>(uid, out var eye))
            _eye.SetVisibilityMask(uid, eye.VisibilityMask | (int)(VisibilityFlags.NullSpace), eye);

        if (TryComp<TemperatureComponent>(uid, out var temp))
            temp.AtmosTemperatureTransferEfficiency = 0;

        var stealth = EnsureComp<StealthComponent>(uid);
        _stealth.SetVisibility(uid, 0.8f, stealth);

        SuppressFactions(uid, component, true);

        EnsureComp<PressureImmunityComponent>(uid);
        EnsureComp<MovementIgnoreGravityComponent>(uid);

        if (TryComp<PullableComponent>(uid, out var pullable) && pullable.BeingPulled)
        {
            _pulling.TryStopPull(uid, pullable);
        }

        if (TryComp<PullerComponent>(uid, out var pullerComp)
            && TryComp<PullableComponent>(pullerComp.Pulling, out var subjectPulling))
        {
            _pulling.TryStopPull(pullerComp.Pulling.Value, subjectPulling);
        }

        DisconnectInternals(uid);
        HideEquipment(uid, component);

        if (TryComp<CarryingComponent>(uid, out var carrying)
            && !HasComp<PressureImmunityComponent>(carrying.Carried))
        {
            EnsureComp<PressureImmunityComponent>(carrying.Carried);
            EnsureComp<NullCarryPressureImmunityComponent>(carrying.Carried);
        }
    }

    public override void OnShutdown(EntityUid uid, NullSpaceComponent component, ComponentShutdown args)
    {
        base.OnShutdown(uid, component, args);

        if (TryComp<VisibilityComponent>(uid, out var visibility))
        {
            _visibilitySystem.AddLayer((uid, visibility), (int)VisibilityFlags.Normal, false);
            _visibilitySystem.RemoveLayer((uid, visibility), (int)VisibilityFlags.NullSpace, false);
            _visibilitySystem.RefreshVisibility(uid, visibility);
        }

        if (TryComp<EyeComponent>(uid, out var eye))
        {
            var mask = (int)VisibilityFlags.Normal;
            if (HasComp<ShowNullSpaceComponent>(uid))
                mask |= (int)VisibilityFlags.NullSpace;
            _eye.SetVisibilityMask(uid, mask, eye);
        }

        if (TryComp<TemperatureComponent>(uid, out var temp))
            temp.AtmosTemperatureTransferEfficiency = 0.1f;

        SuppressFactions(uid, component, false);

        RemComp<StealthComponent>(uid);
        RemComp<PressureImmunityComponent>(uid);
        RemComp<MovementIgnoreGravityComponent>(uid);

        if (TryComp<PullableComponent>(uid, out var pullable) && pullable.BeingPulled)
        {
            _pulling.TryStopPull(uid, pullable);
        }

        if (TryComp<PullerComponent>(uid, out var pullerComp)
            && TryComp<PullableComponent>(pullerComp.Pulling, out var subjectPulling))
        {
            _pulling.TryStopPull(pullerComp.Pulling.Value, subjectPulling);
        }

        RestoreEquipment(uid, component);

        if (TryComp<CarryingComponent>(uid, out var carrying)
            && HasComp<NullCarryPressureImmunityComponent>(carrying.Carried))
        {
            RemComp<NullCarryPressureImmunityComponent>(carrying.Carried);
            RemComp<PressureImmunityComponent>(carrying.Carried);
        }
    }

    public void SuppressFactions(EntityUid uid, NullSpaceComponent component, bool set)
    {
        if (set)
        {
            if (!TryComp<NpcFactionMemberComponent>(uid, out var factions))
                return;

            component.SuppressedFactions = factions.Factions.ToList();

            foreach (var faction in factions.Factions)
                _factions.RemoveFaction(uid, faction);
        }
        else
        {
            foreach (var faction in component.SuppressedFactions)
                _factions.AddFaction(uid, faction);

            component.SuppressedFactions.Clear();
        }
    }

    private void OnExpose(EntityUid uid, NullSpaceComponent component, ref AtmosExposedGetAirEvent args)
    {
        if (args.Handled)
            return;

        args.Gas = null;
        args.Handled = true;
    }

    private void OnBluespacePulse(BluespacePulseActionEvent args)
    {
        var radius = args.Radius;
        var stunTime = System.TimeSpan.FromSeconds(args.StunSeconds);

        var origin = args.Source;
        foreach (var ent in _lookup.GetEntitiesInRange(origin, radius))
        {
            if (!HasComp<NullSpaceComponent>(ent))
                continue;

            if (HasComp<ShadekinComponent>(ent))
                _audio.PlayPvs(NullSpaceCutoffSound, ent);
            RemComp<NullSpaceComponent>(ent);
            _stun.TryParalyze(ent, stunTime, true);
        }

        args.Handled = true;
    }

    private void DisconnectInternals(EntityUid uid)
    {
        if (!TryComp<InternalsComponent>(uid, out var internals))
            return;

        _internals.DisconnectTank((uid, internals), forced: true);
    }

    private void HideEquipment(EntityUid uid, NullSpaceComponent component)
    {
        if (_hiddenEquipment.ContainsKey(uid))
            return;

        var state = new HiddenEquipmentState();
        var hiddenContainer = _container.EnsureContainer<Container>(uid, HiddenEquipmentContainerId);

        if (TryComp<CarryingComponent>(uid, out var carrying))
            _carrying.DropCarried(uid, carrying.Carried);

        if (TryComp<HandsComponent>(uid, out var hands))
            state.HiddenActiveHand = hands.ActiveHand?.Name;

        if (TryComp<InventoryComponent>(uid, out var inventory))
        {
            foreach (var slot in inventory.Slots)
            {
                if (_inventory.TryGetSlotEntity(uid, slot.Name, out var equipped, inventory)
                    && HasComp<NullPhaseComponent>(equipped.Value))
                    continue;

                if (!_inventory.TryUnequip(uid, slot.Name, out var item, silent: true, force: true, inventory: inventory))
                    continue;

                if (_container.Insert(item.Value, hiddenContainer))
                    state.HiddenSlots[slot.Name] = item.Value;
            }
        }

        if (!TryComp<HandsComponent>(uid, out hands))
            return;

        foreach (var hand in _hands.EnumerateHands(uid, hands).ToArray())
        {
            if (hand.HeldEntity is not { } held)
                continue;

            if (_container.Insert(held, hiddenContainer))
                state.HiddenHands[hand.Name] = held;
        }

        _hiddenEquipment[uid] = state;
    }

    private void RestoreEquipment(EntityUid uid, NullSpaceComponent component)
    {
        if (!_hiddenEquipment.TryGetValue(uid, out var state))
            return;

        if (!_container.TryGetContainer(uid, HiddenEquipmentContainerId, out var hiddenContainer))
            return;

        if (TryComp<InventoryComponent>(uid, out var inventory))
        {
            foreach (var (slot, item) in state.HiddenSlots.ToArray())
            {
                if (TerminatingOrDeleted(item))
                {
                    state.HiddenSlots.Remove(slot);
                    continue;
                }

                if (_inventory.TryEquip(uid, item, slot, silent: true, force: true, inventory: inventory))
                {
                    state.HiddenSlots.Remove(slot);
                    continue;
                }

                DropHiddenItem(uid, item, hiddenContainer);
                state.HiddenSlots.Remove(slot);
            }
        }

        if (TryComp<HandsComponent>(uid, out var hands))
        {
            foreach (var (handName, item) in state.HiddenHands.ToArray())
            {
                if (TerminatingOrDeleted(item))
                {
                    state.HiddenHands.Remove(handName);
                    continue;
                }

                var restored = _hands.TryGetHand(uid, handName, out var hand, hands)
                    && _hands.TryPickup(uid, item, hand, checkActionBlocker: false, handsComp: hands);

                restored |= _hands.TryPickupAnyHand(uid, item, checkActionBlocker: false, handsComp: hands);

                if (!restored)
                    DropHiddenItem(uid, item, hiddenContainer);

                state.HiddenHands.Remove(handName);
            }

            if (state.HiddenActiveHand != null)
                _hands.TrySetActiveHand(uid, state.HiddenActiveHand, hands);
        }

        _hiddenEquipment.Remove(uid);
    }

    private void DropHiddenItem(EntityUid uid, EntityUid item, BaseContainer hiddenContainer)
    {
        if (!_container.Remove(item, hiddenContainer))
            return;

        _transform.AttachToGridOrMap(item);
        _transform.SetCoordinates(item, Transform(uid).Coordinates);
    }
}