using Content.Server.Shuttles.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared.Station.Components;
using Content.Server.Cargo.Systems;
using Content.Server._HL.Shipyard; // HardLight
using Content.Server.Shuttles.Save; // HardLight
using Robust.Shared.Timing; // For IGameTiming
using Content.Server.Station.Systems;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._NF.Shipyard;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Content.Shared._NF.CCVar;
using Robust.Shared.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.IO; // HardLight
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text; // HardLight
using System.Text.RegularExpressions; // HardLight
using Robust.Shared.Serialization; // HardLight
using Robust.Shared.Serialization.Markdown; // HardLight
using Robust.Shared.Serialization.Markdown.Mapping; // HardLight
using Robust.Shared.Serialization.Markdown.Sequence; // HardLight
using Robust.Shared.Serialization.Markdown.Value; // HardLight
using Content.Shared._NF.Shipyard.Events;
using Content.Shared.Mobs.Components;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Containers;
using Content.Server._NF.Station.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Utility;
using Robust.Shared.ContentPack;
using Content.Shared.Shuttles.Components; // For IFFComponent
using Content.Shared.Timing;
using Content.Server.Gravity;
using Robust.Shared.Physics;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components; // For GravitySystem
using Robust.Shared.Map.Events; // HardLight
using Robust.Shared.Prototypes;
using Content.Server.Cargo.Components;
using Content.Server.Storage.Components;
using Content.Shared.Storage;
using YamlDotNet.Core; // HardLight
using YamlDotNet.RepresentationModel; // HardLight

namespace Content.Server._NF.Shipyard.Systems;

