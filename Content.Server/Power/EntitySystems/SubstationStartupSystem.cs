using Content.Server.Power.Components;
using Content.Server.NodeContainer.EntitySystems; // HardLight
using Content.Server.Power.Nodes; // HardLight
using Content.Shared.NodeContainer; // HardLight
using Content.Shared.Power;

namespace Content.Server.Power.EntitySystems;

/// <summary>
/// Resets substation battery/power network state on map load to avoid stale map data.
/// </summary>
public sealed class SubstationStartupSystem : EntitySystem
{
    [Dependency] private readonly BatterySystem _batterySystem = default!;
    [Dependency] private readonly NodeGroupSystem _nodeGroup = default!; // HardLight

    private readonly HashSet<EntityUid> _pendingNetRebind = new(); // HardLight

    private static readonly HashSet<string> FullChargeSubstations = new()
    {
        "SubstationBasic",
        "SubstationWallBasic"
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PowerMonitoringDeviceComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PowerMonitoringDeviceComponent, ComponentInit>(OnComponentInit); // HardLight
    }

    // HardLight: Queue an early net rebind for generated/post-map-init substations that may skip MapInitEvent.
    private void OnComponentInit(EntityUid uid, PowerMonitoringDeviceComponent component, ComponentInit args)
    {
        if (component.Group != PowerMonitoringConsoleGroup.Substation)
            return;

        QueueNetRebind(uid, component);
    }

    // HardLight: Defer charger/discharger rebind until both source/load cable nodes have built node groups.
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingNetRebind.Count == 0)
            return;

        var completed = new List<EntityUid>();
        foreach (var uid in _pendingNetRebind)
        {
            if (Deleted(uid) || !TryComp<PowerMonitoringDeviceComponent>(uid, out var monitor))
            {
                completed.Add(uid);
                continue;
            }

            if (!TryComp<NodeContainerComponent>(uid, out var nodeContainer))
                continue;

            var sourceReady = TryGetCableNode(nodeContainer, monitor.SourceNode)?.NodeGroup != null;
            var loadReady = TryGetCableNode(nodeContainer, monitor.LoadNode)?.NodeGroup != null;

            if (!sourceReady || !loadReady)
                continue;

            if (TryComp(uid, out BatteryChargerComponent? charger))
            {
                charger.ClearNet();
                charger.TryFindAndSetNet();
            }

            if (TryComp(uid, out BatteryDischargerComponent? discharger))
            {
                discharger.ClearNet();
                discharger.TryFindAndSetNet();
            }

            completed.Add(uid);
        }

        foreach (var uid in completed)
        {
            _pendingNetRebind.Remove(uid);
        }
    }

    private void OnMapInit(EntityUid uid, PowerMonitoringDeviceComponent component, MapInitEvent args)
    {
        if (component.Group != PowerMonitoringConsoleGroup.Substation)
            return;

        QueueNetRebind(uid, component); // HardLight

        if (!TryComp(uid, out BatteryComponent? battery))
            return;

        var protoId = MetaData(uid).EntityPrototype?.ID;
        if (protoId == null)
            return;

        if (protoId == "SubstationBasicEmpty")
        {
            if (battery.CurrentCharge > 0)
                _batterySystem.SetCharge(uid, 0, battery);
        }
        else if (FullChargeSubstations.Contains(protoId))
        {
            if (battery.CurrentCharge <= 0)
                _batterySystem.SetCharge(uid, battery.MaxCharge, battery);
        }

        if (TryComp(uid, out PowerNetworkBatteryComponent? netBattery))
        {
            netBattery.LoadingNetworkDemand = 0f;
            netBattery.CurrentSupply = 0f;
            netBattery.CurrentReceiving = 0f;
            netBattery.SupplyRampPosition = 0f;
            netBattery.LastSupply = 0f;
        }
    }

    // HardLight: Re-enable/reflood substation cable device nodes, clear stale net refs, then queue deferred connector rebind.
    private void QueueNetRebind(EntityUid uid, PowerMonitoringDeviceComponent component)
    {
        if (TryComp<NodeContainerComponent>(uid, out var nodeContainer))
        {
            EnsureActiveCableNode(nodeContainer, component.SourceNode);
            EnsureActiveCableNode(nodeContainer, component.LoadNode);

            if (TryGetCableNode(nodeContainer, component.SourceNode) is { } source)
                _nodeGroup.QueueReflood(source);

            if (TryGetCableNode(nodeContainer, component.LoadNode) is { } load)
                _nodeGroup.QueueReflood(load);
        }

        if (TryComp(uid, out BatteryChargerComponent? charger))
            charger.ClearNet();

        if (TryComp(uid, out BatteryDischargerComponent? discharger))
            discharger.ClearNet();

        _pendingNetRebind.Add(uid);
    }

    // HardLight: Legacy maps can serialize disabled cable device nodes; force-enable before reflood/rebind.
    private void EnsureActiveCableNode(NodeContainerComponent container, string nodeId)
    {
        if (TryGetCableNode(container, nodeId) is not { } node)
            return;

        node.Enabled = true;
    }

    // HardLight: Helper to fetch a named cable device node from the node container.
    private CableDeviceNode? TryGetCableNode(NodeContainerComponent container, string nodeId)
    {
        if (!container.Nodes.TryGetValue(nodeId, out var node))
            return null;

        return node as CableDeviceNode;
    }
}
