using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.Administration;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client.Administration.UI.Tabs.AdminTab;

public sealed class SharedMacroWindow : DefaultWindow
{
    private readonly ItemList _macroList;
    private readonly Label _metaLabel;
    private readonly TextEdit _commandPreview;
    private readonly Button _refreshButton;
    private readonly Button _copySelectedToLocalButton;
    private readonly Button _copyLocalToSharedButton;
    private readonly ConfirmButton _deleteSharedButton;
    private readonly List<SharedAdminMacroState> _macros = new();

    public event Action? RefreshRequested;
    public event Action? CopySelectedToLocalRequested;
    public event Action? CopyLocalToSharedRequested;
    public event Action? DeleteSharedRequested;

    public SharedAdminMacroState? SelectedMacro
    {
        get
        {
            var selected = _macroList.GetSelected().FirstOrDefault();
            if (selected == null)
                return null;

            var index = _macroList.IndexOf(selected);
            return index >= 0 && index < _macros.Count ? _macros[index] : null;
        }
    }

    public SharedMacroWindow()
    {
        Title = Loc.GetString("admin-tools-shared-macros-window-title");
        MinSize = new Vector2(540, 340);
        SetSize = new Vector2(720, 460);

        _macroList = new ItemList
        {
            MinSize = new Vector2(220, 220),
            VerticalExpand = true,
            HorizontalExpand = true,
            SelectMode = ItemList.ItemListSelectMode.Single,
        };
        _macroList.OnItemSelected += _ => SyncSelectedMacro();

        _metaLabel = new Label
        {
            Text = Loc.GetString("admin-tools-shared-macros-empty"),
            StyleClasses = { "LabelSubText" },
        };

        _commandPreview = new TextEdit
        {
            Editable = false,
            MinHeight = 180,
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(4),
        };

        _refreshButton = new Button
        {
            Text = Loc.GetString("admin-tools-shared-macros-refresh"),
        };
        _refreshButton.OnPressed += _ => RefreshRequested?.Invoke();

        _copySelectedToLocalButton = new Button
        {
            Text = Loc.GetString("admin-tools-shared-macros-copy-to-local"),
            Disabled = true,
        };
        _copySelectedToLocalButton.OnPressed += _ => CopySelectedToLocalRequested?.Invoke();

        _copyLocalToSharedButton = new Button
        {
            Text = Loc.GetString("admin-tools-shared-macros-copy-local-to-shared"),
        };
        _copyLocalToSharedButton.OnPressed += _ => CopyLocalToSharedRequested?.Invoke();

        _deleteSharedButton = new ConfirmButton
        {
            Text = Loc.GetString("admin-tools-shared-macros-delete"),
            ConfirmationText = Loc.GetString("admin-player-actions-confirm"),
            Disabled = true,
        };
        _deleteSharedButton.OnPressed += _ => DeleteSharedRequested?.Invoke();

        var leftPanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            MinWidth = 240,
            HorizontalExpand = true,
        };
        leftPanel.AddChild(new Label { Text = Loc.GetString("admin-tools-shared-macros-list") });
        leftPanel.AddChild(_macroList);

        var previewPanel = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MinHeight = 180,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#14171A"),
                BorderThickness = new Thickness(1),
                BorderColor = Color.FromHex("#5A6572"),
            }
        };
        previewPanel.AddChild(_commandPreview);

        var buttonRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };
        buttonRow.AddChild(_copySelectedToLocalButton);
        buttonRow.AddChild(_copyLocalToSharedButton);
        buttonRow.AddChild(new Control { HorizontalExpand = true });
        buttonRow.AddChild(_refreshButton);
        buttonRow.AddChild(_deleteSharedButton);

        var rightPanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
            VerticalExpand = true,
            MinWidth = 320,
        };
        rightPanel.AddChild(new Label { Text = Loc.GetString("admin-tools-shared-macros-preview") });
        rightPanel.AddChild(_metaLabel);
        rightPanel.AddChild(previewPanel);
        rightPanel.AddChild(buttonRow);
        rightPanel.AddChild(new Label
        {
            Text = Loc.GetString("admin-tools-shared-macros-note"),
            StyleClasses = { "LabelSubText" },
        });

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(8),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(leftPanel);
        root.AddChild(rightPanel);

        Contents.AddChild(root);
    }

    public void UpdateMacros(IEnumerable<SharedAdminMacroState> macros)
    {
        var selectedName = SelectedMacro?.Name;
        var orderedMacros = macros.ToList();
        orderedMacros.Sort(static (left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

        _macros.Clear();
        _macroList.Clear();

        foreach (var macro in orderedMacros)
        {
            _macros.Add(macro);
            _macroList.AddItem(macro.Name);
        }

        if (_macros.Count == 0)
        {
            SyncSelectedMacro();
            return;
        }

        var preferredIndex = selectedName == null
            ? 0
            : _macros.FindIndex(macro => string.Equals(macro.Name, selectedName, StringComparison.OrdinalIgnoreCase));

        _macroList[Math.Max(preferredIndex, 0)].Selected = true;
        SyncSelectedMacro();
    }

    private void SyncSelectedMacro()
    {
        var selected = SelectedMacro;
        _copySelectedToLocalButton.Disabled = selected == null;
        _deleteSharedButton.Disabled = selected == null;

        if (selected == null)
        {
            _metaLabel.Text = Loc.GetString("admin-tools-shared-macros-empty");
            _commandPreview.TextRope = Rope.Leaf.Empty;
            return;
        }

        _metaLabel.Text = Loc.GetString("admin-tools-shared-macros-selected-meta", ("updatedBy", selected.UpdatedBy));
        _commandPreview.TextRope = new Rope.Leaf(selected.Command);
    }
}