public sealed partial class ShipyardSystem : SharedShipyardSystem
{
    private static readonly Regex ShipSaveProtoLineRegex = new(@"^(\s*)- proto:\s*(.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant); // HardLight
    private static readonly Regex ShipSaveUidLineRegex = new(@"^\s*- uid:\s*\d+", RegexOptions.Compiled | RegexOptions.CultureInvariant); // HardLight
    private static readonly Regex ShipSaveUidCaptureLineRegex = new(@"^\s*-\s*uid:\s*(\d+)\b", RegexOptions.Compiled | RegexOptions.CultureInvariant); // HardLight
    private static readonly Regex ShipSaveEntitiesSectionRegex = new(@"^(\s*)entities\s*:\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant); // HardLight
    private static readonly Regex ShipSaveLegacyUidLineRegex = new(@"^(\s*)-\s*uid\s*:\s*\d+\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant); // HardLight
    private static readonly Regex ShipSaveLegacyTypeLineRegex = new(@"^\s*type\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant); // HardLight
    private static readonly FieldInfo? ContainerListField = typeof(Container).GetField("_containerList", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? ContainerSlotEntityField = typeof(ContainerSlot).GetField("_containedEntity", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? ContainerSlotArrayField = typeof(ContainerSlot).GetField("_containedEntityArray", BindingFlags.Instance | BindingFlags.NonPublic);

    // HardLight: Set of tokens that, if found as UIDs in the YAML, indicate a stale or invalid UID
    // that should be sanitized during load to prevent deserialization failures.
    private static readonly HashSet<string> StaleSerializedUidTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "invalid",
        "null",
        "~",
        "0",
    };

    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly DockingSystem _docking = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!; // For safe container removal before deletion
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly IGameTiming _timing = default!; // For cooldown timing
    [Dependency] private readonly ShipSerializationSystem _shipSerialization = default!; // HardLight

    private EntityQuery<TransformComponent> _transformQuery;

    public MapId? ShipyardMap { get; private set; }
    private float _shuttleIndex;
    private const float ShuttleSpawnBuffer = 1f;
    private ISawmill _sawmill = default!;
    private bool _enabled;
    private float _baseSaleRate;
    private readonly Dictionary<EntityUid, TimeSpan> _lastLoadCharge = new(); // Per-player load charge cooldown
    private readonly Dictionary<EntityUid, TimeSpan> _shipyardActionDelayUntil = new(); // HardLight
    private static readonly TimeSpan ShipyardActionDelay = TimeSpan.FromSeconds(1); // HardLight
    private HashSet<string>? _activeLoadDeletedPrototypes; // HardLight

    // The type of error from the attempted sale of a ship.
    public enum ShipyardSaleError
    {
        Success, // Ship can be sold.
        Undocked, // Ship is not docked with the station.
        OrganicsAboard, // Sapient intelligence is aboard, cannot sell, would delete the organics
        InvalidShip, // Ship is invalid
        MessageOverwritten, // Overwritten message.
    }

    // TODO: swap to strictly being a formatted message.
    public struct ShipyardSaleResult
    {
        public ShipyardSaleError Error; // Whether or not the ship can be sold.
        public string? OrganicName; // In case an organic is aboard, this will be set to the first that's aboard.
        public string? OverwrittenMessage; // The message to write if Error is MessageOverwritten.
    }

    public override void Initialize()
    {
        base.Initialize();

        _transformQuery = GetEntityQuery<TransformComponent>();

        // FIXME: Load-bearing jank - game doesn't want to create a shipyard map at this point.
        _enabled = _configManager.GetCVar(NFCCVars.Shipyard);
        _configManager.OnValueChanged(NFCCVars.Shipyard, SetShipyardEnabled); // NOTE: run immediately set to false, see comment above

        _configManager.OnValueChanged(NFCCVars.ShipyardSellRate, SetShipyardSellRate, true);
        _sawmill = Logger.GetSawmill("shipyard");

        SubscribeLocalEvent<ShipyardConsoleComponent, ComponentStartup>(OnShipyardStartup);
        SubscribeLocalEvent<ShipyardConsoleComponent, BoundUIOpenedEvent>(OnConsoleUIOpened);
        SubscribeLocalEvent<ShipyardConsoleComponent, ShipyardConsoleSellMessage>(OnSellMessage);
        // Docked-grid deed creation is handled in Shuttle Records, not Shipyard
        SubscribeLocalEvent<ShipyardConsoleComponent, ShipyardConsolePurchaseMessage>(OnPurchaseMessage);
        // Ship saving/loading functionality
        SubscribeLocalEvent<ShipyardConsoleComponent, ShipyardConsoleLoadMessage>(OnLoadMessage);
        SubscribeLocalEvent<ShipyardConsoleComponent, EntInsertedIntoContainerMessage>(OnItemSlotChanged);
        SubscribeLocalEvent<ShipyardConsoleComponent, EntRemovedFromContainerMessage>(OnItemSlotChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<StationDeedSpawnerComponent, MapInitEvent>(OnInitDeedSpawner);
        SubscribeLocalEvent<BeforeEntityReadEvent>(OnBeforeEntityRead); // HardLight
        InitializeShuttleLifecycleCleanup(); // HardLight
    }

    public override void Shutdown()
    {
        _configManager.UnsubValueChanged(NFCCVars.Shipyard, SetShipyardEnabled);
        _configManager.UnsubValueChanged(NFCCVars.ShipyardSellRate, SetShipyardSellRate);
    }
    private void OnShipyardStartup(EntityUid uid, ShipyardConsoleComponent component, ComponentStartup args)
    {
        if (!_enabled)
            return;
        InitializeConsole();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _shipyardActionDelayUntil.Clear(); // HardLight
        _lastLoadCharge.Clear(); // HardLight
        CleanupShipyard();
    }

    // HardLight: Injects deleted prototype IDs into a temporary set used during ship load to allow YAML loads to succeed by ignoring missing prototypes,
    // while still logging their absence for debugging and future cleanup.
    private void OnBeforeEntityRead(BeforeEntityReadEvent ev)
    {
        if (_activeLoadDeletedPrototypes == null || _activeLoadDeletedPrototypes.Count == 0)
            return;

        foreach (var prototypeId in _activeLoadDeletedPrototypes)
        {
            ev.DeletedPrototypes.Add(prototypeId);
        }
    }

    private void SetShipyardEnabled(bool value)
    {
        if (_enabled == value)
            return;

        _enabled = value;

        if (value)
            SetupShipyardIfNeeded();
        else
            CleanupShipyard();
    }

    private void SetShipyardSellRate(float value)
    {
        _baseSaleRate = Math.Clamp(value, 0.0f, 1.0f);
    }

    // Docked-grid deed creation logic removed from Shipyard; use Shuttle Records console instead

    /// <summary>
    /// Adds a ship to the shipyard, calculates its price, and attempts to ftl-dock it to the given station
    /// </summary>
    /// <param name="stationUid">The ID of the station to dock the shuttle to</param>
    /// <param name="shuttlePath">The path to the shuttle file to load. Must be a grid file!</param>
    /// <param name="shuttleEntityUid">The EntityUid of the shuttle that was purchased</param>
    /// <summary>
    /// Purchases a shuttle and docks it to the grid the console is on, independent of station data.
    /// </summary>
    /// <param name="consoleUid">The entity of the shipyard console to dock to its grid</param>
    /// <param name="shuttlePath">The path to the shuttle file to load. Must be a grid file!</param>
    /// <param name="shuttleEntityUid">The EntityUid of the shuttle that was purchased</param>
    public bool TryPurchaseShuttle(EntityUid consoleUid, ResPath shuttlePath, [NotNullWhen(true)] out EntityUid? shuttleEntityUid)
    {
        // Get the grid the console is on
        if (!_transformQuery.TryComp(consoleUid, out var consoleXform) || consoleXform.GridUid == null)
        {
            shuttleEntityUid = null;
            return false;
        }

        if (!TryAddShuttle(shuttlePath, out var shuttleGrid))
        {
            shuttleEntityUid = null;
            return false;
        }

        var grid = shuttleGrid.Value;

        if (!TryComp<ShuttleComponent>(grid, out var shuttleComponent))
        {
            shuttleEntityUid = null;
            return false;
        }

        var targetGrid = consoleXform.GridUid.Value;

        //_sawmill.Info($"Shuttle {shuttlePath} was purchased at {ToPrettyString(consoleUid)} for {price:f2}");

        // Ensure required components for docking and identification
        EntityManager.EnsureComponent<Robust.Shared.Physics.Components.PhysicsComponent>(grid);
        EntityManager.EnsureComponent<ShuttleComponent>(grid);
        var iff = EntityManager.EnsureComponent<IFFComponent>(grid);
        // Add new grid to the same station as the console's grid (for IFF / ownership), if any
        if (TryComp<StationMemberComponent>(consoleXform.GridUid, out var stationMember))
        {
            _station.AddGridToStation(stationMember.Station, grid);
        }

        _shuttle.TryFTLDock(grid, shuttleComponent, targetGrid);
        QueueShipyardMapCleanupIfEmpty(); // HardLight
        shuttleEntityUid = grid;
        return true;
    }

    /// <summary>
    /// Loads a shuttle from a file and docks it to the grid the console is on, like ship purchases.
    /// This is used for loading saved ships.
    /// </summary>
    /// <param name="consoleUid">The entity of the shipyard console to dock to its grid</param>
    /// <param name="shuttlePath">The path to the shuttle file to load. Must be a grid file!</param>
    /// <param name="shuttleEntityUid">The EntityUid of the shuttle that was loaded</param>
    public bool TryPurchaseShuttleFromFile(EntityUid consoleUid, ResPath shuttlePath, [NotNullWhen(true)] out EntityUid? shuttleEntityUid)
    {
        if (!TryAddShuttle(shuttlePath, out var shuttleGrid)) // HardLight
        {
            shuttleEntityUid = null;
            return false;
        }

        return TryFinalizeLoadedShuttle(consoleUid, shuttleGrid.Value, out shuttleEntityUid); // HardLight
    }

    /// <summary>
    /// Loads a shuttle into the shipyard staging map from a grid file.
    /// </summary>
    /// <param name="shuttlePath">The path to the grid file to load. Must be a grid file!</param>
    /// <returns>Returns the EntityUid of the shuttle</returns>
    private bool TryAddShuttle(ResPath shuttlePath, [NotNullWhen(true)] out EntityUid? shuttleGrid)
    {
        shuttleGrid = null;
        SetupShipyardIfNeeded();
        if (ShipyardMap == null)
            return false;

        if (!_mapLoader.TryLoadGrid(ShipyardMap.Value, shuttlePath, out var grid, offset: new Vector2(500f + _shuttleIndex, 1f)))
        {
            //_sawmill.Error($"Unable to spawn shuttle {shuttlePath}");
            return false;
        }

        _shuttleIndex += grid.Value.Comp.LocalAABB.Width + ShuttleSpawnBuffer;

        shuttleGrid = grid.Value.Owner;
        return true;
    }

    /// <summary>
    /// Loads shuttle YAML straight from memory so ship loads do not have to bounce through a temp file.
    /// </summary>
    private bool TryAddShuttleFromYamlData(string yamlData, string fileName, [NotNullWhen(true)] out EntityUid? shuttleGrid)
    {
        shuttleGrid = null;

        if (string.IsNullOrWhiteSpace(yamlData))
            return false;

        SetupShipyardIfNeeded();
        if (ShipyardMap == null)
            return false;

        var options = new MapLoadOptions
        {
            MergeMap = ShipyardMap.Value,
            Offset = new Vector2(500f + _shuttleIndex, 1f),
            DeserializationOptions = DeserializationOptions.Default,
            ExpectedCategory = FileCategory.Grid,
        };

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yamlData));
        if (!_mapLoader.TryLoadGeneric(stream, fileName, out var result, options) || result == null)
            return false;

        if (result.Grids.Count != 1)
        {
            _mapLoader.Delete(result);
            return false;
        }

        var grid = result.Grids.Single();
        _shuttleIndex += grid.Comp.LocalAABB.Width + ShuttleSpawnBuffer;

        shuttleGrid = grid.Owner;
        return true;
    }

    private bool TryPurchaseShuttleFromRawYamlData(EntityUid consoleUid, string yamlData, string fileName, [NotNullWhen(true)] out EntityUid? shuttleEntityUid)
    {
        shuttleEntityUid = null;

        if (!TryAddShuttleFromYamlData(yamlData, fileName, out var shuttleGrid))
            return false;

        return TryFinalizeLoadedShuttle(consoleUid, shuttleGrid.Value, out shuttleEntityUid);
    }

    /// <summary>
    /// Tries the normal strict load path first, then falls back through the compatibility recovery steps.
    /// </summary>
    private bool TryPurchaseShuttleFromYamlData(EntityUid consoleUid, string yamlData, [NotNullWhen(true)] out EntityUid? shuttleEntityUid)
    {
        shuttleEntityUid = null;
        var fileName = $"shipyard_load_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.yml";

        // HardLight: scrub legacy saves through the same sanitizer used at save time.
        // Modern saves carry SanitizedMarkerComment and skip this work entirely; only
        // pre-marker saves pay the parse/walk/emit cost, and only once - the next save will
        // re-stamp them. This keeps load-time CPU low for the common case while still fixing
        // existing users' broken ships without forcing them to re-save.
        if (!HasSanitizedMarker(yamlData))
            yamlData = ApplyShipSaveSanitizerForLoad(yamlData);

        try
        {
            // Try the original YAML first.
            if (TryPurchaseShuttleFromYamlDataSafe(consoleUid, yamlData, fileName, out shuttleEntityUid))
                return true;

            _sawmill.Debug("[ShipLoad] Strict grid YAML load failed; attempting compatibility recovery stages.");

            var recoveryYaml = yamlData;

            // Strip out missing prototypes and retry before doing anything more invasive.
            var sanitizedYaml = SanitizeLoadYamlMissingPrototypes(yamlData, out var removedProtoBlocks, out var removedEntities);
            var deletedPrototypeIds = FindMissingPrototypeIdsForLoad(sanitizedYaml);
            var needsSanitizedRetry = removedProtoBlocks > 0
                                      || deletedPrototypeIds.Count > 0
                                      || !string.Equals(sanitizedYaml, yamlData, StringComparison.Ordinal);

            if (needsSanitizedRetry)
            {
                if (removedProtoBlocks > 0)
                {
                    _sawmill.Warning($"[ShipLoad] Removed {removedProtoBlocks} invalid prototype block(s) containing {removedEntities} entities from ship YAML before load.");
                }

                if (deletedPrototypeIds.Count > 0)
                {
                    _sawmill.Warning($"[ShipLoad] Ignoring {deletedPrototypeIds.Count} missing prototype id(s) during ship load.");
                }

                _activeLoadDeletedPrototypes = deletedPrototypeIds.Count > 0 ? deletedPrototypeIds : null;
                recoveryYaml = sanitizedYaml;

                if (TryPurchaseShuttleFromYamlDataSafe(consoleUid, recoveryYaml, fileName, out shuttleEntityUid))
                    return true;
            }

            // Last strict retry: drop serialized component payloads and fall back to prototype defaults.
            var strippedYaml = StripSerializedComponentsForRecovery(recoveryYaml);
            if (!string.Equals(strippedYaml, recoveryYaml, StringComparison.Ordinal))
            {
                if (TryPurchaseShuttleFromYamlDataSafe(consoleUid, strippedYaml, fileName, out shuttleEntityUid))
                {
                    _sawmill.Warning("[ShipLoad] Loaded ship after stripping serialized component payloads for compatibility recovery.");
                    return true;
                }

                _sawmill.Debug("[ShipLoad] Component-stripped recovery load failed.");
            }

            // If strict load still cannot recover it, rebuild from ship data and skip anything unusable.
            if (TryPurchaseShuttleFromShipDataYaml(consoleUid, recoveryYaml, out shuttleEntityUid))
                return true;

            _sawmill.Warning("[ShipLoad] Ship-data tolerant fallback also failed.");

            return false;
        }
        catch (Exception ex)
        {
            _sawmill.Warning($"Failed to purchase shuttle from YAML data: {ex.Message}");
            return false;
        }
        finally
        {
            _activeLoadDeletedPrototypes = null;
        }
    }

    private bool TryPurchaseShuttleFromYamlDataSafe(EntityUid consoleUid, string yamlData, string fileName, [NotNullWhen(true)] out EntityUid? shuttleEntityUid)
    {
        shuttleEntityUid = null;

        try
        {
            return TryPurchaseShuttleFromRawYamlData(consoleUid, yamlData, fileName, out shuttleEntityUid);
        }
        catch (Exception ex)
        {
            _sawmill.Debug($"[ShipLoad] Strict YAML load stage threw exception: {ex.Message}");
            return false;
        }
    }

    // Let strict file loads fail without aborting the whole ship load.
    private bool TryPurchaseShuttleFromFileSafe(EntityUid consoleUid, ResPath shuttlePath, [NotNullWhen(true)] out EntityUid? shuttleEntityUid)
    {
        shuttleEntityUid = null;

        try
        {
            return TryPurchaseShuttleFromFile(consoleUid, shuttlePath, out shuttleEntityUid);
        }
        catch (Exception ex)
        {
            _sawmill.Debug($"[ShipLoad] Strict load stage threw exception: {ex.Message}");
            return false;
        }
    }

    // Tolerant fallback that skips entities we still cannot reconstruct.
    private bool TryPurchaseShuttleFromShipDataYaml(EntityUid consoleUid, string yamlData, [NotNullWhen(true)] out EntityUid? shuttleEntityUid)
    {
        shuttleEntityUid = null;

        SetupShipyardIfNeeded();
        if (ShipyardMap == null)
            return false;

        if (LooksLikeStandardGridYaml(yamlData))
        {
            _sawmill.Debug("[ShipLoad] Skipping ship-data fallback for standard grid YAML.");
            return false;
        }

        try
        {
            var shipData = _shipSerialization.DeserializeShipGridDataFromYaml(yamlData, Guid.Empty, out _);
            var grid = _shipSerialization.ReconstructShipOnMap(shipData, ShipyardMap.Value, new Vector2(500f + _shuttleIndex, 1f));

            if (!TryComp<MapGridComponent>(grid, out var gridComp))
            {
                _sawmill.Warning("[ShipLoad] Ship-data fallback created no grid component.");
                return false;
            }

            _shuttleIndex += gridComp.LocalAABB.Width + ShuttleSpawnBuffer;

            if (!TryFinalizeLoadedShuttle(consoleUid, grid, out shuttleEntityUid))
            {
                SafeDelete(grid);
                return false;
            }

            _sawmill.Info("[ShipLoad] Loaded ship via tolerant ship-data fallback path.");
            return true;
        }
        catch (Exception ex)
        {
            _sawmill.Warning($"[ShipLoad] Ship-data fallback failed: {ex.Message}");
            return false;
        }
    }

    // Finish wiring up a loaded shuttle without letting setup failures abort the whole load.
    private bool TryFinalizeLoadedShuttle(EntityUid consoleUid, EntityUid grid, [NotNullWhen(true)] out EntityUid? shuttleEntityUid)
    {
        shuttleEntityUid = null;

        // Get the grid the console is on
        if (!_transformQuery.TryComp(consoleUid, out var consoleXform) || consoleXform.GridUid == null)
            return false;

        if (!TryComp<ShuttleComponent>(grid, out var shuttleComponent))
            return false;

        var targetGrid = consoleXform.GridUid.Value;

        // Ensure required components for docking and identification
        EnsureComp<PhysicsComponent>(grid);
        EnsureComp<ShuttleComponent>(grid);
        EnsureComp<IFFComponent>(grid);

        // Clear stale load-time state in one pass.
        try
        {
            SanitizeLoadedShuttle(grid);
        }
        catch (Exception ex)
        {
            _sawmill.Warning($"[ShipLoad] SanitizeLoadedShuttle failed on {grid}: {ex.Message}");
        }

        // Add new grid to the same station as the console's grid (for IFF / ownership), if any
        if (TryComp<StationMemberComponent>(consoleXform.GridUid, out var stationMember))
        {
            _station.AddGridToStation(stationMember.Station, grid);
        }

        _shuttle.TryFTLDock(grid, shuttleComponent, targetGrid);
        QueueShipyardMapCleanupIfEmpty(); // HardLight
        shuttleEntityUid = grid;
        return true;
    }

    /// <summary>
    /// Removes grouped entities whose prototypes no longer exist so older ship saves still have a chance to load.
    /// </summary>
    private string SanitizeLoadYamlMissingPrototypes(string yamlData, out int removedPrototypeBlocks, out int removedEntities)
    {
        removedPrototypeBlocks = 0;
        removedEntities = 0;

        if (string.IsNullOrWhiteSpace(yamlData))
            return yamlData;

        // Nothing to do if the save does not use grouped proto blocks.
        if (yamlData.IndexOf("- proto:", StringComparison.Ordinal) < 0)
            return yamlData;

        var normalized = yamlData.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var output = new StringBuilder(normalized.Length);
        var removedEntityUids = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var protoMatch = ShipSaveProtoLineRegex.Match(line);

            if (!protoMatch.Success)
            {
                output.AppendLine(line);
                continue;
            }

            var indent = protoMatch.Groups[1].Value;
            var rawProto = protoMatch.Groups[2].Value;
            var commentIndex = rawProto.IndexOf('#');
            if (commentIndex >= 0)
                rawProto = rawProto[..commentIndex];

            var protoId = rawProto.Trim().Trim('"', '\'');

            // Empty proto blocks are runtime data. Leave them alone.
            var keepBlock = string.IsNullOrWhiteSpace(protoId) || _prototypeManager.TryIndex<Robust.Shared.Prototypes.EntityPrototype>(protoId, out _);
            if (keepBlock)
            {
                output.AppendLine(line);
                continue;
            }

            removedPrototypeBlocks++;

            // Skip this proto block until the next sibling proto entry.
            for (i += 1; i < lines.Length; i++)
            {
                var blockLine = lines[i];
                if (ShipSaveUidLineRegex.IsMatch(blockLine))
                {
                    removedEntities++;
                    var uidMatch = ShipSaveUidCaptureLineRegex.Match(blockLine);
                    if (uidMatch.Success)
                        removedEntityUids.Add(uidMatch.Groups[1].Value);
                }

                var nextProto = ShipSaveProtoLineRegex.Match(blockLine);
                if (!nextProto.Success)
                    continue;

                var nextIndent = nextProto.Groups[1].Value;
                if (nextIndent != indent)
                    continue;

                i -= 1;
                break;
            }
        }

        var sanitizedYaml = output.ToString();
        // Always run the stale-reference prune pass. Even if no prototype blocks were removed,
        // serialized ships can still contain invalid UID tokens like "0" that should be cleared
        // from container/storage data before reconstruction.
        return PruneLoadYamlReferencesToRemovedEntities(sanitizedYaml, removedEntityUids);
    }

    /// <summary>
    /// Removes references to entities stripped by the missing-prototype pass so containers and storage stay sane.
    /// </summary>
    private static string PruneLoadYamlReferencesToRemovedEntities(string yamlData, HashSet<string> removedEntityUids)
    {
        if (string.IsNullOrWhiteSpace(yamlData))
            return yamlData;

        // Use a structured pass when the YAML still parses cleanly.
        try
        {
            using var reader = new StringReader(yamlData);
            var documents = DataNodeParser.ParseYamlStream(reader).ToArray();
            if (documents.Length != 1 || documents[0].Root is not MappingDataNode root)
                return PruneLoadYamlReferencesToRemovedEntitiesLineBased(yamlData, removedEntityUids);

            var knownEntityUids = CollectSerializedEntityUids(root);
            PruneLoadNodeReferencesToRemovedEntities(root, removedEntityUids, knownEntityUids);
            return WriteLoadYamlNodeToString(root);
        }
        catch
        {
            return PruneLoadYamlReferencesToRemovedEntitiesLineBased(yamlData, removedEntityUids);
        }
    }

    // Structured pass for pruning stale container and storage references.
    private static void PruneLoadNodeReferencesToRemovedEntities(MappingDataNode root, HashSet<string> removedEntityUids, HashSet<string> knownEntityUids)
    {
        if (!root.TryGet("entities", out SequenceDataNode? protoSeq) || protoSeq == null)
            return;

        foreach (var protoNode in protoSeq)
        {
            if (protoNode is not MappingDataNode protoMap)
                continue;

            if (protoMap.TryGet("entities", out SequenceDataNode? entitiesSeq) && entitiesSeq != null)
            {
                foreach (var entityNode in entitiesSeq)
                {
                    if (entityNode is MappingDataNode entMap)
                        PruneLoadEntityNodeReferences(entMap, removedEntityUids, knownEntityUids);
                }

                continue;
            }

            // Older ship saves can use a flat legacy entities list under the root `entities:` section.
            // If the parsed YAML is in that shape, each item here is already an entity node.
            PruneLoadEntityNodeReferences(protoMap, removedEntityUids, knownEntityUids);
        }
    }

    private static void PruneLoadEntityNodeReferences(MappingDataNode entMap, HashSet<string> removedEntityUids, HashSet<string> knownEntityUids)
    {
        if (!entMap.TryGet("components", out SequenceDataNode? comps) || comps == null)
            return;

        foreach (var compNode in comps)
        {
            if (compNode is not MappingDataNode compMap)
                continue;

            if (!compMap.TryGet("type", out ValueDataNode? typeNode) || typeNode == null)
                continue;

            var componentType = typeNode.Value;

            if (componentType == "ContainerContainer")
            {
                if (!compMap.TryGet("containers", out MappingDataNode? containersMap) || containersMap == null)
                    continue;

                foreach (var (_, containerNode) in containersMap)
                {
                    if (containerNode is not MappingDataNode containerMap)
                        continue;

                    if (containerMap.TryGet("ents", out SequenceDataNode? entsNode) && entsNode != null)
                    {
                        for (var idx = entsNode.Count - 1; idx >= 0; idx--)
                        {
                            if (entsNode[idx] is not ValueDataNode entValue || entValue.IsNull)
                                continue;

                            if (IsStaleSerializedUidReference(entValue.Value, removedEntityUids))
                                entsNode.RemoveAt(idx);
                        }
                    }

                    if (containerMap.TryGet("ent", out ValueDataNode? entNode) && entNode != null && !entNode.IsNull)
                    {
                        if (IsStaleSerializedUidReference(entNode.Value, removedEntityUids))
                            containerMap["ent"] = ValueDataNode.Null();
                    }
                }

                continue;
            }

            if (componentType == "Actions")
            {
                if (!compMap.TryGet("actions", out SequenceDataNode? actionsNode) || actionsNode == null)
                    continue;

                for (var idx = actionsNode.Count - 1; idx >= 0; idx--)
                {
                    if (actionsNode[idx] is not ValueDataNode actionValue || actionValue.IsNull)
                    {
                        actionsNode.RemoveAt(idx);
                        continue;
                    }

                    var normalized = NormalizeSerializedUidToken(actionValue.Value);
                    if (normalized.Length == 0
                        || IsStaleSerializedUidReference(normalized, removedEntityUids)
                        || !knownEntityUids.Contains(normalized))
                    {
                        actionsNode.RemoveAt(idx);
                    }
                }

                continue;
            }

            if (componentType == "Transform")
            {
                if (compMap.TryGet("parent", out ValueDataNode? parentNode)
                    && parentNode != null
                    && !parentNode.IsNull
                    && IsStaleSerializedUidReference(parentNode.Value, removedEntityUids))
                {
                    compMap["parent"] = new ValueDataNode("invalid");
                }

                continue;
            }

            if (componentType != "Storage"
                || !compMap.TryGet("storedItems", out MappingDataNode? storedItemsMap)
                || storedItemsMap == null)
            {
                continue;
            }

            var removeKeys = new List<string>();
            foreach (var (itemUid, _) in storedItemsMap)
            {
                if (IsStaleSerializedUidReference(itemUid, removedEntityUids))
                    removeKeys.Add(itemUid);
            }

            foreach (var key in removeKeys)
                storedItemsMap.Remove(key);
        }
    }

    private static HashSet<string> CollectSerializedEntityUids(MappingDataNode root)
    {
        var knownEntityUids = new HashSet<string>(StringComparer.Ordinal);

        if (!root.TryGet("entities", out SequenceDataNode? protoSeq) || protoSeq == null)
            return knownEntityUids;

        foreach (var protoNode in protoSeq)
        {
            if (protoNode is not MappingDataNode protoMap)
                continue;

            if (protoMap.TryGet("entities", out SequenceDataNode? entitiesSeq) && entitiesSeq != null)
            {
                foreach (var entityNode in entitiesSeq)
                {
                    if (entityNode is MappingDataNode entMap)
                        AddSerializedEntityUid(entMap, knownEntityUids);
                }

                continue;
            }

            AddSerializedEntityUid(protoMap, knownEntityUids);
        }

        return knownEntityUids;
    }

    private static void AddSerializedEntityUid(MappingDataNode entMap, HashSet<string> knownEntityUids)
    {
        if (!entMap.TryGet("uid", out ValueDataNode? uidNode) || uidNode == null || uidNode.IsNull)
            return;

        var normalized = NormalizeSerializedUidToken(uidNode.Value);
        if (normalized.Length > 0)
            knownEntityUids.Add(normalized);
    }

    // HardLight: Cheap textual probe so we can skip the load-time sanitizer for saves that
    // were already scrubbed at save time. Marker is the first line of the file when present.
    private static bool HasSanitizedMarker(string yamlData)
    {
        if (string.IsNullOrEmpty(yamlData))
            return false;

        // Tolerate a UTF-8 BOM and leading whitespace introduced by editors or transports.
        var start = 0;
        if (yamlData.Length > 0 && yamlData[0] == '\uFEFF')
            start = 1;
        while (start < yamlData.Length && (yamlData[start] == ' ' || yamlData[start] == '\t' || yamlData[start] == '\r' || yamlData[start] == '\n'))
            start++;

        var marker = ShipSaveYamlSanitizer.SanitizedMarkerComment;
        if (yamlData.Length - start < marker.Length)
            return false;

        return string.CompareOrdinal(yamlData, start, marker, 0, marker.Length) == 0;
    }

    // HardLight: Run the ship-save sanitizer over the YAML on the way in. Mirrors the
    // sanitation that ShipyardGridSaveSystem.SerializeShipForSaving applies on the way out,
    // so users with previously-saved ships get the same scrubbing on load (dangling
    // EntityUid refs, filtered components/prototypes, container ref pruning, etc.).
    // Falls back to the original YAML if anything goes wrong - we never want this to break a load.
    private string ApplyShipSaveSanitizerForLoad(string yamlData)
    {
        if (string.IsNullOrWhiteSpace(yamlData))
            return yamlData;

        try
        {
            using var reader = new StringReader(yamlData);
            var documents = DataNodeParser.ParseYamlStream(reader).ToArray();
            if (documents.Length != 1 || documents[0].Root is not MappingDataNode root)
                return yamlData;

            ShipSaveYamlSanitizer.SanitizeShipSaveNode(root, _prototypeManager);
            return WriteLoadYamlNodeToString(root);
        }
        catch (Exception ex)
        {
            _sawmill.Debug($"[ShipLoad] Load-time sanitizer pass skipped: {ex.Message}");
            return yamlData;
        }
    }

    // Write the edited YAML tree back out.
    private static string WriteLoadYamlNodeToString(MappingDataNode root)
    {
        var document = new YamlDocument(root.ToYaml());
        using var writer = new StringWriter();
        var stream = new YamlStream { document };
        stream.Save(new YamlMappingFix(new Emitter(writer)), false);
        return writer.ToString();
    }

    // Fallback for malformed YAML we cannot round-trip through the structured parser.
    private static string PruneLoadYamlReferencesToRemovedEntitiesLineBased(string yamlData, HashSet<string> removedEntityUids)
    {
        if (string.IsNullOrWhiteSpace(yamlData))
            return yamlData;

        var normalized = yamlData.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var output = new StringBuilder(normalized.Length);

        var entsIndent = -1;
        var storedItemsIndent = -1;
        var skipSubtreeIndent = -1;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            var indent = line.Length - trimmed.Length;

            if (skipSubtreeIndent >= 0)
            {
                if (trimmed.Length == 0)
                    continue;

                if (indent > skipSubtreeIndent)
                    continue;

                skipSubtreeIndent = -1;
            }

            if (trimmed.Length == 0)
            {
                output.AppendLine(line);
                continue;
            }

            if (entsIndent >= 0 && indent <= entsIndent)
                entsIndent = -1;

            if (storedItemsIndent >= 0 && indent <= storedItemsIndent)
                storedItemsIndent = -1;

            if (trimmed.StartsWith("ents:", StringComparison.Ordinal))
            {
                entsIndent = indent;
                output.AppendLine(line);
                continue;
            }

            if (trimmed.StartsWith("storedItems:", StringComparison.Ordinal))
            {
                storedItemsIndent = indent;
                output.AppendLine(line);
                continue;
            }

            // Prune sequence entries in ContainerContainer.ents lists.
            if (entsIndent >= 0 && indent > entsIndent)
            {
                var listEntry = trimmed;
                if (listEntry.StartsWith("- ", StringComparison.Ordinal))
                    listEntry = listEntry[2..].Trim();

                if (IsStaleSerializedUidReference(listEntry, removedEntityUids))
                    continue;
            }

            // Null stale single-reference entries in ContainerContainer.ent fields.
            if (trimmed.StartsWith("ent:", StringComparison.Ordinal))
            {
                var entValue = trimmed[4..].Trim();
                if (IsStaleSerializedUidReference(entValue, removedEntityUids))
                {
                    output.Append(' ', indent);
                    output.AppendLine("ent: null");
                    continue;
                }
            }

            // Remove Storage.storedItems entries keyed by removed entity UID.
            if (storedItemsIndent >= 0 && indent > storedItemsIndent)
            {
                var keySpan = trimmed;
                var colonIndex = keySpan.IndexOf(':');
                if (colonIndex > 0)
                {
                    var rawKey = keySpan[..colonIndex].Trim().Trim('"', '\'');
                    if (IsStaleSerializedUidReference(rawKey, removedEntityUids))
                    {
                        skipSubtreeIndent = indent;
                        continue;
                    }
                }
            }

            output.AppendLine(line);
        }

        return output.ToString();
    }

    // Check whether a serialized UID points at something we already stripped out.
    private static bool IsStaleSerializedUidReference(string uidToken, HashSet<string> removedEntityUids)
    {
        var normalized = NormalizeSerializedUidToken(uidToken);
        if (normalized.Length == 0)
            return false;

        if (removedEntityUids.Contains(normalized))
            return true;

        return StaleSerializedUidTokens.Contains(normalized);
    }

    private static string NormalizeSerializedUidToken(string uidToken)
    {
        return uidToken.Trim().Trim('"', '\'');
    }

    private static bool LooksLikeStandardGridYaml(string yamlData)
    {
        if (string.IsNullOrWhiteSpace(yamlData))
            return false;

        return yamlData.Contains("tilemap:", StringComparison.Ordinal)
               || yamlData.Contains("orphans:", StringComparison.Ordinal)
               || yamlData.Contains("maps:", StringComparison.Ordinal);
    }

    /// <summary>
    /// Finds missing prototype ids referenced by the ship save so this load can temporarily ignore them.
    /// </summary>
    private HashSet<string> FindMissingPrototypeIdsForLoad(string yamlData)
    {
        var missing = new HashSet<string>();

        if (string.IsNullOrWhiteSpace(yamlData))
            return missing;

        // No entities section means there is nothing to scan.
        if (yamlData.IndexOf("entities:", StringComparison.OrdinalIgnoreCase) < 0)
            return missing;

        var normalized = yamlData.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');

        var inEntities = false;
        var entitiesIndent = -1;
        var currentLegacyEntityIndent = -1;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            var indent = line.Length - trimmed.Length;
            var entitiesSection = ShipSaveEntitiesSectionRegex.Match(line);
            if (!inEntities && entitiesSection.Success)
            {
                inEntities = true;
                entitiesIndent = entitiesSection.Groups[1].Value.Length;
                continue;
            }

            if (!inEntities)
                continue;

            // Left the entities section.
            if (indent <= entitiesIndent)
            {
                inEntities = false;
                currentLegacyEntityIndent = -1;
                continue;
            }

            // Grouped format: "- proto: <id>"
            var protoMatch = ShipSaveProtoLineRegex.Match(line);
            if (protoMatch.Success)
            {
                var protoId = ParseShipSavePrototypeValue(protoMatch.Groups[2].Value);
                if (!string.IsNullOrWhiteSpace(protoId)
                    && !_prototypeManager.TryIndex<Robust.Shared.Prototypes.EntityPrototype>(protoId, out _))
                {
                    missing.Add(protoId);
                }

                currentLegacyEntityIndent = -1;
                continue;
            }

            // Legacy format entity boundary: "- uid: <n>"
            var uidMatch = ShipSaveLegacyUidLineRegex.Match(line);
            if (uidMatch.Success)
            {
                currentLegacyEntityIndent = uidMatch.Groups[1].Value.Length;
                continue;
            }

            // If we're no longer in the current legacy entity block, clear it.
            if (currentLegacyEntityIndent >= 0 && indent <= currentLegacyEntityIndent)
            {
                currentLegacyEntityIndent = -1;
            }

            if (currentLegacyEntityIndent < 0)
                continue;

            // Legacy format prototype: "type: <id>" under the current uid block.
            var legacyTypeMatch = ShipSaveLegacyTypeLineRegex.Match(line);
            if (!legacyTypeMatch.Success)
                continue;

            var legacyProtoId = ParseShipSavePrototypeValue(legacyTypeMatch.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(legacyProtoId)
                && !_prototypeManager.TryIndex<Robust.Shared.Prototypes.EntityPrototype>(legacyProtoId, out _))
            {
                missing.Add(legacyProtoId);
            }
        }

        return missing;
    }

    // Pull a prototype id out of a raw YAML value.
    private static string ParseShipSavePrototypeValue(string rawValue)
    {
        var commentIndex = rawValue.IndexOf('#');
        if (commentIndex >= 0)
            rawValue = rawValue[..commentIndex];

        return rawValue.Trim().Trim('"', '\'');
    }

    /// <summary>
    /// Compatibility fallback for older saves: drop serialized component payloads and use prototype defaults.
    /// </summary>
    private string StripSerializedComponentsForRecovery(string yamlData)
    {
        if (string.IsNullOrWhiteSpace(yamlData))
            return yamlData;

        if (yamlData.IndexOf("components:", StringComparison.Ordinal) < 0
            && yamlData.IndexOf("missingComponents:", StringComparison.Ordinal) < 0)
            return yamlData;

        var normalized = yamlData.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var output = new StringBuilder(normalized.Length);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            var indent = line.Length - trimmed.Length;

            var isComponentsStart = trimmed.StartsWith("components:", StringComparison.Ordinal)
                                    || trimmed.StartsWith("missingComponents:", StringComparison.Ordinal);
            if (!isComponentsStart)
            {
                output.AppendLine(line);
                continue;
            }

            // Skip this block and all deeper-indented lines that belong to it.
            for (i += 1; i < lines.Length; i++)
            {
                var nextLine = lines[i];
                var nextTrimmed = nextLine.TrimStart();

                if (nextTrimmed.Length == 0)
                    continue;

                var nextIndent = nextLine.Length - nextTrimmed.Length;
                if (nextIndent > indent)
                    continue;

                i -= 1;
                break;
            }
        }

        return output.ToString(); // HardLight
    }

