using Content.Server.NPC.HTN;
using Robust.Shared.Timing;

namespace Content.Server._Mono.NPC.HTN;

/// <summary>
/// Drives <see cref="ShipAggroComponent"/>: refreshes aggro on incoming
/// ship-weapon hits (notified by <c>SpaceArtillerySystem</c>) and on
/// hostile <see cref="ShipNpcTargetComponent"/> entities entering
/// proximity. Mirrors aggro state into the HTN blackboard so HTN
/// compounds can branch into chase behavior.
/// </summary>
public sealed class ShipAggroSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    // Throttle proximity scans (cheap query but no need to do it every tick).
    private static readonly TimeSpan ProximityScanInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextProximityScan = TimeSpan.Zero;

    /// <summary>
    /// Aggro any AI cores on the grid that just took ship-weapon fire.
    /// Called from <c>SpaceArtillerySystem.OnProjectileHit</c>.
    /// </summary>
    public void NotifyGridHit(EntityUid grid)
    {
        AggroCoresOnGrid(grid);
    }

    private void AggroCoresOnGrid(EntityUid grid)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ShipAggroComponent, TransformComponent>();
        while (query.MoveNext(out _, out var aggro, out var coreXform))
        {
            if (coreXform.GridUid != grid)
                continue;

            aggro.AggroEndTime = now + aggro.AggroDuration;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        if (now >= _nextProximityScan)
        {
            _nextProximityScan = now + ProximityScanInterval;
            ScanProximity(now);
        }

        // Sync blackboard state on every aggro core (cheap; small population).
        var sync = EntityQueryEnumerator<ShipAggroComponent, HTNComponent>();
        while (sync.MoveNext(out _, out var aggro, out var htn))
        {
            var aggroed = now < aggro.AggroEndTime;
            var hasKey = htn.Blackboard.ContainsKey(aggro.BlackboardKey);
            if (aggroed && !hasKey)
                htn.Blackboard.SetValue(aggro.BlackboardKey, true);
            else if (!aggroed && hasKey)
                htn.Blackboard.Remove<bool>(aggro.BlackboardKey);
        }
    }

    private void ScanProximity(TimeSpan now)
    {
        var query = EntityQueryEnumerator<ShipAggroComponent, TransformComponent>();
        while (query.MoveNext(out var coreUid, out var aggro, out var coreXform))
        {
            var coreGrid = coreXform.GridUid;
            if (coreGrid == null)
                continue;

            var corePos = _xform.GetMapCoordinates(coreUid, coreXform);
            var aggroed = now < aggro.AggroEndTime;

            // Use the larger leash range for the lookup; a hostile inside
            // AggroProximityRange is the only thing that can *start* aggro,
            // but anything inside AggroLeashRange will *maintain* aggro
            // once started (so it only fades after the target is past the
            // leash AND AggroDuration has elapsed).
            var scanRange = MathF.Max(aggro.AggroProximityRange, aggro.AggroLeashRange);
            foreach (var found in _lookup.GetEntitiesInRange<ShipNpcTargetComponent>(corePos, scanRange))
            {
                var targetXform = Transform(found.Owner);
                var targetGrid = targetXform.GridUid;
                if (targetGrid == null || targetGrid == coreGrid)
                    continue;

                var targetPos = _xform.GetMapCoordinates(found.Owner, targetXform);
                if (targetPos.MapId != corePos.MapId)
                    continue;

                var distSq = (targetPos.Position - corePos.Position).LengthSquared();

                if (aggroed)
                {
                    if (distSq <= aggro.AggroLeashRange * aggro.AggroLeashRange)
                    {
                        aggro.AggroEndTime = now + aggro.AggroDuration;
                        break;
                    }
                }
                else
                {
                    if (distSq <= aggro.AggroProximityRange * aggro.AggroProximityRange)
                    {
                        aggro.AggroEndTime = now + aggro.AggroDuration;
                        break;
                    }
                }
            }
        }
    }
}
