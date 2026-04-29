using Content.Client.Administration.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Client.Administration.UI.Bwoink;

[AnyCommand]
public sealed class AhelpSpawnItemCommand : IConsoleCommand
{
    public string Command => "ahelpspawnitem";
    public string Description => "Prompt to spawn an item next to the active ahelp player.";
    public string Help => "Usage: ahelpspawnitem <playerUserIdGuid> <prototypeId>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Help);
            return;
        }

        if (!Guid.TryParse(args[0], out var userGuid))
        {
            shell.WriteError("Invalid user id.");
            return;
        }

        var prototypeId = args[1].Trim();
        if (prototypeId.Length == 0)
        {
            shell.WriteError("Invalid prototype id.");
            return;
        }

        var target = new NetUserId(userGuid);
        var window = new TriageInfoWindow("Spawn Item Next To Player");
        window.AddMarkup($"Spawn [color=goldenrod]{FormattedMessage.EscapeText(prototypeId)}[/color] on a floor tile next to [color=white]{FormattedMessage.EscapeText(target.ToString())}[/color]?");
        window.AddActionButton("Spawn Item", () =>
        {
            var bwoink = IoCManager.Resolve<IEntityManager>().System<BwoinkSystem>();
            bwoink.RequestSpawnAhelpItemNearPlayer(target, prototypeId);
        }, closeOnPressed: true);
        window.OpenCentered();
    }
}