    /// <summary>
    /// Walks a transform tree so ship-load sanitation only touches the newly loaded shuttle.
    /// </summary>
    private void VisitEntityAndDescendants(EntityUid rootUid, Action<EntityUid> visitor)
    {
        if (!_transformQuery.TryComp(rootUid, out _))
            return;

        var pending = new Stack<EntityUid>();
        pending.Push(rootUid);

        while (pending.Count > 0)
        {
            var uid = pending.Pop();
            visitor(uid);

            if (!_transformQuery.TryComp(uid, out var xform))
                continue;

            var childEnumerator = xform.ChildEnumerator;
            while (childEnumerator.MoveNext(out var child))
            {
                pending.Push(child);
            }
        }
    }

    /// <summary>
    /// Safely deletes an entity by ensuring it is first removed from any container relationships, and
    /// recursively clears any contents if the entity itself owns containers. This avoids client-side
    /// asserts when an entity is detached to null-space while still flagged as InContainer.
    /// </summary>
    private void SafeDelete(EntityUid uid)
    {
        try
        {
            // If this entity owns containers, empty them first.
            if (TryComp<ContainerManagerComponent>(uid, out var manager))
            {
                foreach (var container in manager.Containers.Values)
                {
                    // Copy to avoid modifying during iteration
                    foreach (var contained in container.ContainedEntities.ToArray())
                    {
                        try
                        {
                            _container.Remove(contained, container, force: true);
                        }
                        catch { /* best-effort */ }

                        // Recursively ensure any nested containers are emptied then delete.
                        SafeDelete(contained);
                    }
                }
            }

            // Ensure the entity itself is not inside a container anymore (paranoia in case callers misclassify parent).
            _container.TryRemoveFromContainer(uid);
        }
        catch { /* best-effort */ }

        // Finally queue the deletion of the entity itself.
        QueueDel(uid);
    }

