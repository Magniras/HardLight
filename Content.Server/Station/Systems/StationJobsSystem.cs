using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._HL.ColComm; // HardLight
using Content.Server._NF.Station.Components;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Server.StationRecords.Components;
using Content.Shared._NF.Shipyard;
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared.CCVar;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Station.Systems;

/// <summary>
/// Manages job slots for stations.
/// </summary>
[PublicAPI]
public sealed partial class StationJobsSystem : EntitySystem
{
    public const string ShipFreelancerInterviewJobId = "MercenaryInterview";
    private const string ShipContractorInterviewJobId = "ContractorInterview";
    private const string ShipPilotInterviewJobId = "PilotInterview";

    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly ColcommJobSystem _colcommJobs = default!; // HardLight
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly StationSystem _station = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialized);
        SubscribeLocalEvent<StationJobsComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<StationJobsComponent, StationRenamedEvent>(OnStationRenamed);
        SubscribeLocalEvent<StationJobsComponent, ComponentShutdown>(OnStationDeletion);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        Subs.CVar(_configurationManager, CCVars.GameDisallowLateJoins, _ => UpdateJobsAvailable(), true);
    }

    private void OnInit(Entity<StationJobsComponent> ent, ref ComponentInit args)
    {
        NormalizeShipLateJoinJobs(ent.Owner, ent.Comp);
        RefreshSetupJobMetadata(ent.Comp);
    }

    public override void Update(float _)
    {
        if (_availableJobsDirty)
        {
            _cachedAvailableJobs = GenerateJobsAvailableEvent();
            RaiseNetworkEvent(_cachedAvailableJobs, Filter.Empty().AddPlayers(_player.Sessions));
            _availableJobsDirty = false;
        }
    }

    private void OnStationDeletion(EntityUid uid, StationJobsComponent component, ComponentShutdown args)
    {
        UpdateJobsAvailable(); // we no longer exist so the jobs list is changed.
    }

    private void OnStationInitialized(StationInitializedEvent msg)
    {
        if (!TryComp<StationJobsComponent>(msg.Station, out var stationJobs))
            return;

        NormalizeShipLateJoinJobs(msg.Station, stationJobs);
        RefreshSetupJobMetadata(stationJobs);

        stationJobs.JobList = stationJobs.SetupAvailableJobs.ToDictionary(
            x => x.Key,
            x=> (int?)(x.Value[1] < 0 ? null : x.Value[1]));

        stationJobs.TotalJobs = stationJobs.JobList.Values.Select(x => x ?? 0).Sum();

        ApplyActiveRoleCountsToJobList(stationJobs);

        UpdateJobsAvailable();
    }

    private void RefreshSetupJobMetadata(StationJobsComponent stationJobs)
    {
        stationJobs.MidRoundTotalJobs = stationJobs.SetupAvailableJobs.Values
            .Select(x => Math.Max(x[1], 0))
            .Sum();

        stationJobs.OverflowJobs = stationJobs.SetupAvailableJobs
            .Where(x => x.Value[0] < 0)
            .Select(x => x.Key)
            .ToHashSet();
    }

    private void NormalizeShipLateJoinJobs(EntityUid station, StationJobsComponent stationJobs)
    {
        if (!IsShipCrewHiringStation(station))
            return;

        NormalizeShipLateJoinJob(stationJobs, "Mercenary", ShipFreelancerInterviewJobId);
        NormalizeShipLateJoinJob(stationJobs, "Pilot", ShipPilotInterviewJobId);
        NormalizeShipLateJoinJob(stationJobs, "Contractor", ShipContractorInterviewJobId);
    }

    private static void NormalizeShipLateJoinJob(StationJobsComponent stationJobs, string legacyJobId, string interviewJobId)
    {
        if (!stationJobs.SetupAvailableJobs.Remove(legacyJobId, out var legacySlots))
            return;

        if (stationJobs.SetupAvailableJobs.TryGetValue(interviewJobId, out var interviewSlots))
        {
            stationJobs.SetupAvailableJobs[interviewJobId] =
            [
                Math.Max(interviewSlots[0], legacySlots[0]),
                Math.Max(interviewSlots[1], legacySlots[1]),
            ];
            return;
        }

        stationJobs.SetupAvailableJobs[interviewJobId] =
        [
            legacySlots[0],
            legacySlots[1],
        ];
    }

    private static bool TryGetShipInterviewJobId(string jobPrototypeId, [NotNullWhen(true)] out string? interviewJobId)
    {
        interviewJobId = jobPrototypeId switch
        {
            "Mercenary" => ShipFreelancerInterviewJobId,
            "Pilot" => ShipPilotInterviewJobId,
            "Contractor" => ShipContractorInterviewJobId,
            _ => null,
        };

        return interviewJobId != null;
    }

    #region Public API

    /// <inheritdoc cref="TryAssignJob(Robust.Shared.GameObjects.EntityUid,string,NetUserId,Content.Server.Station.Components.StationJobsComponent?)"/>
    /// <param name="station">Station to assign a job on.</param>
    /// <param name="job">Job to assign.</param>
    /// <param name="netUserId">The net user ID of the player we're assigning this job to.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    public bool TryAssignJob(EntityUid station, JobPrototype job, NetUserId netUserId, StationJobsComponent? stationJobs = null)
    {
        return TryAssignJob(station, job.ID, netUserId, stationJobs);
    }

    /// <summary>
    /// Attempts to assign the given job once. (essentially, it decrements the slot if possible).
    /// </summary>
    /// <param name="station">Station to assign a job on.</param>
    /// <param name="jobPrototypeId">Job prototype ID to assign.</param>
    /// <param name="netUserId">The net user ID of the player we're assigning this job to.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>Whether or not assignment was a success.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public bool TryAssignJob(EntityUid station, string jobPrototypeId, NetUserId netUserId, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs, false))
            return false;

        if (!IsAdvertisedLateJoinJob(station, jobPrototypeId))
            return false;

        if (!stationJobs.JobList.TryGetValue(jobPrototypeId, out var localSlots))
            return false;

        var globalJobPrototypeId = GetColcommJobId(jobPrototypeId);
        Entity<ColcommJobRegistryComponent> colcomm = default;
        int? globalSlots = null;
        var hasGlobalSlots = false;

        if (_colcommJobs.TryGetColcommRegistry(out colcomm))
            hasGlobalSlots = _colcommJobs.TryGetJobSlot(colcomm, globalJobPrototypeId, out globalSlots);

        if (IsPlayerJobTracked(station, netUserId, jobPrototypeId, stationJobs))
        {
            if (hasGlobalSlots)
                _colcommJobs.TryTrackPlayerJob(colcomm, netUserId, globalJobPrototypeId);

            return true;
        }

        if (hasGlobalSlots && _colcommJobs.IsPlayerJobTracked(colcomm, netUserId, globalJobPrototypeId))
        {
            TryTrackPlayerJob(station, netUserId, jobPrototypeId, stationJobs);
            return true;
        }

        if (localSlots == 0)
            return false;

        if (hasGlobalSlots && globalSlots == 0)
            return false;

        if (hasGlobalSlots)
        {
            if (!_colcommJobs.TryAdjustJobSlot(colcomm, globalJobPrototypeId, -1, clamp: true))
                return false;

            _colcommJobs.TryTrackPlayerJob(colcomm, netUserId, globalJobPrototypeId);
        }

        if (!TryAdjustJobSlot(station, jobPrototypeId, -1, false, true, stationJobs))
        {
            if (hasGlobalSlots)
            {
                _colcommJobs.TryAdjustJobSlot(colcomm, globalJobPrototypeId, 1, clamp: true);
                _colcommJobs.TryUntrackPlayerJob(colcomm, netUserId, globalJobPrototypeId);
            }

            return false;
        }

        TryTrackPlayerJob(station, netUserId, jobPrototypeId, stationJobs);
        return true;

    }

    /// <inheritdoc cref="TryAdjustJobSlot(Robust.Shared.GameObjects.EntityUid,string,int,bool,bool,Content.Server.Station.Components.StationJobsComponent?)"/>
    /// <param name="station">Station to adjust the job slot on.</param>
    /// <param name="job">Job to adjust.</param>
    /// <param name="amount">Amount to adjust by.</param>
    /// <param name="createSlot">Whether or not it should create the slot if it doesn't exist.</param>
    /// <param name="clamp">Whether or not to clamp to zero if you'd remove more jobs than are available.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    public bool TryAdjustJobSlot(EntityUid station, JobPrototype job, int amount, bool createSlot = false, bool clamp = false,
        StationJobsComponent? stationJobs = null)
    {
        return TryAdjustJobSlot(station, job.ID, amount, createSlot, clamp, stationJobs);
    }

    /// <summary>
    /// Attempts to adjust the given job slot by the amount provided.
    /// </summary>
    /// <param name="station">Station to adjust the job slot on.</param>
    /// <param name="jobPrototypeId">Job prototype ID to adjust.</param>
    /// <param name="amount">Amount to adjust by.</param>
    /// <param name="createSlot">Whether or not it should create the slot if it doesn't exist.</param>
    /// <param name="clamp">Whether or not to clamp to zero if you'd remove more jobs than are available.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>Whether or not slot adjustment was a success.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public bool TryAdjustJobSlot(EntityUid station,
        string jobPrototypeId,
        int amount,
        bool createSlot = false,
        bool clamp = false,
        StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        var jobList = stationJobs.JobList;

        // This should:
        // - Return true when zero slots are added/removed.
        // - Return true when you add.
        // - Return true when you remove and do not exceed the number of slot available.
        // - Return false when you remove from a job that doesn't exist.
        // - Return false when you remove and exceed the number of slots available.
        // And additionally, if adding would add a job not previously on the manifest when createSlot is false, return false and do nothing.

        if (amount == 0)
            return true;

        switch (jobList.TryGetValue(jobPrototypeId, out var available))
        {
            case false when amount < 0:
                return false;
            case false:
                if (!createSlot)
                    return false;
                stationJobs.TotalJobs += amount;
                jobList[jobPrototypeId] = amount;
                UpdateJobsAvailable();
                return true;
            case true:
                // Job is unlimited so just say we adjusted it and do nothing.
                if (available is not {} avail)
                    return true;

                // Would remove more jobs than we have available.
                if (available + amount < 0 && !clamp)
                    return false;

                jobList[jobPrototypeId] = Math.Max(avail + amount, 0);
                stationJobs.TotalJobs = jobList.Values.Select(x => x ?? 0).Sum();
                UpdateJobsAvailable();
                return true;
        }
    }

    public bool TryGetPlayerJobs(EntityUid station,
        NetUserId userId,
        [NotNullWhen(true)] out List<ProtoId<JobPrototype>>? jobs,
        StationJobsComponent? jobsComponent = null)
    {
        jobs = null;
        if (!Resolve(station, ref jobsComponent, false))
            return false;

        return jobsComponent.PlayerJobs.TryGetValue(userId, out jobs);
    }

    public bool TryRemovePlayerJobs(EntityUid station,
        NetUserId userId,
        StationJobsComponent? jobsComponent = null)
    {
        if (!Resolve(station, ref jobsComponent, false))
            return false;

        return jobsComponent.PlayerJobs.Remove(userId);
    }

    /// <inheritdoc cref="TrySetJobSlot(Robust.Shared.GameObjects.EntityUid,string,int,bool,Content.Server.Station.Components.StationJobsComponent?)"/>
    /// <param name="station">Station to adjust the job slot on.</param>
    /// <param name="jobPrototype">Job prototype to adjust.</param>
    /// <param name="amount">Amount to set to.</param>
    /// <param name="createSlot">Whether or not it should create the slot if it doesn't exist.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns></returns>
    public bool TrySetJobSlot(EntityUid station, JobPrototype jobPrototype, int amount, bool createSlot = false,
        StationJobsComponent? stationJobs = null)
    {
        return TrySetJobSlot(station, jobPrototype.ID, amount, createSlot, stationJobs);
    }

    /// <summary>
    /// Attempts to set the given job slot to the amount provided.
    /// </summary>
    /// <param name="station">Station to adjust the job slot on.</param>
    /// <param name="jobPrototypeId">Job prototype ID to adjust.</param>
    /// <param name="amount">Amount to set to.</param>
    /// <param name="createSlot">Whether or not it should create the slot if it doesn't exist.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>Whether or not setting the value succeeded.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public bool TrySetJobSlot(EntityUid station,
        string jobPrototypeId,
        int amount,
        bool createSlot = false,
        StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));
        if (amount < 0)
            throw new ArgumentException("Tried to set a job to have a negative number of slots!", nameof(amount));

        var jobList = stationJobs.JobList;

        switch (jobList.ContainsKey(jobPrototypeId))
        {
            case false:
                if (!createSlot)
                    return false;
                stationJobs.TotalJobs += amount;
                jobList[jobPrototypeId] = amount;
                UpdateJobsAvailable();
                return true;
            case true:
                stationJobs.TotalJobs += amount - (jobList[jobPrototypeId] ?? 0);

                jobList[jobPrototypeId] = amount;
                UpdateJobsAvailable();
                return true;
        }
    }

    /// <summary>
    /// HardLight: Returns true when the given job is present in the station's configured job list
    /// (<see cref="StationJobsComponent.SetupAvailableJobs"/>).  Use this from systems that only
    /// have Read access to <see cref="StationJobsComponent"/> to avoid RA0002 violations.
    /// </summary>
    public bool IsConfiguredJob(EntityUid station,
        ProtoId<JobPrototype> jobPrototypeId,
        StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs, false))
            return false;

        return stationJobs.SetupAvailableJobs.ContainsKey(jobPrototypeId);
    }

    /// <summary>
    /// HardLight: Returns true if this station already tracks the given player's assignment for the job.
    /// </summary>
    public bool IsPlayerJobTracked(EntityUid station,
        NetUserId userId,
        ProtoId<JobPrototype> jobPrototypeId,
        StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs, false))
            return false;

        return stationJobs.PlayerJobs.TryGetValue(userId, out var jobs) && jobs.Contains(jobPrototypeId);
    }

    public bool IsAdvertisedLateJoinJob(EntityUid station, ProtoId<JobPrototype> jobPrototypeId)
    {
        return IsAdvertisedLateJoinJob(station, jobPrototypeId.ToString());
    }

    public bool IsAdvertisedLateJoinJob(EntityUid station, string jobPrototypeId)
    {
        if (!HasComp<ExtraShuttleInformationComponent>(station))
            return true;

        if (!IsShipCrewHiringStation(station))
            return false;

        return jobPrototypeId == ShipFreelancerInterviewJobId;
    }

    /// <summary>
    /// Returns true when the station represents a player-manageable ship that should expose freelancer crew hiring.
    /// Cargo-class and custom/non-player vessels are excluded.
    /// </summary>
    public bool IsShipCrewHiringStation(EntityUid station, ExtraShuttleInformationComponent? shuttleInfo = null)
    {
        if (!Resolve(station, ref shuttleInfo, false)
            || shuttleInfo.Vessel is not { } vesselId
            || !_prototype.TryIndex(vesselId, out VesselPrototype? vessel))
        {
            return false;
        }

        return vessel.Group != ShipyardConsoleUiKey.Custom
            && !vessel.Classes.Contains(VesselClass.Cargo);
    }

    public ProtoId<JobPrototype> GetColcommJobId(ProtoId<JobPrototype> jobPrototypeId)
    {
        return GetColcommJobId(jobPrototypeId.ToString());
    }

    public ProtoId<JobPrototype> GetColcommJobId(string jobPrototypeId)
    {
        return jobPrototypeId switch
        {
            ShipFreelancerInterviewJobId => "Mercenary",
            ShipPilotInterviewJobId => "Pilot",
            ShipContractorInterviewJobId => "Contractor",
            _ => jobPrototypeId,
        };
    }

    public ProtoId<JobPrototype> GetStationTrackingJobId(EntityUid station, ProtoId<JobPrototype> jobPrototypeId, StationJobsComponent? stationJobs = null)
    {
        return GetStationTrackingJobId(station, jobPrototypeId.ToString(), stationJobs);
    }

    public ProtoId<JobPrototype> GetStationTrackingJobId(EntityUid station, string jobPrototypeId, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs, false)
            || !IsShipCrewHiringStation(station)
            || !TryGetShipInterviewJobId(jobPrototypeId, out var interviewJobId))
        {
            return jobPrototypeId;
        }

        return stationJobs.JobList.ContainsKey(interviewJobId)
            || stationJobs.SetupAvailableJobs.ContainsKey(interviewJobId)
            ? interviewJobId
            : jobPrototypeId;
    }

    /// <summary>
    /// HardLight: Ensures this station tracks the given player's assignment for the job.
    /// </summary>
    public bool TryTrackPlayerJob(EntityUid station,
        NetUserId userId,
        ProtoId<JobPrototype> jobPrototypeId,
        StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs, false))
            return false;

        stationJobs.PlayerJobs.TryAdd(userId, new());
        if (!stationJobs.PlayerJobs[userId].Contains(jobPrototypeId))
            stationJobs.PlayerJobs[userId].Add(jobPrototypeId);

        return true;
    }

    /// <summary>
    /// HardLight: Removes this player's assignment tracking for the given job on the station.
    /// </summary>
    public bool TryUntrackPlayerJob(EntityUid station,
        NetUserId userId,
        ProtoId<JobPrototype> jobPrototypeId,
        StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs, false))
            return false;

        if (!stationJobs.PlayerJobs.TryGetValue(userId, out var jobs))
            return false;

        jobs.Remove(jobPrototypeId);
        if (jobs.Count == 0)
            stationJobs.PlayerJobs.Remove(userId);

        return true;
    }

    /// <summary>
    /// HardLight: Attempts to set the configured mid-round maximum slots for a job in <see cref="StationJobsComponent.SetupAvailableJobs"/>.
    /// This controls logic that references setup max values (e.g. job reopening checks).
    /// </summary>
    /// <param name="station">Station to update.</param>
    /// <param name="jobPrototypeId">Job prototype ID to update.</param>
    /// <param name="amount">New configured mid-round maximum slots.</param>
    /// <param name="createSlot">Whether to create setup entry when missing.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>Whether the update succeeded.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public bool TrySetJobMidRoundMax(EntityUid station,
        string jobPrototypeId,
        int amount,
        bool createSlot = false,
        StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        if (amount < 0)
            throw new ArgumentException("Tried to set a job to have a negative configured max slots!", nameof(amount));

        if (stationJobs.SetupAvailableJobs.TryGetValue(jobPrototypeId, out var setupSlots))
        {
            if (setupSlots.Length < 2)
                return false;

            setupSlots[1] = amount;
            RefreshSetupJobMetadata(stationJobs);
            return true;
        }

        if (!createSlot)
            return false;

        stationJobs.SetupAvailableJobs[jobPrototypeId] = new[] { amount, amount };
        RefreshSetupJobMetadata(stationJobs);
        return true;
    }

    public bool TryGetJobMidRoundMax(EntityUid station,
        string jobPrototypeId,
        out int? amount,
        StationJobsComponent? stationJobs = null)
    {
        amount = null;

        if (!Resolve(station, ref stationJobs, false))
            return false;

        if (!stationJobs.SetupAvailableJobs.TryGetValue(jobPrototypeId, out var setupSlots)
            || setupSlots.Length < 2)
        {
            return false;
        }

        amount = setupSlots[1] < 0 ? null : setupSlots[1];
        return true;
    }

    public int GetTrackedJobOccupancy(EntityUid station,
        ProtoId<JobPrototype> jobPrototypeId,
        StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs, false))
            return 0;

        var total = 0;
        foreach (var jobs in stationJobs.PlayerJobs.Values)
        {
            if (jobs.Contains(jobPrototypeId))
                total++;
        }

        return total;
    }

    public bool TryAdjustJobCapacity(EntityUid station,
        string jobPrototypeId,
        int amount,
        bool createSlot = false,
        bool clamp = false,
        StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        if (amount == 0)
            return true;

        if (!TryGetJobMidRoundMax(station, jobPrototypeId, out var configuredMax, stationJobs))
        {
            if (!createSlot)
                return false;

            configuredMax = 0;
        }

        if (configuredMax == null)
            return true;

        if (configuredMax + amount < 0 && !clamp)
            return false;

        var targetMax = Math.Max(configuredMax.Value + amount, 0);
        var occupied = GetTrackedJobOccupancy(station, jobPrototypeId, stationJobs);

        if (!TrySetJobMidRoundMax(station, jobPrototypeId, targetMax, createSlot, stationJobs))
            return false;

        return TrySetJobSlot(station, jobPrototypeId, Math.Max(targetMax - occupied, 0), createSlot, stationJobs);
    }

    public bool TryReopenTrackedJobSlot(EntityUid station,
        string jobPrototypeId,
        StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs, false))
            return false;

        if (!stationJobs.JobList.TryGetValue(jobPrototypeId, out var currentSlots))
            return false;

        if (currentSlots == null)
            return true;

        if (TryGetJobMidRoundMax(station, jobPrototypeId, out var configuredMax, stationJobs)
            && configuredMax != null)
        {
            var occupied = GetTrackedJobOccupancy(station, jobPrototypeId, stationJobs);
            if (currentSlots.Value + occupied >= configuredMax.Value)
                return false;
        }

        return TryAdjustJobSlot(station, jobPrototypeId, 1, stationJobs: stationJobs);
    }

    /// <inheritdoc cref="MakeJobUnlimited(Robust.Shared.GameObjects.EntityUid,string,Content.Server.Station.Components.StationJobsComponent?)"/>
    /// <param name="station">Station to make a job unlimited on.</param>
    /// <param name="job">Job to make unlimited.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    public void MakeJobUnlimited(EntityUid station, JobPrototype job, StationJobsComponent? stationJobs = null)
    {
        MakeJobUnlimited(station, job.ID, stationJobs);
    }

    /// <summary>
    /// Makes the given job have unlimited slots.
    /// </summary>
    /// <param name="station">Station to make a job unlimited on.</param>
    /// <param name="jobPrototypeId">Job prototype ID to make unlimited.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public void MakeJobUnlimited(EntityUid station, string jobPrototypeId, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        // Subtract out the job we're fixing to make have unlimited slots.
        if (stationJobs.JobList.TryGetValue(jobPrototypeId, out var existing))
            stationJobs.TotalJobs -= existing ?? 0;

        stationJobs.JobList[jobPrototypeId] = null;

        UpdateJobsAvailable();
    }

    /// <inheritdoc cref="IsJobUnlimited(Robust.Shared.GameObjects.EntityUid,string,Content.Server.Station.Components.StationJobsComponent?)"/>
    /// <param name="station">Station to check.</param>
    /// <param name="job">Job to check.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    public bool IsJobUnlimited(EntityUid station, JobPrototype job, StationJobsComponent? stationJobs = null)
    {
        return IsJobUnlimited(station, job.ID, stationJobs);
    }

    /// <summary>
    /// Checks if the given job is unlimited.
    /// </summary>
    /// <param name="station">Station to check.</param>
    /// <param name="jobPrototypeId">Job prototype ID to check.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>Returns if the given slot is unlimited.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public bool IsJobUnlimited(EntityUid station, string jobPrototypeId, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        return TryGetJobSlot(station, jobPrototypeId, out var job, stationJobs) && job == null;
    }

    /// <inheritdoc cref="TryGetJobSlot(Robust.Shared.GameObjects.EntityUid,string,out System.Nullable{uint},Content.Server.Station.Components.StationJobsComponent?)"/>
    /// <param name="station">Station to get slot info from.</param>
    /// <param name="job">Job to get slot info for.</param>
    /// <param name="slots">The number of slots remaining. Null if infinite.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    public bool TryGetJobSlot(EntityUid station, JobPrototype job, out int? slots, StationJobsComponent? stationJobs = null)
    {
        return TryGetJobSlot(station, job.ID, out slots, stationJobs);
    }

    /// <summary>
    /// Returns information about the given job slot.
    /// </summary>
    /// <param name="station">Station to get slot info from.</param>
    /// <param name="jobPrototypeId">Job prototype ID to get slot info for.</param>
    /// <param name="slots">The number of slots remaining. Null if infinite.</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>Whether or not the slot exists.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    /// <remarks>slots will be null if the slot doesn't exist, as well, so make sure to check the return value.</remarks>
    public bool TryGetJobSlot(EntityUid station, string jobPrototypeId, out int? slots, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        if (!IsAdvertisedLateJoinJob(station, jobPrototypeId))
        {
            slots = null;
            return false;
        }

        if (!stationJobs.JobList.TryGetValue(jobPrototypeId, out var localSlots))
        {
            slots = null;
            return false;
        }

        var globalJobPrototypeId = GetColcommJobId(jobPrototypeId);
        if (_colcommJobs.TryGetColcommRegistry(out var colcomm)
            && _colcommJobs.TryGetJobSlot(colcomm, globalJobPrototypeId, out var globalSlots))
        {
            slots = GetEffectiveJobSlots(localSlots, globalSlots);
            return true;
        }

        slots = localSlots;
        return true;
    }

    /// <summary>
    /// Returns all jobs available on the station.
    /// </summary>
    /// <param name="station">Station to get jobs for</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>Set containing all jobs available.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public IEnumerable<ProtoId<JobPrototype>> GetAvailableJobs(EntityUid station, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        if (HasComp<ExtraShuttleInformationComponent>(station))
        {
            return stationJobs.JobList.Keys
                .Where(job => TryGetJobSlot(station, job, out var slots, stationJobs) && slots != 0)
                .ToArray();
        }

        if (_colcommJobs.TryGetColcommRegistry(out var colcomm))
        {
            return stationJobs.SetupAvailableJobs.Keys
                .Where(job => _colcommJobs.TryGetJobSlot(colcomm, GetColcommJobId(job), out var slots) && slots != 0)
                .ToArray();
        }

        return stationJobs.JobList.Keys
            .Where(job => TryGetJobSlot(station, job, out var slots, stationJobs) && slots != 0)
            .ToArray();
    }

    /// <summary>
    /// Returns all overflow jobs available on the station.
    /// </summary>
    /// <param name="station">Station to get jobs for</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>Set containing all overflow jobs available.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public IReadOnlySet<ProtoId<JobPrototype>> GetOverflowJobs(EntityUid station, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        return stationJobs.OverflowJobs;
    }

    /// <summary>
    /// Returns a readonly dictionary of all jobs and their slot info.
    /// </summary>
    /// <param name="station">Station to get jobs for</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>List of all jobs on the station.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public IReadOnlyDictionary<ProtoId<JobPrototype>, int?> GetJobs(EntityUid station, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        if (HasComp<ExtraShuttleInformationComponent>(station))
        {
            return stationJobs.JobList.Keys
                .Select(job => (job, found: TryGetJobSlot(station, job, out var slots, stationJobs), slots))
                .Where(entry => entry.found)
                .ToDictionary(entry => entry.job, entry => entry.slots);
        }

        if (_colcommJobs.TryGetColcommRegistry(out var colcomm))
        {
            // Use the per-station TryGetJobSlot helper, which already returns
            // MIN(localSlots, globalSlots). The previous implementation read colcomm-only
            // counts, which could disagree with what TryAssignJob actually enforces:
            // TryAssignJob requires both the per-station JobList count AND the colcomm
            // count to be > 0. When the per-station JobList hit 0 (e.g. depleted by
            // earlier joiners) but colcomm still showed > 0 (because colcomm is shared
            // across stations and may still have headroom, or DynamicJobAllocationRule
            // rebalanced the colcomm registry), the lobby would advertise the slot as
            // open while the join was rejected. Aligning the lobby read with the same
            // helper TryAssignJob effectively gates on closes that gap.
            return stationJobs.SetupAvailableJobs.Keys
                .Select(job => (job, found: TryGetJobSlot(station, job, out var slots, stationJobs), slots))
                .Where(entry => entry.found)
                .ToDictionary(entry => entry.job, entry => entry.slots);
        }

        return stationJobs.JobList.Keys
            .Select(job => (job, found: TryGetJobSlot(station, job, out var slots, stationJobs), slots))
            .Where(entry => entry.found)
            .ToDictionary(entry => entry.job, entry => entry.slots);
    }

    private static int? GetEffectiveJobSlots(int? localSlots, int? globalSlots)
    {
        if (localSlots == null)
            return globalSlots;

        if (globalSlots == null)
            return localSlots;

        return Math.Min(localSlots.Value, globalSlots.Value);
    }

    /// <summary>
    /// Returns a readonly dictionary of all round-start jobs and their slot info.
    /// </summary>
    /// <param name="station">Station to get jobs for</param>
    /// <param name="stationJobs">Resolve pattern, station jobs component of the station.</param>
    /// <returns>List of all round-start jobs.</returns>
    /// <exception cref="ArgumentException">Thrown when the given station is not a station.</exception>
    public Dictionary<ProtoId<JobPrototype>, int?> GetRoundStartJobs(EntityUid station, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        return stationJobs.SetupAvailableJobs.ToDictionary(
            x => x.Key,
            x=> (int?)(x.Value[0] < 0 ? null : x.Value[0]));
    }

    /// <summary>
    /// Looks at the given priority list, and picks the best available job (optionally with the given exclusions)
    /// </summary>
    /// <param name="station">Station to pick from.</param>
    /// <param name="jobPriorities">The priority list to use for selecting a job.</param>
    /// <param name="pickOverflows">Whether or not to pick from the overflow list.</param>
    /// <param name="disallowedJobs">A set of disallowed jobs, if any.</param>
    /// <returns>The selected job, if any.</returns>
    public ProtoId<JobPrototype>? PickBestAvailableJobWithPriority(EntityUid station, IReadOnlyDictionary<ProtoId<JobPrototype>, JobPriority> jobPriorities, bool pickOverflows, IReadOnlySet<ProtoId<JobPrototype>>? disallowedJobs = null)
    {
        if (station == EntityUid.Invalid)
            return null;

        var available = GetAvailableJobs(station);
        bool TryPick(JobPriority priority, [NotNullWhen(true)] out ProtoId<JobPrototype>? jobId)
        {
            var filtered = jobPriorities
                .Where(p =>
                            p.Value == priority
                            && disallowedJobs != null
                            && !disallowedJobs.Contains(p.Key)
                            && available.Contains(p.Key))
                .Select(p => p.Key)
                .ToList();

            if (filtered.Count != 0)
            {
                jobId = _random.Pick(filtered);
                return true;
            }

            jobId = default;
            return false;
        }

        if (TryPick(JobPriority.High, out var picked))
        {
            return picked;
        }

        if (TryPick(JobPriority.Medium, out picked))
        {
            return picked;
        }

        if (TryPick(JobPriority.Low, out picked))
        {
            return picked;
        }

        if (!pickOverflows)
            return null;

        var overflows = GetOverflowJobs(station);
        if (overflows.Count == 0)
            return null;

        return _random.Pick(overflows);
    }

    #endregion Public API

    #region Latejoin job management

    private bool _availableJobsDirty;

    private TickerJobsAvailableEvent _cachedAvailableJobs = new(new()); // Frontier: use one dictionary of composite objects instead of two

    /// <summary>
    /// Assembles an event from the current available-to-play jobs.
    /// This is moderately expensive to construct.
    /// </summary>
    /// <returns>The event.</returns>
    private TickerJobsAvailableEvent GenerateJobsAvailableEvent()
    {
        // If late join is disallowed, return no available jobs.
        if (_gameTicker.DisallowLateJoin)
            return new TickerJobsAvailableEvent(new()); // Frontier: changed param type

        var query = EntityQueryEnumerator<StationJobsComponent>();
        var stationsWithCrewRecordsConsole = GetStationsWithCrewRecordsConsole();

        // Frontier: the dictionary inside a dictionary replaced with <NetEntity, StationJobInformation> which is much cleaner.
        var stationJobInformationList = new Dictionary<NetEntity, StationJobInformation>();

        while (query.MoveNext(out var station, out var comp))
        {
            var stationNetEntity = GetNetEntity(station);
            var list = GetJobs(station, comp).ToDictionary(x => x.Key, x => x.Value); // HardLight: Editted

            // Frontier: overwrite station/vessel information generation
            var isLateJoinStation = false;
            VesselDisplayInformation? vesselDisplay = null;
            StationDisplayInformation? stationDisplay = null;
            if (TryComp<ExtraShuttleInformationComponent>(station, out var extraVesselInfo))
            {
                if (!stationsWithCrewRecordsConsole.Contains(station))
                    continue;

                if (!list.Any(x => x.Value != 0))
                    continue;

                vesselDisplay = new VesselDisplayInformation(
                    vesselAdvertisement: extraVesselInfo.Advertisement,
                    vessel: extraVesselInfo.Vessel,
                    hiddenIfNoJobs: extraVesselInfo.HiddenWithoutOpenJobs
                );
            }
            else
            {
                isLateJoinStation = true;
                if (TryComp<ExtraStationInformationComponent>(station, out var extraStationInformation))
                {
                    stationDisplay = new StationDisplayInformation(
                        stationSubtext: extraStationInformation.StationSubtext,
                        stationDescription: extraStationInformation.StationDescription,
                        stationIcon: extraStationInformation.IconPath,
                        lobbySortOrder: extraStationInformation.LobbySortOrder,
                        hiddenIfNoJobs: extraStationInformation.HiddenWithoutOpenJobs // <-- Add this line
                    );
                }
            }
            var stationJobInformation = new StationJobInformation(
                stationName: Name(station),
                jobsAvailable: list,
                isLateJoinStation: isLateJoinStation,
                stationDisplayInfo: stationDisplay,
                vesselDisplayInfo: vesselDisplay
            );
            stationJobInformationList.Add(stationNetEntity, stationJobInformation);
            // End Frontier: overwrite station/vessel information generation
        }
        return new TickerJobsAvailableEvent(stationJobInformationList); // Frontier: changed param type
    }

    private HashSet<EntityUid> GetStationsWithCrewRecordsConsole()
    {
        var stations = new HashSet<EntityUid>();
        var consoleQuery = AllEntityQuery<GeneralStationRecordConsoleComponent, TransformComponent>();

        while (consoleQuery.MoveNext(out var uid, out _, out var xform))
        {
            if (_station.GetOwningStation(uid, xform) is { } stationUid)
            {
                stations.Add(stationUid);
            }
        }

        return stations;
    }

    /// <summary>
    /// Updates the cached available jobs. Moderately expensive.
    /// </summary>
    public void UpdateJobsAvailable() // Frontier: private<public
    {
        _availableJobsDirty = true;
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        RaiseNetworkEvent(_cachedAvailableJobs, ev.PlayerSession.Channel);
    }

    private void OnStationRenamed(EntityUid uid, StationJobsComponent component, StationRenamedEvent args)
    {
        UpdateJobsAvailable();
    }

    #endregion
}
