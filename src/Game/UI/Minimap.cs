using System.Collections.Generic;
using Godot;
using LumiFeast.Logic.Stages;

namespace LumiFeast.Game.UI;

/// <summary>Top-right room map in the style of Little Noah: one box per room, the current one highlighted.</summary>
public partial class Minimap : CanvasLayer
{
    private const float CellWidth = 56;
    private const float CellHeight = 36;
    private const float CellGap = 8;
    private const float Margin = 32;
    private const float PanelPadding = 14;
    private const float TitleHeight = 40;
    private const float Line = 3;

    private static readonly Color Panel = new(0.1f, 0.08f, 0.16f, 0.55f);
    private static readonly Color Visited = new(0.95f, 0.92f, 0.85f, 0.9f);
    private static readonly Color Unknown = new(0.95f, 0.92f, 0.85f, 0.35f);
    private static readonly Color Here = new("f29bbd");

    private readonly MapCanvas _canvas = new();
    private Label _title = null!;

    public override void _Ready()
    {
        Layer = 11;
        _title = UiCommon.OutlinedLabel(26);
        AddChild(_canvas);
        AddChild(_title);
    }

    /// <param name="typeColors">Accent per room index: boss, lair, chest, shrine; rooms without one are plain.</param>
    public void Show(StageMap map, string area, IReadOnlyDictionary<int, Color> typeColors)
    {
        var width = map.Rooms.Count * (CellWidth + CellGap) - CellGap + PanelPadding * 2;
        var left = UiCommon.ScreenWidth - Margin - width;
        _title.Text = area;
        _title.Position = new Vector2(left, Margin - 6);
        _canvas.Position = new Vector2(left, Margin + TitleHeight);
        _canvas.Map = map;
        _canvas.Width = width;
        _canvas.TypeColors = typeColors;
        _canvas.QueueRedraw();
    }

    private sealed partial class MapCanvas : Node2D
    {
        public StageMap? Map { get; set; }

        public float Width { get; set; }

        public IReadOnlyDictionary<int, Color> TypeColors { get; set; } = new Dictionary<int, Color>();

        public override void _Draw()
        {
            if (Map is null)
            {
                return;
            }

            DrawRect(new Rect2(0, 0, Width, CellHeight + PanelPadding * 2), Panel);
            for (var i = 0; i < Map.Rooms.Count; i++)
            {
                if (!Map.IsKnown(i))
                {
                    continue;
                }

                var (x, _) = Map.Cell(i);
                var box = new Rect2(PanelPadding + x * (CellWidth + CellGap), PanelPadding, CellWidth, CellHeight);
                var state = Map.State(i);
                if (i == Map.Current)
                {
                    DrawRect(box, Here);
                }
                else if (state.Visited)
                {
                    DrawRect(box, new Color(Visited, 0.25f));
                }

                DrawRect(box, state.Visited ? Visited : Unknown, false, Line);
                if (TypeColors.TryGetValue(i, out var accent))
                {
                    DrawCircle(box.GetCenter(), CellHeight * 0.22f, state.Visited ? accent : new Color(accent, 0.5f));
                }

                // a link between rooms is a short bar across the gap
                if (Map.Neighbor(i, 1) is var east && east >= 0 && Map.IsKnown(east))
                {
                    var y = box.GetCenter().Y;
                    DrawLine(new Vector2(box.End.X, y), new Vector2(box.End.X + CellGap, y), Unknown, Line);
                }
            }
        }
    }
}