    /// <summary>
    /// Removes stale load-state in one pass so large ships do not pay for repeated full-grid traversals.
    /// This includes removing any deserialized JointComponent instances and clearing DockingComponent
    /// joint references so physics does not encounter stale or invalid body UIDs from YAML state.
    /// The DockingSystem will recreate proper weld joints during docking, and use delays are reset here too.
    /// </summary>
    private void SanitizeLoadedShuttle(EntityUid gridUid)
    {
        var prunedContainers = 0;

        VisitEntityAndDescendants(gridUid, uid =>
        {
            RemComp<JointComponent>(uid);

            if (TryComp<ContainerManagerComponent>(uid, out var manager))
                prunedContainers += PruneInvalidContainerContents(uid, manager);

            if (TryComp<DockingComponent>(uid, out var dock))
            {
                dock.DockJoint = null;
                dock.DockJointId = null;

                if (dock.DockedWith != null)
                {
                    var other = dock.DockedWith.Value;
                    if (!other.IsValid() || !HasComp<MetaDataComponent>(other))
                        dock.DockedWith = null;
                }
            }

            if (TryComp<UseDelayComponent>(uid, out var useDelay))
                _useDelay.ResetAllDelays((uid, useDelay));
        });

        if (prunedContainers > 0)
            _sawmill.Warning($"[ShipLoad] Pruned {prunedContainers} stale contained UID reference(s) from loaded shuttle {ToPrettyString(gridUid)}.");
    }

