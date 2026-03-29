using System;
using Content.Server._NF.Roles.Systems;
using Content.Server.GameTicking;
using Content.Server.Station.Systems; // HardLight
using Content.Server.Station.Components;
using Content.Server.StationEvents.Components;
using Content.Shared._NF.Roles.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Roles; // HardLight
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes; // HardLight

namespace Content.Server.StationEvents.Events;

[UsedImplicitly]
public sealed class DynamicJobAllocationRule : StationEventSystem<DynamicJobAllocationRuleComponent>
{
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!; // HardLight
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    // HardLight: Legacy job id used before Mercenary/Freelancer naming cleanup.
    private const string LegacyFreelancerJobId = "Freelancer";

    private bool _recalculationQueued;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        SubscribeLocalEvent<JobTrackingStateChangedEvent>(OnJobTrackingStateChanged);
        SubscribeLocalEvent<MindAddedMessage>(OnMindAddedGlobal, after: new[] { typeof(JobTrackingSystem) });
        SubscribeLocalEvent<MindRemovedMessage>(OnMindRemovedGlobal, after: new[] { typeof(JobTrackingSystem) });
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    protected override void Started(EntityUid uid, DynamicJobAllocationRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        QueueRecalculation();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_recalculationQueued)
            return;

        _recalculationQueued = false;
        UpdateActiveRules();
    }

    protected override void ActiveTick(EntityUid uid, DynamicJobAllocationRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        component.TimeSinceLastCheck += frameTime;

        if (component.TimeSinceLastCheck >= component.CheckInterval)
        {
            component.TimeSinceLastCheck = 0f;
            AdjustJobSlots(uid, component);
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        QueueRecalculation();
    }

    private void OnJobTrackingStateChanged(JobTrackingStateChangedEvent ev)
    {
        QueueRecalculation();
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        QueueRecalculation();
    }

    private void OnMindAddedGlobal(MindAddedMessage ev)
    {
        QueueRecalculation();
    }

    private void OnMindRemovedGlobal(MindRemovedMessage ev)
    {
        QueueRecalculation();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        QueueRecalculation();
    }

    private void QueueRecalculation()
    {
        _recalculationQueued = true;
    }

    private void UpdateActiveRules()
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var component, out _))
        {
            AdjustJobSlots(uid, component);
        }
    }

    private void AdjustJobSlots(EntityUid uid, DynamicJobAllocationRuleComponent component)
    {
        var stations = new Dictionary<EntityUid, (int staffedNonMercenary, int filledMercenary)>();
        var staffedEntities = new HashSet<EntityUid>();

        var stationQuery = EntityQueryEnumerator<StationJobsComponent>();
        while (stationQuery.MoveNext(out var stationUid, out _))
        {
            stations[stationUid] = (0, 0);
        }

        if (stations.Count == 0)
            return;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } attached)
                continue;

            if (session.Status != SessionStatus.InGame)
                continue;

            staffedEntities.Add(attached);
        }

        var jobsQuery = EntityQueryEnumerator<JobTrackingComponent, TransformComponent>(); // HardLight: Added TransformComponent
        while (jobsQuery.MoveNext(out var jobTrackedEntity, out var jobTracking, out var xform)) // HardLight: Added out var xform
        {
            if (!staffedEntities.Contains(jobTrackedEntity)
                || !jobTracking.Active // HardLight
                || jobTracking.Job is not { } job) // HardLight
                continue;

            var stationUid = ResolveTrackedEntityStation(jobTrackedEntity, jobTracking, xform, stations); // HardLight
            if (stationUid is not { } resolvedStation // HardLight
                || !stations.TryGetValue(resolvedStation, out var counts))
                continue;

            if (IsMercenaryJob(job, component)) // HardLight
                counts.filledMercenary++;
            else
                counts.staffedNonMercenary++;

            stations[resolvedStation] = counts; // HardLight: jobTracking.SpawnStation<resolvedStation
        }

        foreach (var (stationUid, counts) in stations)
        {
            var desiredTotalSlots = Math.Min(counts.staffedNonMercenary, component.MercenaryCap);
            var availableSlots = Math.Max(0, desiredTotalSlots - counts.filledMercenary);

            _stationJobs.TrySetJobMidRoundMax(stationUid, component.MercenaryJob, desiredTotalSlots);

            _stationJobs.TrySetJobSlot(stationUid, component.MercenaryJob, availableSlots);
        }
    }

    // HardLight: Handle stale SpawnStation references after round transitions by resolving current station ownership.
    private EntityUid? ResolveTrackedEntityStation(
        EntityUid trackedEntity,
        JobTrackingComponent jobTracking,
        TransformComponent xform,
        Dictionary<EntityUid, (int staffedNonMercenary, int filledMercenary)> stations)
    {
        // Fast path: if spawn station is still valid for this round, keep using it.
        if (stations.ContainsKey(jobTracking.SpawnStation))
            return jobTracking.SpawnStation;

        // Round transitions can invalidate stored spawn station UIDs; resolve the current owner.
        var owningStation = _stationSystem.GetOwningStation(trackedEntity, xform);
        if (owningStation is not { } stationUid || !stations.ContainsKey(stationUid))
            return null;

        // Use resolved station for this calculation pass without mutating shared tracking state.
        return stationUid;
    }

    // HardLight: Count both current and legacy freelancer ids as mercenary-equivalent for slot accounting.
    private static bool IsMercenaryJob(ProtoId<JobPrototype> jobId, DynamicJobAllocationRuleComponent component)
    {
        return string.Equals(jobId, component.MercenaryJob, StringComparison.Ordinal)
               || string.Equals(jobId, LegacyFreelancerJobId, StringComparison.Ordinal);
    }
}
