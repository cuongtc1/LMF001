using System.Collections.Generic;
using Godot;
using LumiFeast.Game.Common;

namespace LumiFeast.Game.UI;

/// <summary>End of a run, lost or cleared: what happened, then attack starts a new run.</summary>
public partial class RunResultScreen : CanvasLayer
{
    /// <summary>Ignores attack for a moment, so a player still mashing Z does not skip the screen.</summary>
    private const double InputDelaySeconds = 0.8;
    private const float PanelWidth = 1000;
    private const float PanelTop = 230;

    private static readonly Color Dim = new(0.08f, 0.05f, 0.12f, 0.55f);
    private static readonly Color PanelColor = new(0.14f, 0.1f, 0.22f, 0.92f);
    private static readonly Color ClearColor = new("ffd36b");
    private static readonly Color DefeatColor = new("f2a7c3");
    private static readonly Color HintColor = new("c9c2e0");

    private System.Action _onClose = null!;
    private double _wait;

    public override void _Ready()
    {
        Layer = 40;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;
    }

    /// <param name="lines">Stat lines under the title.</param>
    public void Open(bool cleared, string title, string line, IReadOnlyList<string> lines, string prompt, System.Action onClose)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _onClose = onClose;
        _wait = InputDelaySeconds;
        AddChild(new ColorRect { Color = Dim, Size = new Vector2(UiCommon.ScreenWidth, UiCommon.ScreenHeight) });
        var panel = new PanelContainer { Position = new Vector2((UiCommon.ScreenWidth - PanelWidth) / 2, PanelTop), CustomMinimumSize = new Vector2(PanelWidth, 0) };
        panel.AddThemeStyleboxOverride("panel", UiCommon.RoundPanel(PanelColor, 40));
        AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);
        panel.AddChild(column);

        var heading = UiCommon.OutlinedLabel(76, title);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        heading.AddThemeColorOverride("font_color", cleared ? ClearColor : DefeatColor);
        column.AddChild(heading);
        var story = UiCommon.OutlinedLabel(28, line);
        story.HorizontalAlignment = HorizontalAlignment.Center;
        story.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(story);
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 16) });
        foreach (var text in lines)
        {
            var stat = UiCommon.OutlinedLabel(32, text);
            stat.HorizontalAlignment = HorizontalAlignment.Center;
            stat.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            column.AddChild(stat);
        }

        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 24) });
        var hint = UiCommon.OutlinedLabel(36, prompt);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.AddThemeColorOverride("font_color", HintColor);
        column.AddChild(hint);
        Visible = true;
    }

    public void Close()
    {
        Visible = false;
        _onClose();
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _wait -= delta;
        if (_wait <= 0 && Input.IsActionJustPressed(InputActions.AttackLight))
        {
            Close();
        }
    }
}