    private int PruneInvalidContainerContents(EntityUid owner, ContainerManagerComponent manager)
    {
        var pruned = 0;

        foreach (var container in manager.Containers.Values)
        {
            pruned += PruneInvalidContainerContents(owner, container);
        }

        if (pruned > 0)
            Dirty(owner, manager);

        return pruned;
    }

    private int PruneInvalidContainerContents(EntityUid owner, BaseContainer container)
    {
        var stale = new List<EntityUid>();

        foreach (var contained in container.ContainedEntities.ToArray())
        {
            if (!contained.IsValid()
                || TerminatingOrDeleted(contained)
                || !HasComp<MetaDataComponent>(contained)
                || !HasComp<TransformComponent>(contained))
            {
                stale.Add(contained);
            }
        }

        if (stale.Count == 0)
            return 0;

        switch (container)
        {
            case Container standard when ContainerListField?.GetValue(standard) is List<EntityUid> list:
                foreach (var contained in stale)
                {
                    list.Remove(contained);
                }

                return stale.Count;

            case ContainerSlot slot when slot.ContainedEntity is { } contained && stale.Contains(contained):
                ContainerSlotEntityField?.SetValue(slot, null);
                ContainerSlotArrayField?.SetValue(slot, null);
                return 1;

            default:
                foreach (var contained in stale)
                {
                    _sawmill.Warning($"[ShipLoad] Failed to scrub stale contained UID {contained} from {container.GetType().Name} on {ToPrettyString(owner)}.");
                }

                return 0;
        }
    }

