using System;
using System.Collections.Generic;
using Godot;

namespace LumiFeast.Game.UI;

/// <summary>
/// Red arrows at the screen edge that point to monsters outside the view (Little Noah does the same), so a big room
/// never hides the last monster.
/// </summary>
public partial class OffscreenMarkers : CanvasLayer
{
    private const float EdgeInset = 46;
    private const float ArrowLength = 34;
    private const float ArrowHalfWidth = 18;
    private static readonly Color ArrowColor = new(1f, 0.25f, 0.25f, 0.9f);
    private static readonly Color ArrowOutline = new(0.2f, 0.02f, 0.05f, 0.9f);

    private readonly Arrows _arrows = new();

    /// <summary>World positions to point at; read every frame.</summary>
    public Func<IEnumerable<Vector2>> Targets { get; set; } = () => [];

    public override void _Ready()
    {
        Layer = 8;
        AddChild(_arrows);
        _arrows.Owner_ = this;
    }

    public override void _Process(double delta) => _arrows.QueueRedraw();

    private sealed partial class Arrows : Node2D
    {
        public OffscreenMarkers Owner_ { get; set; } = null!;

        public override void _Draw()
        {
            var view = new Rect2(Vector2.Zero, new Vector2(UiCommon.ScreenWidth, UiCommon.ScreenHeight));
            var toScreen = GetViewport().CanvasTransform;
            var center = view.GetCenter();
            foreach (var world in Owner_.Targets())
            {
                var screen = toScreen * world;
                if (view.HasPoint(screen))
                {
                    continue;
                }

                var edge = new Vector2(
                    Mathf.Clamp(screen.X, EdgeInset, view.Size.X - EdgeInset),
                    Mathf.Clamp(screen.Y, EdgeInset, view.Size.Y - EdgeInset));
                var direction = (screen - center).Normalized();
                var side = new Vector2(-direction.Y, direction.X) * ArrowHalfWidth;
                var tip = edge + direction * (ArrowLength / 2);
                var back = edge - direction * (ArrowLength / 2);
                Vector2[] points = [tip, back + side, back - side];
                DrawColoredPolygon(points, ArrowColor);
                DrawPolyline([tip, back + side, back - side, tip], ArrowOutline, 3);
            }
        }
    }
}
