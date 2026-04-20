using Content.Client._Common.Consent;
using Content.Shared._Common.Consent;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Overlays;

public sealed class ShowNonconIconsSystem : EntitySystem
{
    [Dependency] private readonly IClientConsentManager _consentManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private static readonly ProtoId<ConsentTogglePrototype> NonconConsentToggle = "NonconIcon";
    private static readonly ProtoId<SecurityIconPrototype> NonconStatusIcon = "NonconIcon";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConsentComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    private void OnGetStatusIconsEvent(Entity<ConsentComponent> ent, ref GetStatusIconsEvent ev)
    {
        if (!_consentManager.HasLoaded)
            return;

        // Mutual opt-in: the local viewer must want to see the icon, and the target must want to display it.
        if (!_consentManager.GetConsentSettings().Toggles.ContainsKey(NonconConsentToggle) ||
            !ent.Comp.ConsentSettings.Toggles.ContainsKey(NonconConsentToggle))
        {
            return;
        }

        if (_prototype.TryIndex<SecurityIconPrototype>(NonconStatusIcon, out var iconPrototype))
            ev.StatusIcons.Add(iconPrototype);
    }
}