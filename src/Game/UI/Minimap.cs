using System.Collections.Generic;
using Godot;
using LumiFeast.Logic.Stages;

namespace LumiFeast.Game.UI;

/// <summary>Draws the room grid of a run; shared by the minimap and the full map screen.</summary>
public static class MapDrawing
{
    private static readonly Color Visited = new(0.95f, 0.92f, 0.85f, 0.9f);
    private static readonly Color Unknown = new(0.95f, 0.92f, 0.85f, 0.35f);
    private static readonly Color Here = new("f29bbd");
    private static readonly Color Cursor = new("ffe08a");

    public static (int MinX, int MinY, int Columns, int Rows) Bounds(StageMap map)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (var i = 0; i < map.Rooms.Count; i++)
        {
            var (x, y) = map.Cell(i);
            (minX, maxX, minY, maxY) = (Mathf.Min(minX, x), Mathf.Max(maxX, x), Mathf.Min(minY, y), Mathf.Max(maxY, y));
        }

        return (minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public static Vector2 Size(StageMap map, Vector2 cell, float gap)
    {
        var (_, _, columns, rows) = Bounds(map);
        return new Vector2(columns * (cell.X + gap) - gap, rows * (cell.Y + gap) - gap);
    }

    public static Rect2 CellRect(StageMap map, int index, Vector2 origin, Vector2 cell, float gap)
    {
        var (minX, minY, _, _) = Bounds(map);
        var (x, y) = map.Cell(index);
        return new Rect2(origin + new Vector2((x - minX) * (cell.X + gap), (y - minY) * (cell.Y + gap)), cell);
    }

    /// <param name="cursor">Room picked on the full map, or -1.</param>
    public static void Draw(CanvasItem canvas, StageMap map, IReadOnlyDictionary<int, Color> accents, Vector2 origin, Vector2 cell, float gap, float line, int cursor = -1)
    {
        for (var i = 0; i < map.Rooms.Count; i++)
        {
            if (!map.IsKnown(i))
            {
                continue;
            }

            var box = CellRect(map, i, origin, cell, gap);
            var state = map.State(i);
            if (i == map.Current)
            {
                canvas.DrawRect(box, Here);
            }
            else if (state.Visited)
            {
                canvas.DrawRect(box, new Color(Visited, 0.25f));
            }

            canvas.DrawRect(box, state.Visited ? Visited : Unknown, false, line);
            if (i == cursor)
            {
                canvas.DrawRect(box.Grow(line * 2), Cursor, false, line * 1.5f);
            }

            if (accents.TryGetValue(i, out var accent))
            {
                canvas.DrawCircle(box.GetCenter(), Mathf.Min(cell.X, cell.Y) * 0.22f, state.Visited ? accent : new Color(accent, 0.5f));
            }

            // links: a short bar across the gap to the east and below
            if (map.Neighbor(i, 1, 0) is var east && east >= 0 && map.IsKnown(east))
            {
                var y = box.GetCenter().Y;
                canvas.DrawLine(new Vector2(box.End.X, y), new Vector2(box.End.X + gap, y), Unknown, line);
            }

            if (map.Neighbor(i, 0, 1) is var below && below >= 0 && map.IsKnown(below))
            {
                var x = box.GetCenter().X;
                canvas.DrawLine(new Vector2(x, box.End.Y), new Vector2(x, box.End.Y + gap), Unknown, line);
            }
        }
    }
}

/// <summary>Top-right room map in the style of Little Noah: one box per room, the current one highlighted.</summary>
public partial class Minimap : CanvasLayer
{
    private static readonly Vector2 Cell = new(48, 32);
    private const float CellGap = 8;
    private const float Margin = 32;
    private const float PanelPadding = 12;
    private const float TitleHeight = 40;
    private const float Line = 3;

    private static readonly Color Panel = new(0.1f, 0.08f, 0.16f, 0.55f);

    private readonly MapCanvas _canvas = new();
    private Label _title = null!;

    public override void _Ready()
    {
        Layer = 11;
        _title = UiCommon.OutlinedLabel(26);
        AddChild(_canvas);
        AddChild(_title);
    }

    /// <param name="accents">Color per room index: boss, lair, chest, shrine; rooms without one are plain.</param>
    public void Show(StageMap map, string area, IReadOnlyDictionary<int, Color> accents)
    {
        var size = MapDrawing.Size(map, Cell, CellGap) + Vector2.One * PanelPadding * 2;
        var left = UiCommon.ScreenWidth - Margin - size.X;
        _title.Text = area;
        _title.Position = new Vector2(left, Margin - 6);
        _canvas.Position = new Vector2(left, Margin + TitleHeight);
        _canvas.Map = map;
        _canvas.PanelSize = size;
        _canvas.Accents = accents;
        _canvas.QueueRedraw();
    }

    private sealed partial class MapCanvas : Node2D
    {
        public StageMap? Map { get; set; }

        public Vector2 PanelSize { get; set; }

        public IReadOnlyDictionary<int, Color> Accents { get; set; } = new Dictionary<int, Color>();

        public override void _Draw()
        {
            if (Map is null)
            {
                return;
            }

            DrawRect(new Rect2(Vector2.Zero, PanelSize), Panel);
            MapDrawing.Draw(this, Map, Accents, Vector2.One * PanelPadding, Cell, CellGap, Line);
        }
    }
}
