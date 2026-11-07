using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Game.Gameplay.Player;
using LumiFeast.Logic.Combat;
using LumiFeast.Logic.Common;

namespace LumiFeast.Game.UI;

/// <summary>HP, MP, combo counter and the arena wave.</summary>
public partial class PrototypeHud : CanvasLayer
{
    private PlayerController _player = null!;
    private ProgressBar _hp = null!;
    private ProgressBar _mp = null!;
    private Label _combo = null!;
    private Label _status = null!;
    private ProgressBar _bossBar = null!;
    private Health? _boss;

    public void Setup(PlayerController player)
    {
        _player = player;
        Layer = 10;
        var column = new VBoxContainer { Position = new Vector2(32, 28), CustomMinimumSize = new Vector2(340, 0) };
        AddChild(column);
        _hp = AddBar(column, new Color("e2506a"), player.Health.Max);
        _mp = AddBar(column, new Color("6c8cf5"), player.MaxMp);
        column.AddChild(new Label { Text = GameServices.Messages.Get(MessageKeys.DebugHelp) });

        _combo = new Label { Position = new Vector2(1600, 300), Visible = false };
        _combo.AddThemeFontSizeOverride("font_size", 48);
        _combo.AddThemeColorOverride("font_outline_color", Colors.Black);
        _combo.AddThemeConstantOverride("outline_size", 10);
        AddChild(_combo);

        _bossBar = new ProgressBar { ShowPercentage = false, Position = new Vector2(460, 980), CustomMinimumSize = new Vector2(1000, 24), Visible = false };
        _bossBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color("b45cc9") });
        AddChild(_bossBar);

        _status = new Label { Position = new Vector2(860, 40), Visible = false };
        _status.AddThemeFontSizeOverride("font_size", 42);
        _status.AddThemeColorOverride("font_outline_color", Colors.Black);
        _status.AddThemeConstantOverride("outline_size", 10);
        AddChild(_status);
    }

    /// <summary>Shows a boss health bar; null hides it.</summary>
    public void TrackBoss(Health? boss)
    {
        _boss = boss;
        _bossBar.Visible = boss is not null;
        if (boss is not null)
        {
            _bossBar.MaxValue = boss.Max;
        }
    }

    public void ShowStatus(string text)
    {
        _status.Text = text;
        _status.Visible = text.Length > 0;
    }

    public override void _Process(double delta)
    {
        _hp.Value = _player.Health.Current;
        _mp.Value = _player.Mp;
        _bossBar.Value = _boss?.Current ?? 0;
        _combo.Visible = _player.ComboHits > 1;
        _combo.Text = GameServices.Messages.Get(MessageKeys.HudCombo, _player.ComboHits);
    }

    private static ProgressBar AddBar(Container parent, Color color, double max)
    {
        var bar = new ProgressBar { MaxValue = max, ShowPercentage = false, CustomMinimumSize = new Vector2(340, 22) };
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = color });
        parent.AddChild(bar);
        return bar;
    }
}
