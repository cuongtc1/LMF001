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
    private int _huntFrom = -1;
    private int _huntTo = -1;

    private const float HuntReach = 170;
    private const float HuntJumpHeight = 160;
    private const int HuntSwingTicks = 16;
    private const int HuntMenuTicks = 30;
    private const int HuntStuckTicks = 180;
    private const int HuntReportTicks = 600;
    private int _stuckTicks;

    /// <summary>Room id a run script starts in (the boss den, say), or null for the first room.</summary>
    public string? StartRoom { get; private set; }

    /// <summary>False keeps reward screens open (for screenshots of the cards); the default picks the first card.</summary>
    public static bool PicksRewards { get; private set; } = true;

    /// <summary>Gold carried at the start (to test the shop).</summary>
    public int StartGold { get; private set; }

    /// <summary>Keys carried at the start (to test locked chests).</summary>
    public int StartKeys { get; private set; }

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
        PicksRewards = !document.RootElement.TryGetProperty("pick_rewards", out var picks) || picks.GetBoolean();
        StartKeys = document.RootElement.TryGetProperty("keys", out var keys) ? keys.GetInt32() : 0;
        StartGold = document.RootElement.TryGetProperty("gold", out var gold) ? gold.GetInt32() : 0;
        if (document.RootElement.TryGetProperty("merchant", out var merchant))
        {
            GameServices.MerchantMet = merchant.GetBoolean();
        }
        // "hunt": [from, to] lets Lumi walk to the nearest monster and attack, so a test survives room layout changes
        if (document.RootElement.TryGetProperty("hunt", out var hunt))
        {
            _huntFrom = hunt[0].GetInt32();
            _huntTo = hunt[1].GetInt32();
        }

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

        if (_tick >= _huntFrom && _tick < _huntTo)
        {
            Hunt();
            if (_tick % HuntReportTicks == 0)
            {
                GD.Print($"[autoplay] {_tick / 60}s x={(int)_player.GlobalPosition.X} hits={_player.TotalHits}");
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

    /// <summary>
    /// A simple bot: walks to the nearest monster and swings; with no monster left it walks right and keeps swinging
    /// (that opens shrines, crates, chests and levers), and it picks the first card of any reward screen.
    /// Good enough to play a whole run and time it.
    /// </summary>
    private void Hunt()
    {
        _held.Remove(InputActions.MoveLeft);
        _held.Remove(InputActions.MoveRight);
        if (GetTree().Paused)
        {
            if (_tick % HuntMenuTicks == 0)
            {
                Input.ActionPress(InputActions.AttackLight);
            }
            else if (_tick % HuntMenuTicks == 2)
            {
                Input.ActionRelease(InputActions.AttackLight);
            }

            return;
        }

        var target = GetTree().GetNodesInGroup("monsters").OfType<Gameplay.Enemies.MonsterController>()
            .Where(monster => monster.IsActive)
            .OrderBy(monster => monster.BodyCenter.DistanceSquaredTo(_player.GlobalPosition))
            .FirstOrDefault();
        if (target is null)
        {
            _held.Add(InputActions.MoveRight);
            if (_tick % HuntSwingTicks == 0)
            {
                _pressedNow.Add(InputActions.AttackLight);
            }

            return;
        }

        var delta = target.GlobalPosition - _player.GlobalPosition;
        // the bot cannot plan a climb: after a few seconds out of reach it hops onto the monster's level (timing only)
        _stuckTicks = Mathf.Abs(delta.Y) > HuntJumpHeight ? _stuckTicks + 1 : 0;
        if (_stuckTicks > HuntStuckTicks)
        {
            _stuckTicks = 0;
            _player.MoveTo(target.GlobalPosition + new Vector2(-HuntReach * Mathf.Sign(delta.X == 0 ? 1 : delta.X), -10));
            return;
        }

        var toward = delta.X < 0 ? InputActions.MoveLeft : InputActions.MoveRight;
        if (Mathf.Abs(delta.X) > HuntReach)
        {
            _held.Add(toward);
        }
        else if (_tick % HuntSwingTicks == 0)
        {
            _held.Add(toward); // a held direction on the swing tick turns Lumi to face it
            _pressedNow.Add(InputActions.AttackLight);
        }

        if (delta.Y < -HuntJumpHeight && _tick % 40 == 0)
        {
            _pressedNow.Add(InputActions.Jump);
            _held.Add(InputActions.Jump);
        }
        else if (_tick % 40 == 20)
        {
            _held.Remove(InputActions.Jump);
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