    /// <summary>
    /// Checks a shuttle to make sure that it is docked to the given station, and that there are no lifeforms aboard. Then it teleports tagged items on top of the console, appraises the grid, outputs to the server log, and deletes the grid
    /// </summary>
    /// <param name="stationUid">The ID of the station that the shuttle is docked to</param>
    /// <param name="shuttleUid">The grid ID of the shuttle to be appraised and sold</param>
    /// <param name="consoleUid">The ID of the console being used to sell the ship</param>
    /// <summary>
    /// Sells a shuttle, checking that it is docked to the grid the console is on, and not to a station.
    /// </summary>
    /// <param name="shuttleUid">The grid ID of the shuttle to be appraised and sold</param>
    /// <param name="consoleUid">The ID of the console being used to sell the ship</param>
    /// <param name="bill">The amount the shuttle is sold for</param>
    public ShipyardSaleResult TrySellShuttle(EntityUid shuttleUid, EntityUid consoleUid, out int bill)
    {
        ShipyardSaleResult result = new ShipyardSaleResult();
        bill = 0;

        if (!HasComp<ShuttleComponent>(shuttleUid)
            || !_transformQuery.TryComp(shuttleUid, out var xform)
            || !_transformQuery.TryComp(consoleUid, out var consoleXform)
            || consoleXform.GridUid == null) // HardLight
        {
            result.Error = ShipyardSaleError.InvalidShip;
            return result;
        }

        var targetGrid = consoleXform.GridUid.Value;
        var gridDocks = _docking.GetDocks(targetGrid);
        var shuttleDocks = _docking.GetDocks(shuttleUid);
        var isDocked = false;

        foreach (var shuttleDock in shuttleDocks)
        {
            foreach (var gridDock in gridDocks)
            {
                if (shuttleDock.Comp.DockedWith == gridDock.Owner)
                {
                    isDocked = true;
                    break;
                }
            }
            if (isDocked)
                break;
        }

        if (!isDocked)
        {
            //_sawmill.Warning($"shuttle is not docked to the console's grid");
            result.Error = ShipyardSaleError.Undocked;
            return result;
        }

        var mobQuery = GetEntityQuery<MobStateComponent>();
        var xformQuery = GetEntityQuery<TransformComponent>();

        var charName = FoundOrganics(shuttleUid, mobQuery, xformQuery);
        if (charName is not null)
        {
            //_sawmill.Warning($"organics on board");
            result.Error = ShipyardSaleError.OrganicsAboard;
            result.OrganicName = charName;
            return result;
        }

        if (TryComp<ShipyardConsoleComponent>(consoleUid, out var comp))
        {
            CleanGrid(shuttleUid, consoleUid);
        }

        bill = (int) AppraiseGridForShipyardSale(shuttleUid);
        QueueDel(shuttleUid);
        //_sawmill.Info($"Sold shuttle {shuttleUid} for {bill}");

        // Update all record UI (skip records, no new records)
        _shuttleRecordsSystem.RefreshStateForAll(true);

        result.Error = ShipyardSaleError.Success;
        return result;
    }

