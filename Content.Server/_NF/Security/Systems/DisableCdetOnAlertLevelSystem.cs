using Content.Server.AlertLevel;
using Content.Server._NF.Security.Components;
using Content.Server.Station.Systems;
using Content.Server.Turrets;
using Content.Shared.Popups;
using Content.Shared.Turrets;

namespace Content.Server._NF.Security.Systems;

public sealed class DisableCdetOnAlertLevelSystem : EntitySystem
{
    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly DeployableTurretSystem _turrets = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DisableCdetOnAlertLevelComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DisableCdetOnAlertLevelComponent, DeployableTurretStateAttemptEvent>(OnAttemptStateChange);
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
    }

    private void OnStartup(Entity<DisableCdetOnAlertLevelComponent> ent, ref ComponentStartup args)
    {
        RefreshTurretState(ent);
    }

    private void OnAttemptStateChange(Entity<DisableCdetOnAlertLevelComponent> ent, ref DeployableTurretStateAttemptEvent args)
    {
        if (!args.Enabled || !IsDisabledForCurrentAlert(ent))
            return;

        args.Cancelled = true;

        if (args.User != null)
            _popup.PopupClient(Loc.GetString("deployable-turret-component-disabled-by-alert"), ent, args.User.Value);
    }

    private void OnAlertLevelChanged(AlertLevelChangedEvent args)
    {
        var query = EntityQueryEnumerator<DisableCdetOnAlertLevelComponent, DeployableTurretComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var alertLock, out var turret, out var xform))
        {
            if (args.Station != EntityUid.Invalid && _station.GetOwningStation(uid, xform) != args.Station)
                continue;

            RefreshTurretState((uid, alertLock), (uid, turret), args.AlertLevel);
        }
    }

    private void RefreshTurretState(
        Entity<DisableCdetOnAlertLevelComponent> ent,
        Entity<DeployableTurretComponent?> turretEnt = default,
        string? currentAlert = null)
    {
        if (!Resolve(turretEnt.Owner, ref turretEnt.Comp, false))
            return;

        currentAlert ??= GetCurrentAlert(ent.Owner);
        var disabled = ent.Comp.DisabledAlertLevels.Contains(currentAlert);

        if (disabled)
        {
            if (turretEnt.Comp.Enabled)
                ent.Comp.RestoreAfterLockdown = true;

            _turrets.TrySetState((turretEnt.Owner, turretEnt.Comp), false);
            return;
        }

        if (!ent.Comp.RestoreAfterLockdown)
            return;

        ent.Comp.RestoreAfterLockdown = false;
        _turrets.TrySetState((turretEnt.Owner, turretEnt.Comp), true);
    }

    private bool IsDisabledForCurrentAlert(Entity<DisableCdetOnAlertLevelComponent> ent)
    {
        return ent.Comp.DisabledAlertLevels.Contains(GetCurrentAlert(ent.Owner));
    }

    private string GetCurrentAlert(EntityUid uid)
    {
        var station = _station.GetOwningStation(uid) ?? EntityUid.Invalid;
        return _alertLevel.GetLevel(station);
    }
}