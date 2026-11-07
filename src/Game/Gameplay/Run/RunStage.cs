using System.Collections.Generic;
using System.Linq;
using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Game.Gameplay.Combat;
using LumiFeast.Game.Gameplay.Enemies;
using LumiFeast.Game.Gameplay.Player;
using LumiFeast.Game.Gameplay.World;
using LumiFeast.Game.Systems.Debug;
using LumiFeast.Game.UI;
using LumiFeast.Logic.Combat;
using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;
using LumiFeast.Logic.Stages;

namespace LumiFeast.Game.Gameplay.Run;

/// <summary>One roguelite run: builds a room, runs its waves, opens the door, moves on.</summary>
public partial class RunStage : Node2D
{
    private const float FloorClearance = 4;
    private const float CameraHeight = 160;
    private const float DoorZoneWidth = 90;
    private const int PromptFontSize = 40;

    private static readonly Color ClosedDoor = new(0.55f, 0.5f, 0.6f);

    private readonly List<MonsterController> _monsters = [];
    private RunDef _run = null!;
    private RoomDef _room = null!;
    private IReadOnlyList<string> _plan = [];
    private int _roomIndex;
    private GameFeel _gameFeel = null!;
    private PlayerController _player = null!;
    private PrototypeHud _hud = null!;
    private Camera2D _camera = null!;
    private WaveRunner _waves = null!;
    private Node2D _roomNode = null!;
    private Sprite2D _door = null!;
    private Label _prompt = null!;
    private int _groundedTicks;
    private bool _roomCleared;

    public override void _Ready()
    {
        if (GameServices.Data is null)
        {
            GameServices.Load();
        }

        var data = GameServices.Data!;
        _run = data.Run(GameConstants.FirstRunId);
        _camera = new Camera2D { LimitLeft = 0, LimitTop = 0, LimitBottom = 1080, PositionSmoothingEnabled = true, PositionSmoothingSpeed = 9 };
        _gameFeel = new GameFeel();
        AddChild(_gameFeel);
        _gameFeel.Setup(data.Feel, _camera);

        var lumi = data.Character(GameConstants.PlayerCharacterId);
        _player = new PlayerController();
        AddChild(_player);
        _player.Setup(lumi, data.AttackSet(lumi.Combat.AttackSet), data.Feel, _gameFeel, Vector2.Zero);
        _player.Blockers = _monsters;
        _player.Respawned += StartRun;
        _player.AddChild(_camera);
        _camera.Position = new Vector2(0, -CameraHeight);
        _camera.MakeCurrent();

        _hud = new PrototypeHud();
        AddChild(_hud);
        _hud.Setup(_player);

        var debug = new DebugPanel();
        AddChild(debug);
        debug.Setup(_player, _run.Tier, StartRun, NextRoom);

        var script = AutoplayRunner.RequestedScript();
        StartRun();
        if (script is not null)
        {
            var autoplay = new AutoplayRunner();
            AddChild(autoplay);
            autoplay.Setup(script, _player);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_gameFeel.Frozen)
        {
            return;
        }

        switch (_waves.Step(_monsters.Count))
        {
            case WaveEvent.SpawnWave:
                SpawnWave();
                break;
            case WaveEvent.Cleared:
                OpenDoor();
                break;
        }

        StepDoor();
    }

    private void StartRun()
    {
        var seed = AutoplayRunner.RequestedScript() is null ? Time.GetTicksUsec() : GameConstants.ScriptedRunSeed;
        _plan = RunPlan.Build(_run, seed);
        _roomIndex = 0;
        GameLog.Info(MessageKeys.RunStarted, string.Join(' ', _plan));
        BuildRoom();
    }

    private void NextRoom()
    {
        if (_roomIndex + 1 >= _plan.Count)
        {
            StartRun();
            return;
        }

        _roomIndex++;
        BuildRoom();
    }

    private void BuildRoom()
    {
        _monsters.Clear();
        _roomNode?.QueueFree();
        _room = _run.Rooms.First(room => room.Id == _plan[_roomIndex]);
        _roomNode = new Node2D();
        AddChild(_roomNode);
        foreach (var layer in _room.Layers)
        {
            StageBuilder.AddLayer(_roomNode, layer);
        }

        StageBuilder.AddBounds(_roomNode, _room.Width, _room.GroundY);
        foreach (var platform in _room.Platforms)
        {
            StageBuilder.AddPlatform(_roomNode, platform);
        }

        BuildDoor();
        _camera.LimitRight = (int)_room.Width;
        var spawn = new Vector2(_run.PlayerSpawnX, _room.GroundY - FloorClearance);
        _player.SetRespawnPoint(spawn);
        _player.MoveTo(spawn);
        _roomCleared = false;
        _groundedTicks = 0;
        _waves = new WaveRunner(_room.Waves.Count, _run.WaveDelayTicks);
        _waves.Start();
        _hud.ShowStatus(GameServices.Messages.Get(
            _plan[_roomIndex] == _run.BossRoom ? MessageKeys.HudBoss : MessageKeys.HudRoom, _roomIndex + 1, _plan.Count));
    }

    private void BuildDoor()
    {
        var texture = GameServices.Texture(_run.DoorSprite);
        var scale = _run.DoorHeight / texture.GetHeight();
        _door = new Sprite2D
        {
            Texture = texture,
            Scale = Vector2.One * scale,
            Position = new Vector2(_room.Width - _run.DoorInset, _room.GroundY - _run.DoorHeight / 2),
            Modulate = ClosedDoor,
            ZIndex = -4,
        };
        _roomNode.AddChild(_door);
        _prompt = new Label
        {
            Text = GameServices.Messages.Get(MessageKeys.RunDoorPrompt),
            Position = new Vector2(_door.Position.X - 40, _room.GroundY - _run.DoorHeight - 60),
            Visible = false,
        };
        _prompt.AddThemeFontSizeOverride("font_size", PromptFontSize);
        _prompt.AddThemeColorOverride("font_outline_color", Colors.Black);
        _prompt.AddThemeConstantOverride("outline_size", 8);
        _roomNode.AddChild(_prompt);
    }

    private void SpawnWave()
    {
        foreach (var spawn in _room.Waves[_waves.Wave].Spawns)
        {
            var monster = MonsterSpawner.Spawn(_roomNode, spawn.Monster, spawn.X, _room.GroundY, _run.Tier, _gameFeel, _player);
            monster.Healed += OnHealed;
            _monsters.Add(monster);
        }
    }

    private void OnHealed(MonsterController monster) => _monsters.Remove(monster);

    private void OpenDoor()
    {
        _roomCleared = true;
        _door.Modulate = Colors.White;
        var last = _roomIndex + 1 >= _plan.Count;
        if (last)
        {
            GameLog.Info(MessageKeys.RunCleared);
            _hud.ShowStatus(GameServices.Messages.Get(MessageKeys.HudClear));
        }
    }

    /// <summary>The prompt waits until Lumi has stood still on the ground, so nobody presses it from the air.</summary>
    private void StepDoor()
    {
        var atDoor = _roomCleared && Mathf.Abs(_player.GlobalPosition.X - _door.Position.X) < DoorZoneWidth && _player.OnGround;
        _groundedTicks = atDoor ? _groundedTicks + 1 : 0;
        var ready = _groundedTicks >= _run.DoorPromptTicks;
        _prompt.Visible = ready;
        _player.SuppressJump = ready;
        if (ready && _player.UpPressed)
        {
            _player.SuppressJump = false;
            NextRoom();
        }
    }
}