    private void CleanGrid(EntityUid grid, EntityUid destination)
    {
        var xform = Transform(grid);
        var enumerator = xform.ChildEnumerator;
        var entitiesToPreserve = new List<EntityUid>();

        while (enumerator.MoveNext(out var child))
        {
            FindEntitiesToPreserve(child, ref entitiesToPreserve);
        }
        foreach (var ent in entitiesToPreserve)
        {
            // Teleport this item and all its children to the floor (or space).
            _transform.SetCoordinates(ent, new EntityCoordinates(destination, 0, 0));
            _transform.AttachToGridOrMap(ent);
        }
    }

    // checks if something has the ShipyardPreserveOnSaleComponent and if it does, adds it to the list
    private void FindEntitiesToPreserve(EntityUid entity, ref List<EntityUid> output)
    {
        if (TryComp<ShipyardSellConditionComponent>(entity, out var comp) && comp.PreserveOnSale == true)
        {
            output.Add(entity);
            return;
        }
        else if (TryComp<ContainerManagerComponent>(entity, out var containers))
        {
            foreach (var container in containers.Containers.Values)
            {
                foreach (var ent in container.ContainedEntities)
                {
                    FindEntitiesToPreserve(ent, ref output);
                }
            }
        }
    }

    // returns false if it has ShipyardPreserveOnSaleComponent, true otherwise
    private bool LacksPreserveOnSaleComp(EntityUid uid)
    {
        return !TryComp<ShipyardSellConditionComponent>(uid, out var comp) || comp.PreserveOnSale == false;
    }

