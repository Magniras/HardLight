using System.Numerics;
using Content.Shared.Examine;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client.Popups;

/// <summary>
/// Draws popup text, either in world or on screen.
/// </summary>
public sealed class PopupOverlay : Overlay
{
    private const float PopupStackSpacing = 14f; // HardLight: Vertical spacing between stacked popups, in pixels.

    private readonly IConfigurationManager _configManager;
    private readonly IEntityManager _entManager;
    private readonly IPlayerManager _playerMgr;
    private readonly IUserInterfaceManager _uiManager;
    private readonly PopupSystem _popup;
    private readonly PopupUIController _controller;
    private readonly ExamineSystemShared _examine;
    private readonly SharedTransformSystem _transform;
    private readonly ShaderInstance _shader;
    private readonly Dictionary<(MapId mapId, EntityUid entity, int x, int y), int> _stackCounts = new(); // HardLight: Tracks how many popups are stacked at each position.

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public PopupOverlay(
        IConfigurationManager configManager,
        IEntityManager entManager,
        IPlayerManager playerMgr,
        IPrototypeManager protoManager,
        IUserInterfaceManager uiManager,
        PopupUIController controller,
        ExamineSystemShared examine,
        SharedTransformSystem transform,
        PopupSystem popup)
    {
        _configManager = configManager;
        _entManager = entManager;
        _playerMgr = playerMgr;
        _uiManager = uiManager;
        _examine = examine;
        _transform = transform;
        _popup = popup;
        _controller = controller;

        _shader = protoManager.Index(UnshadedShaderId).Instance();
    }

    private static readonly ProtoId<ShaderPrototype> UnshadedShaderId = "unshaded";

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        args.DrawingHandle.SetTransform(Matrix3x2.Identity);
        args.DrawingHandle.UseShader(_shader);
        var scale = _configManager.GetCVar(CVars.DisplayUIScale);

        if (scale == 0f)
            scale = _uiManager.DefaultUIScale;

        DrawWorld(args.ScreenHandle, args, scale);

        args.DrawingHandle.UseShader(null);
    }

    private void DrawWorld(DrawingHandleScreen worldHandle, OverlayDrawArgs args, float scale)
    {
        if (_popup.WorldLabels.Count == 0 || args.ViewportControl == null)
            return;

        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var ourEntity = _playerMgr.LocalEntity;
        var viewPos = new MapCoordinates(args.WorldAABB.Center, args.MapId);
        var ourPos = args.WorldBounds.Center;
        if (ourEntity != null)
        {
            viewPos = _transform.GetMapCoordinates(ourEntity.Value);
            ourPos = viewPos.Position;
        }

        _stackCounts.Clear();

        foreach (var popup in _popup.WorldLabels)
        {
            var mapPos = _transform.ToMapCoordinates(popup.InitialPos);

            if (mapPos.MapId != args.MapId)
                continue;

            var distance = (mapPos.Position - ourPos).Length();

            // Should handle fade here too wyci.
            if (!args.WorldBounds.Contains(mapPos.Position) || !_examine.InRangeUnOccluded(viewPos, mapPos, distance,
                    e => e == popup.InitialPos.EntityId || e == ourEntity, entMan: _entManager))
                continue;

            var pos = Vector2.Transform(mapPos.Position, matrix);

            // HardLight start: Calculate stacked position for world popups; prevents overlap when multiple popups spawn at the same position.
            var stackEntity = popup.InitialPos.EntityId;
            var stackX = (int) MathF.Round(mapPos.X * 10f);
            var stackY = (int) MathF.Round(mapPos.Y * 10f);
            var stackKey = (mapPos.MapId, stackEntity, stackX, stackY);

            var stackLevel = 0;
            if (_stackCounts.TryGetValue(stackKey, out var count))
                stackLevel = count;

            _stackCounts[stackKey] = stackLevel + 1;

            var stackedPos = pos - new Vector2(0f, stackLevel * PopupStackSpacing * scale);
            _controller.DrawPopup(popup, worldHandle, stackedPos, scale); // pos<stackedPos
            // HardLight end
        }
    }
}
