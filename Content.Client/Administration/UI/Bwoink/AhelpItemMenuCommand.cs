using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Administration.Systems;
using Content.Shared.Administration;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Administration.UI.Bwoink
{
    /// <summary>
    ///     Opens an admin-only picker listing every entity prototype that matched a
    ///     clicked ahelp item mention keyword. Each row offers a Spawn button that
    ///     forwards the chosen prototype to the existing <c>ahelpspawnitem</c> flow.
    /// </summary>
    [AnyCommand]
    public sealed class AhelpItemMenuCommand : IConsoleCommand
    {
        public string Command => "ahelpitemmenu";
        public string Description => "Open a chooser for ahelp item mention candidates.";
        public string Help => "Usage: ahelpitemmenu <playerUserIdGuid> <normalizedKeyword>";

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

            var keyword = args[1].Trim();
            if (keyword.Length == 0)
            {
                shell.WriteError("Invalid keyword.");
                return;
            }

            if (!AhelpItemMentionRegistry.TryGet(keyword, out var prototypeIds) || prototypeIds.Count == 0)
            {
                shell.WriteError($"No item mention candidates registered for '{keyword}'.");
                return;
            }

            var target = new NetUserId(userGuid);
            var protoMan = IoCManager.Resolve<IPrototypeManager>();

            // If there is only one candidate, skip the chooser and go straight to
            // the existing single-item confirm prompt.
            if (prototypeIds.Count == 1)
            {
                OpenConfirm(target, prototypeIds[0], protoMan);
                return;
            }

            var window = new AhelpItemPickerWindow(target, prototypeIds, protoMan);
            window.OpenCentered();
        }

        internal static void OpenConfirm(NetUserId target, string prototypeId, IPrototypeManager protoMan)
        {
            var displayName = ResolveDisplayName(prototypeId, protoMan);

            var window = new TriageInfoWindow("Spawn Item Next To Player");
            window.AddMarkup(
                $"Spawn [color=goldenrod]{FormattedMessage.EscapeText(displayName)}[/color] " +
                $"([color=lightgray]{FormattedMessage.EscapeText(prototypeId)}[/color]) on a floor tile next to " +
                $"[color=white]{FormattedMessage.EscapeText(target.ToString())}[/color]?");
            window.AddActionButton("Spawn Item", () =>
            {
                var bwoink = IoCManager.Resolve<IEntityManager>().System<BwoinkSystem>();
                bwoink.RequestSpawnAhelpItemNearPlayer(target, prototypeId);
            }, closeOnPressed: true);
            window.OpenCentered();
        }

        internal static string ResolveDisplayName(string prototypeId, IPrototypeManager protoMan)
        {
            if (protoMan.TryIndex<EntityPrototype>(prototypeId, out var proto) && !string.IsNullOrWhiteSpace(proto.Name))
                return proto.Name;
            return prototypeId;
        }
    }

    /// <summary>
    ///     Scrollable chooser listing every candidate prototype for a clicked ahelp
    ///     item mention. Sorted so exact-name matches and shorter IDs surface first.
    /// </summary>
    internal sealed class AhelpItemPickerWindow : DefaultWindow
    {
        public AhelpItemPickerWindow(NetUserId target, IReadOnlyList<string> prototypeIds, IPrototypeManager protoMan)
        {
            Title = "Choose Item to Spawn";
            MinSize = new Vector2(520, 320);
            SetSize = new Vector2(640, 420);

            var rows = prototypeIds
                .Select(id =>
                {
                    var name = AhelpItemMenuCommand.ResolveDisplayName(id, protoMan);
                    return new { Id = id, Name = name };
                })
                .OrderBy(row => row.Name.ToLowerInvariant())
                .ThenBy(row => row.Id.ToLowerInvariant())
                .ToList();

            var list = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 4,
                VerticalExpand = true,
                HorizontalExpand = true,
            };

            foreach (var row in rows)
            {
                var line = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 6,
                    HorizontalExpand = true,
                };

                var nameLabel = new Label
                {
                    Text = row.Name,
                    HorizontalExpand = true,
                    ClipText = true,
                };

                var idLabel = new Label
                {
                    Text = row.Id,
                    StyleClasses = { "LabelSubText" },
                    ClipText = true,
                };

                var spawnButton = new Button
                {
                    Text = "Spawn",
                };

                var prototypeId = row.Id;
                spawnButton.OnPressed += _ =>
                {
                    var bwoink = IoCManager.Resolve<IEntityManager>().System<BwoinkSystem>();
                    bwoink.RequestSpawnAhelpItemNearPlayer(target, prototypeId);
                    Close();
                };

                line.AddChild(nameLabel);
                line.AddChild(idLabel);
                line.AddChild(spawnButton);
                list.AddChild(line);
            }

            var scroll = new ScrollContainer
            {
                VerticalExpand = true,
                HorizontalExpand = true,
                HScrollEnabled = false,
                VScrollEnabled = true,
            };
            scroll.AddChild(list);

            var header = new Label
            {
                Text = $"Pick an item to spawn next to {target}:",
            };

            var closeButton = new Button { Text = "Close", HorizontalAlignment = Control.HAlignment.Right };
            closeButton.OnPressed += _ => Close();

            var container = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 6,
                Margin = new Thickness(8),
            };
            container.AddChild(header);
            container.AddChild(scroll);
            container.AddChild(closeButton);

            Contents.AddChild(container);
        }
    }
}
