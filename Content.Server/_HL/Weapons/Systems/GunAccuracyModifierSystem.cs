using Content.Shared._HL.Weapons.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Maths;

namespace Content.Server._HL.Weapons.Systems;

public sealed class GunAccuracyModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunAccuracyModifierComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
    }

    private void OnGunRefreshModifiers(Entity<GunAccuracyModifierComponent> ent, ref GunRefreshModifiersEvent args)
    {
        var spreadMultiplier = ent.Comp.SpreadMultiplier;
        if (spreadMultiplier <= 0f)
            return;

        if (!MathHelper.CloseTo(spreadMultiplier, 1f))
        {
            args.AngleIncrease *= spreadMultiplier;
            args.MaxAngle *= spreadMultiplier;
            args.MinAngle *= spreadMultiplier;
            args.AngleDecay /= spreadMultiplier;
        }

        args.MaxAngle += ent.Comp.MaxAngleOffset;
        args.MinAngle += ent.Comp.MinAngleOffset;

        if (args.MinAngle > args.MaxAngle)
            args.MinAngle = args.MaxAngle;

        if (args.MinAngle < Angle.Zero)
            args.MinAngle = Angle.Zero;

        if (args.MaxAngle < Angle.Zero)
            args.MaxAngle = Angle.Zero;
    }
}