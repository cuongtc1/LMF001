using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Game.Gameplay.Player;
using LumiFeast.Logic.Common;

namespace LumiFeast.Game.Systems.Debug;

/// <summary>F1 panel for testing: difficulty, tier, god mode, double jump, heal, respawn, reload data.</summary>
public partial class DebugPanel : CanvasLayer
{
    private PlayerController _player = null!;
    private System.Action _respawn = null!;
    private System.Action _goToArena = null!;
    private PanelContainer _panel = null!;
    private Button _difficulty = null!;
    private Button _tier = null!;
    private Button _level = null!;

    /// <summary>Quick steps for the level buttons (1 to 99 one by one is too slow).</summary>
    private static readonly int[] LevelSteps = [1, 2, 3, 5, 8, 10, 15, 20, 30, 40, 60, 80, 99];
    private Button _god = null!;
    private Button _airJump = null!;
    private int _stageTier;

    public void Setup(PlayerController player, int stageTier, System.Action respawn, System.Action goToArena)
    {
        _player = player;
        _stageTier = stageTier;
        _respawn = respawn;
        _goToArena = goToArena;
        Layer = 20;
        _panel = new PanelContainer { Visible = false, Position = new Vector2(1500, 24) };
        var column = new VBoxContainer();
        _panel.AddChild(column);
        AddChild(_panel);

        var messages = GameServices.Messages;
        column.AddChild(new Label { Text = messages.Get(MessageKeys.DebugTitle) });
        _difficulty = AddButton(column, string.Empty, NextDifficulty);
        _tier = AddButton(column, string.Empty, NextTier);
        _level = AddButton(column, string.Empty, NextPlayerLevel);
        _god = AddButton(column, string.Empty, ToggleGod);
        _airJump = AddButton(column, string.Empty, ToggleAirJump);
        AddButton(column, messages.Get(MessageKeys.DebugHeal), _player.Heal);
        AddButton(column, messages.Get(MessageKeys.DebugRespawn), () => _respawn());
        AddButton(column, messages.Get(MessageKeys.DebugArena), () => _goToArena());
        AddButton(column, messages.Get(MessageKeys.DebugPrototype), () => GetTree().ChangeSceneToFile(GameConstants.PrototypeScene));
        AddButton(column, messages.Get(MessageKeys.DebugReload), Reload);
        Refresh();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input.IsActionPressed(InputActions.DebugPanel))
        {
            _panel.Visible = !_panel.Visible;
        }
        else if (input.IsActionPressed(InputActions.DebugReload))
        {
            Reload();
        }
    }

    private static Button AddButton(Container parent, string text, System.Action action)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private int CurrentTier => GameServices.LevelOverride ?? _stageTier;

    private void NextDifficulty()
    {
        var modes = GameServices.Data.Difficulties.Modes;
        for (var i = 0; i < modes.Count; i++)
        {
            if (modes[i].Id == GameServices.DifficultyId)
            {
                GameServices.DifficultyId = modes[(i + 1) % modes.Count].Id;
                break;
            }
        }

        Refresh();
        _respawn();
    }

    private void NextTier()
    {
        GameServices.LevelOverride = NextStep(CurrentTier);
        Refresh();
        _respawn();
    }

    private void NextPlayerLevel()
    {
        GameServices.PlayerLevel = NextStep(GameServices.PlayerLevel);
        Refresh();
        _respawn();
    }

    private static int NextStep(int level) => System.Array.Find(LevelSteps, step => step > level) is var next && next > 0 ? next : LevelSteps[0];

    private void ToggleGod()
    {
        GameServices.GodMode = !GameServices.GodMode;
        Refresh();
    }

    private void ToggleAirJump()
    {
        GameServices.AirJumpUnlocked = !GameServices.AirJumpUnlocked;
        Refresh();
    }

    private void Refresh()
    {
        var messages = GameServices.Messages;
        _difficulty.Text = messages.Get(MessageKeys.DebugDifficulty, GameServices.DifficultyId);
        _tier.Text = messages.Get(MessageKeys.DebugTier, CurrentTier);
        _level.Text = messages.Get(MessageKeys.DebugPlayerLevel, GameServices.PlayerLevel, _player.Level);
        _god.Text = messages.Get(MessageKeys.DebugGodMode, GameServices.GodMode);
        _airJump.Text = messages.Get(MessageKeys.DebugAirJump, GameServices.AirJumpUnlocked);
    }

    private void Reload()
    {
        try
        {
            GameServices.Load();
            GameLog.Info(MessageKeys.DataReloaded);
            GetTree().ReloadCurrentScene();
        }
        catch (GameDataException error)
        {
            GameLog.Error(error);
        }
    }
}
