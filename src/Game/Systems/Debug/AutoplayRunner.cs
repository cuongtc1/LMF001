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
    private readonly HashSet<int> _shotTicks = [];

    /// <summary>Room id a run script starts in (the boss den, say), or null for the first room.</summary>
    public string? StartRoom { get; private set; }

    /// <summary>The start room counts as already healed, to test moving around it.</summary>
    public bool StartCleared { get; private set; }

    public static string? RequestedScript() =>
        OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith(GameConstants.AutoplayArgument, System.StringComparison.Ordinal))
            ?[GameConstants.AutoplayArgument.Length..];

    public void Setup(string path, PlayerController player)
    {
        _player = player;
        ProcessPhysicsPriority = -200; // apply steps before the player reads input this tick
        ProcessMode = ProcessModeEnum.Always; // keeps counting to end_tick while a result screen pauses the game
        player.InputSource = this;
        using var document = JsonDocument.Parse(new GodotDataSource().ReadText(path));
        _endTick = document.RootElement.GetProperty("end_tick").GetInt32();
        if (document.RootElement.TryGetProperty("start_x", out var startX))
        {
            _player.Teleport(startX.GetSingle());
        }

        GameServices.GodMode = document.RootElement.TryGetProperty("god_mode", out var god) && god.GetBoolean();
        if (document.RootElement.TryGetProperty("room", out var room))
        {
            StartRoom = room.GetString();
        }

        StartCleared = document.RootElement.TryGetProperty("cleared", out var cleared) && cleared.GetBoolean();


        if (document.RootElement.TryGetProperty("skills", out var skills))
        {
            _player.Heal();
            var set = GameServices.Data.SkillSet(GameServices.Data.Character(GameConstants.PlayerCharacterId).Combat.SkillSet);
            foreach (var id in skills.EnumerateArray())
            {
                _player.Loadout.Add(set.Skills.First(skill => skill.Id == id.GetString()));
            }
        }

        if (document.RootElement.TryGetProperty("amulets", out var amulets))
        {
            var all = GameServices.Data.AmuletSets.Values.SelectMany(set => set.Amulets).ToList();
            foreach (var id in amulets.EnumerateArray())
            {
                _player.Modifiers.Add(all.First(amulet => amulet.Id == id.GetString()));
            }

            _player.ApplyModifiers();
        }

        // after "skills", which fills HP and MP
        if (document.RootElement.TryGetProperty("start_hp", out var startHp))
        {
            _player.Health.Damage(_player.Health.Current - startHp.GetInt32());
        }

        if (document.RootElement.TryGetProperty("screenshots", out var shots))
        {
            _shotTicks.UnionWith(shots.EnumerateArray().Select(tick => tick.GetInt32()));
        }

        foreach (var step in document.RootElement.GetProperty("steps").EnumerateArray())
        {
            var global = step.TryGetProperty("global", out var flag) && flag.GetBoolean();
            var at = step.TryGetProperty("x", out var x) ? new Vector2(x.GetSingle(), step.GetProperty("y").GetSingle()) : (Vector2?)null;
            _steps.Add(new Step(step.GetProperty("tick").GetInt32(), step.GetProperty("action").GetString()!, step.TryGetProperty("pressed", out var pressed) && pressed.GetBoolean(), global, at));
        }

        GameLog.Info(MessageKeys.AutoplayStarted, path);
    }

    public override void _PhysicsProcess(double delta)
    {
        _pressedNow.Clear();
        foreach (var step in _steps.Where(item => item.Tick == _tick))
        {
            if (step.Action == TeleportAction && step.At is { } at)
            {
                _player.MoveTo(at);
            }
            else if (step.Global)
            {
                // menus such as the map read Godot's Input, not the player's input source
                if (step.Pressed)
                {
                    Input.ActionPress(step.Action);
                }
                else
                {
                    Input.ActionRelease(step.Action);
                }
            }
            else if (step.Pressed)
            {
                _held.Add(step.Action);
                _pressedNow.Add(step.Action);
            }
            else
            {
                _held.Remove(step.Action);
            }
        }

        if (_shotTicks.Contains(_tick))
        {
            // user:// is %APPDATA%/Godot/app_userdata/<project name>
            GetViewport().GetTexture().GetImage().SavePng($"user://autoplay_{_tick}.png");
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
        _held.Contains(InputActions.Guard),
        System.Array.FindIndex(InputActions.Skills, _pressedNow.Contains));

    /// <param name="Global">Pressed on Godot's Input for menus instead of fed to Lumi.</param>
    private readonly record struct Step(int Tick, string Action, bool Pressed, bool Global, Vector2? At);

    /// <summary>A step that puts Lumi at its x and y, for tests of hard-to-script platforming.</summary>
    private const string TeleportAction = "teleport";
}
