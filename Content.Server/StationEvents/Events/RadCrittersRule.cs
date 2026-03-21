using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Content.Shared.Storage;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.StationEvents.Events;

public sealed class RadCrittersRule : StationEventSystem<RadCrittersRuleComponent>
{
    protected override void Started(EntityUid uid, RadCrittersRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var station))
            return;

        var locations = EntityQueryEnumerator<RadCritterSpawnLocationComponent, TransformComponent>();
        var validLocations = new List<(EntityCoordinates, float)>();

        while (locations.MoveNext(out _, out var location, out var transform))
        {
            if (CompOrNull<StationMemberComponent>(transform.GridUid)?.Station != station)
                continue;

            var entry = (transform.Coordinates, location.SpawnRange);
            validLocations.Add(entry);

            foreach (var spawn in EntitySpawnCollection.GetSpawns(component.Entries, RobustRandom))
            {
                Spawn(spawn, GetSpawnCoordinates(entry.Item1, entry.Item2));
            }
        }

        if (component.SpecialEntries.Count == 0 || validLocations.Count == 0)
            return;

        var specialEntry = RobustRandom.Pick(component.SpecialEntries);
        var specialSpawn = RobustRandom.Pick(validLocations);
        Spawn(specialEntry.PrototypeId, GetSpawnCoordinates(specialSpawn.Item1, specialSpawn.Item2));

        foreach (var location in validLocations)
        {
            foreach (var spawn in EntitySpawnCollection.GetSpawns(component.SpecialEntries, RobustRandom))
            {
                Spawn(spawn, GetSpawnCoordinates(location.Item1, location.Item2));
            }
        }
    }

    private EntityCoordinates GetSpawnCoordinates(EntityCoordinates coordinates, float range)
    {
        if (range <= 0f)
            return coordinates;

        return coordinates.Offset(RobustRandom.NextVector2(range));
    }
}