using System;
using System.Numerics;
using Content.Shared.Administration;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.Administration.UI.Bwoink
{
    /// <summary>
    ///     Popup window that lets an admin pick a ship from a server-provided list
    ///     and assign its deed to the target player's held or stored ID card / PDA.
    /// </summary>
    public sealed class AssignDeedWindow : DefaultWindow
    {
        private readonly ItemList _shipList;
        private readonly RichTextLabel _previewLabel;
        private readonly Button _assignButton;
        private readonly RichTextLabel _statusLabel;

        private ShipDeedEntry[] _ships = Array.Empty<ShipDeedEntry>();

        /// <summary>Index into <see cref="_ships"/> for the currently selected row, or -1.</summary>
        public int SelectedIndex { get; private set; } = -1;

        public ShipDeedEntry? SelectedShip => SelectedIndex >= 0 && SelectedIndex < _ships.Length
            ? _ships[SelectedIndex]
            : null;

        public event Action? RefreshRequested;
        public event Action<ShipDeedEntry>? AssignRequested;

        public AssignDeedWindow(string playerName)
        {
            Title = Loc.GetString("bwoink-assign-deed-window-title", ("name", playerName));
            MinSize = new Vector2(520, 380);
            SetSize = new Vector2(560, 420);

            var root = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 6,
                Margin = new Thickness(4),
            };

            // ── top bar ──────────────────────────────────────────────────────────────
            var topBar = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 4,
            };

            var headerLabel = new RichTextLabel();
            headerLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(
                Loc.GetString("bwoink-assign-deed-header", ("name", playerName))));
            headerLabel.HorizontalExpand = true;
            headerLabel.VerticalAlignment = VAlignment.Center;
            topBar.AddChild(headerLabel);

            var refreshButton = new Button
            {
                Text = Loc.GetString("bwoink-assign-deed-refresh"),
            };
            refreshButton.OnPressed += _ => RefreshRequested?.Invoke();
            topBar.AddChild(refreshButton);

            root.AddChild(topBar);

            // ── main split: list left, preview right ──────────────────────────────────
            var body = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 6,
                VerticalExpand = true,
            };

            _shipList = new ItemList
            {
                MinWidth = 280,
                HorizontalExpand = true,
                VerticalExpand = true,
                SelectMode = ItemList.ItemListSelectMode.Single,
            };
            _shipList.OnItemSelected += OnShipSelected;
            _shipList.OnItemDeselected += _ =>
            {
                SelectedIndex = -1;
                UpdatePreview();
                if (_assignButton != null) _assignButton.Disabled = true;
            };
            body.AddChild(_shipList);

            var previewPanel = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 4,
                MinWidth = 200,
            };

            previewPanel.AddChild(new Label
            {
                Text = Loc.GetString("bwoink-assign-deed-preview-header"),
                StyleClasses = { "LabelSmall" },
            });

            _previewLabel = new RichTextLabel
            {
                VerticalExpand = true,
            };
            UpdatePreview();
            previewPanel.AddChild(_previewLabel);

            body.AddChild(previewPanel);
            root.AddChild(body);

            // ── bottom bar ────────────────────────────────────────────────────────────
            var bottomBar = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 4,
            };

            _statusLabel = new RichTextLabel
            {
                HorizontalExpand = true,
                VerticalAlignment = VAlignment.Center,
            };
            bottomBar.AddChild(_statusLabel);

            _assignButton = new Button
            {
                Text = Loc.GetString("bwoink-assign-deed-assign"),
                Disabled = true,
                StyleClasses = { "Caution" },
            };
            _assignButton.OnPressed += _ =>
            {
                if (SelectedShip is { } ship)
                    AssignRequested?.Invoke(ship);
            };
            bottomBar.AddChild(_assignButton);

            root.AddChild(bottomBar);

            Contents.AddChild(root);
        }

        /// <summary>
        ///     Replace the ship list content. Called whenever the server sends an updated list.
        /// </summary>
        public void UpdateShipList(ShipDeedEntry[] ships)
        {
            var previousSelection = SelectedShip?.ShipNetEntity;
            _ships = ships;
            _shipList.Clear();

            foreach (var ship in ships)
            {
                var owner = string.IsNullOrWhiteSpace(ship.OwnerName)
                    ? Loc.GetString("bwoink-assign-deed-no-owner")
                    : ship.OwnerName;
                _shipList.AddItem($"{ship.ShipName}  [color=#888](by {owner})[/color]");
            }

            // Try to restore selection.
            SelectedIndex = -1;
            if (previousSelection.HasValue)
            {
                for (var i = 0; i < _ships.Length; i++)
                {
                    if (_ships[i].ShipNetEntity == previousSelection.Value)
                    {
                        _shipList[i].Selected = true;
                        SelectedIndex = i;
                        break;
                    }
                }
            }

            UpdatePreview();
            _assignButton.Disabled = SelectedIndex < 0;
            SetStatus(string.Empty);
        }

        /// <summary>Show a status message (e.g. success or error) at the bottom of the window.</summary>
        public void ShowStatus(bool success, string message)
        {
            var color = success ? "lightgreen" : "salmon";
            var msg = new FormattedMessage();
            msg.PushColor(Color.FromHex(success ? "#90EE90" : "#FA8072"));
            msg.AddText(message);
            msg.Pop();
            _statusLabel.SetMessage(msg);
        }

        private void OnShipSelected(ItemList.ItemListSelectedEventArgs args)
        {
            SelectedIndex = args.ItemIndex;
            UpdatePreview();
            _assignButton.Disabled = false;
        }

        private void UpdatePreview()
        {
            var msg = new FormattedMessage();
            if (SelectedShip is { } ship)
            {
                msg.PushColor(Color.LightGray);
                msg.AddText(Loc.GetString("bwoink-assign-deed-preview-name"));
                msg.Pop();
                msg.AddText(" ");
                msg.PushColor(Color.White);
                msg.AddText(ship.ShipName);
                msg.Pop();
                msg.AddText("\n");

                msg.PushColor(Color.LightGray);
                msg.AddText(Loc.GetString("bwoink-assign-deed-preview-owner"));
                msg.Pop();
                msg.AddText(" ");
                msg.PushColor(Color.White);
                msg.AddText(string.IsNullOrWhiteSpace(ship.OwnerName)
                    ? Loc.GetString("bwoink-assign-deed-no-owner")
                    : ship.OwnerName);
                msg.Pop();
                msg.AddText("\n");

                if (!string.IsNullOrWhiteSpace(ship.OwnerUserId))
                {
                    msg.PushColor(Color.LightGray);
                    msg.AddText(Loc.GetString("bwoink-assign-deed-preview-owner-id"));
                    msg.Pop();
                    msg.AddText(" ");
                    msg.PushColor(Color.Gray);
                    msg.AddText(ship.OwnerUserId);
                    msg.Pop();
                }
            }
            else
            {
                msg.PushColor(Color.Gray);
                msg.AddText(Loc.GetString("bwoink-assign-deed-no-selection"));
                msg.Pop();
            }

            _previewLabel.SetMessage(msg);
        }

        private void SetStatus(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _statusLabel.SetMessage(FormattedMessage.Empty);
                return;
            }

            _statusLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(text));
        }
    }
}
