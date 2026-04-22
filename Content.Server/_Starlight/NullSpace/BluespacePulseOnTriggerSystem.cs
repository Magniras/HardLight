using Content.Server.Explosion.EntitySystems;
using Content.Shared._Starlight.NullSpace;
using Content.Shared._Starlight;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.NullSpace;

/// <summary>
/// Periodically scans for NullSpace entities within range and pulses to remove them + stun.
/// Uses Update-based detection instead of TriggerOnProximity because NullSpace entities
/// cancel all physics contacts, making physics-based proximity sensors blind to them.
/// </summary>
public sealed class BluespacePulseOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly SoundPathSpecifier NullSpaceCutoffSound = new("/Audio/_HL/Effects/ma cutoff.ogg");

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<BluespacePulseOnTriggerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (curTime < comp.NextTrigger)
                continue;

            // Collect NullSpace entities in range before modifying any components
            var found = new List<EntityUid>();
            foreach (var ent in _lookup.GetEntitiesInRange(uid, comp.Radius))
            {
                if (HasComp<NullSpaceComponent>(ent))
                    found.Add(ent);
            }

            if (found.Count == 0)
                continue;

            comp.NextTrigger = curTime + comp.Cooldown;

            // Fire effects (EmitSoundOnTrigger + SpawnOnTrigger)
            _trigger.Trigger(uid);

            // Remove NullSpace and stun all detected entities
            var stunTime = TimeSpan.FromSeconds(comp.StunSeconds);
            foreach (var ent in found)
            {
                if (HasComp<ShadekinComponent>(ent))
                    _audio.PlayPvs(NullSpaceCutoffSound, ent);
                RemComp<NullSpaceComponent>(ent);
                _stun.TryParalyze(ent, stunTime, true);
            }
        }
    }
}
