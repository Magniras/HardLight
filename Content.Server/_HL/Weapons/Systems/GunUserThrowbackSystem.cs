using Content.Shared._HL.Weapons.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Throwing;

namespace Content.Server._HL.Weapons.Systems;

public sealed class GunUserThrowbackSystem : EntitySystem
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunUserThrowbackComponent, AmmoShotEvent>(OnAmmoShot);
    }

    private void OnAmmoShot(Entity<GunUserThrowbackComponent> ent, ref AmmoShotEvent args)
    {
        if (args.Shooter is not { } shooter || ent.Comp.Strength <= 0f)
            return;

        if (!TryComp<GunComponent>(ent, out var gun) || gun.ShootCoordinates is not { } shootCoordinates)
            return;

        var shooterCoords = _transform.GetMapCoordinates(shooter);
        var targetCoords = _transform.ToMapCoordinates(shootCoordinates);

        if (shooterCoords.MapId != targetCoords.MapId)
            return;

        var recoilDirection = shooterCoords.Position - targetCoords.Position;
        if (recoilDirection.LengthSquared() <= 0f)
            return;

        _throwing.TryThrow(shooter, recoilDirection, ent.Comp.Strength, user: shooter, compensateFriction: ent.Comp.CompensateFriction);
    }
}