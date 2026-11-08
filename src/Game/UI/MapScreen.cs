using System.Collections.Generic;
using System.Linq;
using Godot;
using LumiFeast.Logic.Common;
using LumiFeast.Game.Common;
using LumiFeast.Logic.Stages;

namespace LumiFeast.Game.UI;

/// <summary>
/// Full map (Little Noah style): known rooms large, a legend, and warp to a healed room already visited.
/// Arrows move the cursor, attack warps, map or guard closes. The game is paused while it is open.
/// </summary>
public partial class MapScreen : CanvasLayer
{
    private static readonly Vector2 Cell = new(150, 96);
    private const float CellGap = 26;
    private const float Line = 5;
    private const float LegendDot = 12;
    private const float LegendSpacing = 270;

    private static readonly Color Dim = new(0.06f, 0.04f, 0.1f, 0.82f);
    private static readonly Color Parchment = new(0.93f, 0.84f, 0.66f, 0.12f);

    private readonly Canvas _canvas = new();
    private StageMap _map = null!;
    private IReadOnlyDictionary<int, Color> _accents = new Dictionary<int, Color>();
    private System.Func<int, bool> _warp = null!;
    private int _cursor;
    private ulong _openedFrame;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        Layer = 38;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;
        AddChild(new ColorRect { Color = Dim, Size = new Vector2(UiCommon.ScreenWidth, UiCommon.ScreenHeight) });
        AddChild(_canvas);
        _canvas.Screen = this;
    }

    /// <param name="legend">Name and color of each room type, in display order.</param>
    /// <param name="warp">Tries to warp to a room; false keeps the map open.</param>
    public void Open(StageMap map, string title, string hint, IReadOnlyDictionary<int, Color> accents, IReadOnlyList<(string Name, Color Color)> legend, System.Func<int, bool> warp)
    {
        _map = map;
        _accents = accents;
        _warp = warp;
        _cursor = map.Current;
        _canvas.Title = title;
        _canvas.Hint = hint;
        _canvas.Legend = legend;
        _openedFrame = Engine.GetProcessFrames();
        Visible = true;
        AudioService.Play(SoundIds.MapOpen);
        GetTree().Paused = true;
        _canvas.QueueRedraw();
    }

    public void Close()
    {
        Visible = false;
        GetTree().Paused = false;
    }

    public override void _Process(double delta)
    {
        if (!Visible || Engine.GetProcessFrames() == _openedFrame)
        {
            return;
        }

        if (Input.IsActionJustPressed(InputActions.Map) || Input.IsActionJustPressed(InputActions.Guard))
        {
            AudioService.Play(SoundIds.UiBack);
            Close();
            return;
        }

        if (Input.IsActionJustPressed(InputActions.AttackLight) && _cursor != _map.Current && _warp(_cursor))
        {
            AudioService.Play(SoundIds.UiConfirm);
            Close();
            return;
        }

        var step = Input.IsActionJustPressed(InputActions.MoveLeft) ? (-1, 0)
            : Input.IsActionJustPressed(InputActions.MoveRight) ? (1, 0)
            : Input.IsActionJustPressed(InputActions.Jump) ? (0, -1)
            : Input.IsActionJustPressed(InputActions.MoveDown) ? (0, 1)
            : (0, 0);
        if (step != (0, 0))
        {
            _cursor = Nearest(step);
            AudioService.Play(SoundIds.UiMove);
            _canvas.QueueRedraw();
        }
    }

    /// <summary>The closest visited room in that direction, or the cursor itself.</summary>
    private int Nearest((int X, int Y) step)
    {
        var (cx, cy) = _map.Cell(_cursor);
        return Enumerable.Range(0, _map.Rooms.Count)
            .Where(i => _map.State(i).Visited)
            .Select(i => (Index: i, Dx: _map.Cell(i).X - cx, Dy: _map.Cell(i).Y - cy))
            .Where(c => (step.X != 0 && Mathf.Sign(c.Dx) == step.X) || (step.Y != 0 && Mathf.Sign(c.Dy) == step.Y))
            .OrderBy(c => Mathf.Abs(c.Dx) + Mathf.Abs(c.Dy) * 2)
            .Select(c => c.Index)
            .DefaultIfEmpty(_cursor)
            .First();
    }

    private sealed partial class Canvas : Node2D
    {
        public MapScreen Screen { get; set; } = null!;

        public string Title { get; set; } = string.Empty;

        public string Hint { get; set; } = string.Empty;

        public IReadOnlyList<(string Name, Color Color)> Legend { get; set; } = [];

        public override void _Draw()
        {
            if (Screen._map is not { } map)
            {
                return;
            }

            var font = ThemeDB.FallbackFont;
            var size = MapDrawing.Size(map, Cell, CellGap);
            var origin = new Vector2((UiCommon.ScreenWidth - size.X) / 2, (UiCommon.ScreenHeight - size.Y) / 2);
            DrawRect(new Rect2(origin - Vector2.One * 60, size + Vector2.One * 120), Parchment);
            DrawString(font, new Vector2(origin.X - 60, origin.Y - 100), Title, HorizontalAlignment.Left, -1, 48);
            DrawString(font, new Vector2(origin.X - 60, origin.Y + size.Y + 120), Hint, HorizontalAlignment.Left, -1, 28);
            MapDrawing.Draw(this, map, Screen._accents, origin, Cell, CellGap, Line, Screen._cursor);

            // one row under the hint, centered
            var legendLeft = (UiCommon.ScreenWidth - Legend.Count * LegendSpacing) / 2;
            for (var i = 0; i < Legend.Count; i++)
            {
                var row = new Vector2(legendLeft + i * LegendSpacing, origin.Y + size.Y + 190);
                DrawCircle(row, LegendDot, Legend[i].Color);
                DrawString(font, row + new Vector2(28, 10), Legend[i].Name, HorizontalAlignment.Left, -1, 30);
            }
        }
    }
}
