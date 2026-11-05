using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Game.Gameplay.Player;
using LumiFeast.Logic.Common;

namespace LumiFeast.Game.Systems.Debug;

/// <summary>Smoke test: plays a scripted input file, logs the result, quits. Started with --autoplay=path.</summary>
public partial class AutoplayRunner : Node, IPlayerInput
{
    private readonly List<Step> _steps = [];
    private readonly HashSet<string> _held = [];
    private readonly HashSet<string> _pressedNow = [];
    private PlayerController _player = null!;
    private int _tick;
    private int _endTick;

    public static string? RequestedScript() =>
        OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith(GameConstants.AutoplayArgument, System.StringComparison.Ordinal))
            ?[GameConstants.AutoplayArgument.Length..];

    public void Setup(string path, PlayerController player)
    {
        _player = player;
        ProcessPhysicsPriority = -200; // apply steps before the player reads input this tick
        player.InputSource = this;
        using var document = JsonDocument.Parse(new GodotDataSource().ReadText(path));
        _endTick = document.RootElement.GetProperty("end_tick").GetInt32();
        if (document.RootElement.TryGetProperty("start_x", out var startX))
        {
            _player.Teleport(startX.GetSingle());
        }

        GameServices.GodMode = document.RootElement.TryGetProperty("god_mode", out var god) && god.GetBoolean();
        foreach (var step in document.RootElement.GetProperty("steps").EnumerateArray())
        {
            _steps.Add(new Step(step.GetProperty("tick").GetInt32(), step.GetProperty("action").GetString()!, step.GetProperty("pressed").GetBoolean()));
        }

        GameLog.Info(MessageKeys.AutoplayStarted, path);
    }

    public override void _PhysicsProcess(double delta)
    {
        _pressedNow.Clear();
        foreach (var step in _steps.Where(item => item.Tick == _tick))
        {
            if (step.Pressed)
            {
                _held.Add(step.Action);
                _pressedNow.Add(step.Action);
            }
            else
            {
                _held.Remove(step.Action);
            }
        }

        _tick++;
        if (_tick >= _endTick)
        {
            GameLog.Info(
                MessageKeys.AutoplayFinished,
                (int)_player.GlobalPosition.X,
                _player.Health.Current,
                _player.Health.Max,
                (int)_player.Mp,
                _player.TotalHits,
                (int)_player.GlobalPosition.Y);
            GetTree().Quit();
        }
    }

    public PlayerFrameInput Read() => new(
        (_held.Contains(InputActions.MoveRight) ? 1 : 0) - (_held.Contains(InputActions.MoveLeft) ? 1 : 0),
        _pressedNow.Contains(InputActions.Jump),
        _held.Contains(InputActions.Jump),
        _pressedNow.Contains(InputActions.Dash),
        _pressedNow.Contains(InputActions.AttackLight),
        _held.Contains(InputActions.MoveDown),
        _pressedNow.Contains(InputActions.AttackHeavy),
        _pressedNow.Contains(InputActions.MoveDown),
        _held.Contains(InputActions.Sprint));

    private readonly record struct Step(int Tick, string Action, bool Pressed);
}