    // Shipyard appraisal should not trigger price hooks that mutate game state.
    // Normal ship contents still count. The only special cases here are items that
    // manufacture value during pricing or depend on outside reward systems.
    private bool IsSafeForShipyardAppraisal(EntityUid uid)
    {
        return !HasComp<SpawnItemsOnUseComponent>(uid)
               && !HasComp<CargoBountyLabelComponent>(uid);
    }

    private bool IsSafeForShipyardSaleAppraisal(EntityUid uid)
    {
        return LacksPreserveOnSaleComp(uid) && IsSafeForShipyardAppraisal(uid);
    }

    private double AppraiseGridForShipyard(EntityUid gridUid)
    {
        return AppraiseGridForShipyard(gridUid, saleMode: false);
    }

    private double AppraiseGridForShipyardSale(EntityUid gridUid)
    {
        return AppraiseGridForShipyard(gridUid, saleMode: true);
    }

    private double AppraiseGridForShipyard(EntityUid gridUid, bool saleMode)
    {
        Func<EntityUid, bool> predicate = saleMode ? IsSafeForShipyardSaleAppraisal : IsSafeForShipyardAppraisal;
        var price = _pricing.AppraiseGrid(gridUid, predicate);

        VisitEntityAndDescendants(gridUid, uid =>
        {
            if (saleMode && !LacksPreserveOnSaleComp(uid))
                return;

            price += GetShipyardSpecialCaseAppraisal(uid);
        });

        return price;
    }

    private double GetShipyardSpecialCaseAppraisal(EntityUid uid)
    {
        if (TryComp<SpawnItemsOnUseComponent>(uid, out var spawnItems))
            return GetEstimatedSpawnItemsOnUsePrice(spawnItems);

        return 0.0;
    }

    private double GetEstimatedSpawnItemsOnUsePrice(SpawnItemsOnUseComponent component)
    {
        var price = 0.0;
        var ungrouped = EntitySpawnCollection.CollectOrGroups(component.Items, out var orGroups);

        foreach (var entry in ungrouped)
        {
            if (entry.PrototypeId is not { } prototypeId || !_prototypeManager.TryIndex<EntityPrototype>(prototypeId, out var prototype))
                continue;

            price += _pricing.GetEstimatedPrice(prototype) * entry.SpawnProbability * entry.GetAmount(Random.Shared, getAverage: true);
        }

        foreach (var group in orGroups)
        {
            foreach (var entry in group.Entries)
            {
                if (entry.PrototypeId is not { } prototypeId || !_prototypeManager.TryIndex<EntityPrototype>(prototypeId, out var prototype))
                    continue;

                price += _pricing.GetEstimatedPrice(prototype)
                         * (entry.SpawnProbability / group.CumulativeProbability)
                         * entry.GetAmount(Random.Shared, getAverage: true);
            }
        }

        return price;
    }

    private void CleanupShipyard()
    {
        if (ShipyardMap == null || !_map.MapExists(ShipyardMap.Value))
        {
            ShipyardMap = null;
            _shuttleIndex = 0f; // HardLight
            return;
        }

        _map.DeleteMap(ShipyardMap.Value);
        ShipyardMap = null; // HardLight
        _shuttleIndex = 0f; // HardLight
    }

    public void SetupShipyardIfNeeded()
    {
        if (ShipyardMap != null && _map.MapExists(ShipyardMap.Value))
            return;

        var shipyardMapUid = _map.CreateMap(out var shipyardMap); // HardLight
        ShipyardMap = shipyardMap;
        _metaData.SetEntityName(shipyardMapUid, "Shipyard"); // HardLight

        _map.SetPaused(ShipyardMap.Value, false);
    }

    /// <summary>
    /// Cleans up the shipyard staging map once it no longer has any grids on it.
    /// </summary>
    private void QueueShipyardMapCleanupIfEmpty(int attempt = 0)
    {
        if (ShipyardMap == null || !_map.MapExists(ShipyardMap.Value))
        {
            ShipyardMap = null;
            _shuttleIndex = 0f;
            return;
        }

        var shipyardMap = ShipyardMap.Value;
        var gridQuery = EntityManager.AllEntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (gridQuery.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID == shipyardMap)
            {
                if (attempt >= 24)
                    return;

                Timer.Spawn(TimeSpan.FromSeconds(5), () => QueueShipyardMapCleanupIfEmpty(attempt + 1));
                return;
            }
        }

        CleanupShipyard();
    }

    // <summary>
    // Tries to rename a shuttle deed and update the respective components.
    // Returns true if successful.
    //
    // Null name parts are promptly ignored.
    // </summary>
    public bool TryRenameShuttle(EntityUid uid, ShuttleDeedComponent? shuttleDeed, string? newName, string? newSuffix)
    {
        if (!Resolve(uid, ref shuttleDeed))
            return false;

        var shuttle = shuttleDeed.ShuttleUid;
        if (shuttle != null
             && TryGetEntity(shuttle.Value, out var shuttleEntity)
             && _station.GetOwningStation(shuttleEntity.Value) is { Valid: true } shuttleStation)
        {
            shuttleDeed.ShuttleName = newName;
            shuttleDeed.ShuttleNameSuffix = newSuffix;
            Dirty(uid, shuttleDeed);

            var fullName = GetFullName(shuttleDeed);
            _station.RenameStation(shuttleStation, fullName, loud: false);
            _metaData.SetEntityName(shuttleEntity.Value, fullName);
            _metaData.SetEntityName(shuttleStation, fullName);
        }
        else
        {
            _sawmill.Error($"Could not rename shuttle {ToPrettyString(shuttle):entity} to {newName}");
            return false;
        }

        //TODO: move this to an event that others hook into.
        if (shuttleDeed.ShuttleUid != null &&
            _shuttleRecordsSystem.TryGetRecord(shuttleDeed.ShuttleUid.Value, out var record))
        {
            record.Name = newName ?? "";
            record.Suffix = newSuffix ?? "";
            _shuttleRecordsSystem.TryUpdateRecord(record);
        }

        return true;
    }

    /// <summary>
    /// Returns the full name of the shuttle component in the form of [prefix] [name] [suffix].
    /// </summary>
    public static string GetFullName(ShuttleDeedComponent comp)
    {
        string?[] parts = { comp.ShuttleName, comp.ShuttleNameSuffix };
        return string.Join(' ', parts.Where(it => it != null));
    }

    /// <summary>
    /// Attempts to extract ship name from YAML data
    /// </summary>
    private string? ExtractShipNameFromYaml(string yamlData)
    {
        try
        {
            // Simple YAML parsing to extract ship name
            var lines = yamlData.Split('\n');
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("shipName:"))
                {
                    var parts = trimmedLine.Split(':', 2);
                    if (parts.Length > 1)
                    {
                        return parts[1].Trim().Trim('"', '\'');
                    }
                }
                // Also check for entity names that might indicate ship name
                if (trimmedLine.StartsWith("name:"))
                {
                    var parts = trimmedLine.Split(':', 2);
                    if (parts.Length > 1)
                    {
                        var name = parts[1].Trim().Trim('"', '\'');
                        // Only use if it looks like a ship name (not generic component names)
                        if (!name.Contains("Component") && !name.Contains("System") && name.Length > 3)
                        {
                            return name;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _sawmill.Warning($"Failed to extract ship name from YAML: {ex}");
        }
        return null;
    }
}
