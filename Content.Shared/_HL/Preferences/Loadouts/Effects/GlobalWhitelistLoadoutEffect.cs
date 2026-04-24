using System.Diagnostics.CodeAnalysis;
using Content.Shared.Players;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences.Loadouts.Effects;

/// <summary>
/// Restricts a loadout item to players who are globally server-whitelisted.
/// </summary>
public sealed partial class GlobalWhitelistLoadoutEffect : LoadoutEffect
{
    public override bool Validate(
        HumanoidCharacterProfile profile,
        RoleLoadout loadout,
        ICommonSession? session,
        IDependencyCollection collection,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        // Allow server-side spawns (no session = admin/map spawn)
        if (session == null)
        {
            reason = null;
            return true;
        }

        if (session.ContentData()?.Whitelisted ?? false)
        {
            reason = null;
            return true;
        }

        reason = FormattedMessage.FromUnformatted(Loc.GetString("global-whitelist-loadout-invalid"));
        return false;
    }
}
