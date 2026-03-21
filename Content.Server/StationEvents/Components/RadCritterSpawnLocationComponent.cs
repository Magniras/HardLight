using Content.Server.StationEvents.Events;

namespace Content.Server.StationEvents.Components;

[RegisterComponent, Access(typeof(RadCrittersRule))]
public sealed partial class RadCritterSpawnLocationComponent : Component
{
    [DataField("spawnRange")]
    public float SpawnRange = 0.75f;
}