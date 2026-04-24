using Content.Shared.Actions.Events;
using Content.Shared.Hands.EntitySystems;

namespace Content.Server._HL.Actions;

public sealed class DuskEnclaveOnboardingSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DuskEnclaveOnboardingActionEvent>(OnDuskEnclaveOnboarding);
    }

    private void OnDuskEnclaveOnboarding(DuskEnclaveOnboardingActionEvent args)
    {
        if (args.Handled)
            return;

        var coords = Transform(args.Performer).Coordinates;
        var package = Spawn("DuskEnclaveOnboardingPackage", coords);
        _hands.TryPickupAnyHand(args.Performer, package);
        args.Handled = true;
    }
}
