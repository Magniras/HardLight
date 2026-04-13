using System.Numerics;
using Content.Client.Gameplay;
using Content.Shared.Popups;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.Popups;

/// <summary>
/// Handles screens-space popups. World popups are handled via PopupOverlay.
/// </summary>
public sealed class PopupUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [UISystemDependency] private readonly PopupSystem? _popup = default!;

    private Font _smallFont = default!;
    private Font _mediumFont = default!;
    private Font _largeFont = default!;

    private PopupRootControl? _popupControl;

    public override void Initialize()
    {
        base.Initialize();
        var cache = IoCManager.Resolve<IResourceCache>();

        _smallFont = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Italic.ttf"), 10);
        _mediumFont = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Italic.ttf"), 12);
        _largeFont = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-BoldItalic.ttf"), 14);
    }

    public void OnStateEntered(GameplayState state)
    {
        _popupControl = new PopupRootControl(_popup, this);

        UIManager.RootControl.AddChild(_popupControl);
    }

    public void OnStateExited(GameplayState state)
    {
        if (_popupControl == null)
            return;

        UIManager.RootControl.RemoveChild(_popupControl);
        _popupControl = null;
    }

    public void DrawPopup(PopupSystem.PopupLabel popup, DrawingHandleScreen handle, Vector2 position, float scale)
    {
        var lifetime = PopupSystem.GetPopupLifetime(popup);

        // Keep alpha at 1 until TotalTime passes half its lifetime, then gradually decrease to 0.
        var alpha = MathF.Min(1f, 1f - MathF.Max(0f, popup.TotalTime - lifetime / 2) * 2 / lifetime);

        var updatedPosition = position - new Vector2(0f, MathF.Min(8f, 12f * (popup.TotalTime * popup.TotalTime + popup.TotalTime)));
        var font = _smallFont;
        var color = Color.White.WithAlpha(alpha);

        switch (popup.Type)
        {
            case PopupType.SmallCaution:
                color = Color.Red;
                break;
            case PopupType.Medium:
                font = _mediumFont;
                color = Color.LightGray;
                break;
            case PopupType.MediumCaution:
                font = _mediumFont;
                color = Color.Red;
                break;
            case PopupType.Large:
                font = _largeFont;
                color = Color.LightGray;
                break;
            case PopupType.LargeCaution:
                font = _largeFont;
                color = Color.Red;
                break;
        }

        var dimensions = handle.GetDimensions(font, popup.Text, scale);
        handle.DrawString(font, updatedPosition - dimensions / 2f, popup.Text, scale, color.WithAlpha(alpha));
    }

    /// <summary>
    /// Handles drawing all screen popups.
    /// </summary>
    private sealed class PopupRootControl : Control
    {
        private readonly PopupSystem? _popup;
        private readonly PopupUIController _controller;
        private readonly Dictionary<(int x, int y), int> _stackCounts = new(); // HardLight: Tracks how many popups are stacked at each position.

        public PopupRootControl(PopupSystem? system, PopupUIController controller)
        {
            _popup = system;
            _controller = controller;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            if (_popup == null)
                return;

            // Different window
            var windowId = UserInterfaceManager.RootControl.Window.Id;
            var stackSpacing = 14f * UIScale;

            _stackCounts.Clear();

            foreach (var popup in _popup.CursorLabels)
            {
                if (popup.InitialPos.Window != windowId)
                    continue;

                // HardLight start: Calculate stacked position for cursor popups; prevents overlap when multiple popups spawn at the same position.
                var stackX = (int) MathF.Round(popup.InitialPos.Position.X);
                var stackY = (int) MathF.Round(popup.InitialPos.Position.Y);
                var stackKey = (stackX, stackY);

                var stackLevel = 0;
                if (_stackCounts.TryGetValue(stackKey, out var count))
                    stackLevel = count;

                _stackCounts[stackKey] = stackLevel + 1;

                var stackedPos = popup.InitialPos.Position - new Vector2(0f, stackLevel * stackSpacing);
                _controller.DrawPopup(popup, handle, stackedPos, UIScale); // popup.InitialPos.Position<stackedPos
                // HardLight end
            }
        }
    }
}
