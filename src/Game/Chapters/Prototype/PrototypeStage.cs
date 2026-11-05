using System.Collections.Generic;
using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Game.Gameplay.Combat;
using LumiFeast.Game.Gameplay.Enemies;
using LumiFeast.Game.Gameplay.Player;
using LumiFeast.Game.Systems.Debug;
using LumiFeast.Game.UI;
using LumiFeast.Logic.Combat;
using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;
using LumiFeast.Logic.Stats;

namespace LumiFeast.Game.Chapters.Prototype;

/// <summary>Builds the test stage from its data file. Later a shared StageBuilder grows out of this.</summary>
public partial class PrototypeStage : Node2D
{
    private const float WallThickness = 200;
    private const float GroundThickness = 400;
    private const float ArenaRespawnOffset = 120;

    private static readonly Color ArenaWallColor = new(1f, 0.82f, 0.3f, 0.45f);

    private readonly List<MonsterController> _monsters = [];
    private readonly Dictionary<MonsterController, SpawnDef> _spawnOf = [];
    private readonly HashSet<MonsterController> _arenaMonsters = [];
    private StageDef _stage = null!;
    private GameFeel _gameFeel = null!;
    private PlayerController _player = null!;
    private PrototypeHud _hud = null!;
    private WaveRunner? _waves;
    private StaticBody2D _arenaWalls = null!;
    private Node2D _arenaWallView = null!;
    private Vector2 _playerStart;

    public override void _Ready()
    {
        if (GameServices.Data is null)
        {
            GameServices.Load();
        }

        var data = GameServices.Data!;
        _stage = data.Stage(GameConstants.PrototypeStageId);

        foreach (var layer in _stage.Layers)
        {
            AddLayer(layer);
        }

        AddWorldCollision();
        foreach (var platform in _stage.Platforms)
        {
            AddPlatform(platform);
        }

        var camera = new Camera2D
        {
            LimitLeft = 0,
            LimitRight = (int)_stage.Width,
            LimitTop = 0,
            LimitBottom = 1080,
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 9,
        };
        _gameFeel = new GameFeel();
        AddChild(_gameFeel);
        _gameFeel.Setup(data.Feel, camera);

        var lumi = data.Character(GameConstants.PlayerCharacterId);
        _player = new PlayerController();
        AddChild(_player);
        _playerStart = new Vector2(_stage.PlayerSpawnX, _stage.GroundY - 4);
        _player.Setup(lumi, data.AttackSet(lumi.Combat.AttackSet), data.Feel, _gameFeel, _playerStart);
        _player.Respawned += OnPlayerRespawned;
        _player.Blockers = _monsters;
        _player.AddChild(camera);
        camera.Position = new Vector2(0, -160);
        camera.MakeCurrent();

        BuildArena();
        _hud = new PrototypeHud();
        AddChild(_hud);
        _hud.Setup(_player);
        RespawnAll();

        var debug = new DebugPanel();
        AddChild(debug);
        debug.Setup(_player, _stage.Tier, RespawnAll, GoToArena);

        if (AutoplayRunner.RequestedScript() is { } script)
        {
            var autoplay = new AutoplayRunner();
            AddChild(autoplay);
            autoplay.Setup(script, _player);
        }

        GameLog.Info(MessageKeys.StageLoaded, _stage.Id, CurrentTier, GameServices.DifficultyId);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_waves is null || _stage.Arena is not { } arena || _gameFeel.Frozen)
        {
            return;
        }

        if (!_waves.IsRunning && !_waves.IsCleared && _player.GlobalPosition.X >= arena.TriggerX)
        {
            _waves.Start();
            SetArenaClosed(true);
            _player.SetRespawnPoint(new Vector2(arena.TriggerX - ArenaRespawnOffset, _stage.GroundY - 4));
        }

        switch (_waves.Step(_arenaMonsters.Count))
        {
            case WaveEvent.SpawnWave:
                foreach (var spawn in arena.Waves[_waves.Wave].Spawns)
                {
                    _arenaMonsters.Add(Spawn(spawn));
                }

                GameLog.Info(MessageKeys.ArenaWave, _waves.Wave + 1, arena.Waves.Count);
                _hud.ShowStatus(GameServices.Messages.Get(MessageKeys.HudWave, _waves.Wave + 1, arena.Waves.Count));
                break;
            case WaveEvent.Cleared:
                SetArenaClosed(false);
                GameLog.Info(MessageKeys.ArenaCleared);
                _hud.ShowStatus(GameServices.Messages.Get(MessageKeys.HudClear));
                break;
        }
    }

    private int CurrentTier => GameServices.TierOverride ?? _stage.Tier;

    private void BuildArena()
    {
        if (_stage.Arena is not { } arena)
        {
            return;
        }

        _waves = new WaveRunner(arena.Waves.Count, arena.WaveDelayTicks);
        _arenaWalls = new StaticBody2D { CollisionLayer = 0, CollisionMask = 0 };
        _arenaWallView = new Node2D { Visible = false, ZIndex = 5 };
        var size = new Vector2(WallThickness, _stage.GroundY * 2);
        foreach (var side in new[] { arena.LeftX - WallThickness / 2, arena.RightX + WallThickness / 2 })
        {
            AddSolid(_arenaWalls, new Vector2(side, 0), size);
            _arenaWallView.AddChild(new ColorRect
            {
                Color = ArenaWallColor,
                Position = new Vector2(side - WallThickness / 2, 0),
                Size = new Vector2(WallThickness, _stage.GroundY),
            });
        }

        AddChild(_arenaWalls);
        AddChild(_arenaWallView);
    }

    private void SetArenaClosed(bool closed)
    {
        _arenaWalls.CollisionLayer = closed ? PhysicsLayers.World : 0;
        _arenaWallView.Visible = closed;
    }

    private void ResetArena()
    {
        if (_waves is null)
        {
            return;
        }

        foreach (var monster in _arenaMonsters)
        {
            _monsters.Remove(monster);
            monster.QueueFree();
        }

        _arenaMonsters.Clear();
        _waves.Reset();
        SetArenaClosed(false);
        _player.SetRespawnPoint(_playerStart);
        _hud.ShowStatus(string.Empty);
    }

    private void OnPlayerRespawned()
    {
        if (_waves is { IsRunning: true })
        {
            var point = _player.GlobalPosition;
            ResetArena();
            _player.SetRespawnPoint(point);
        }
    }

    private void GoToArena()
    {
        if (_stage.Arena is { } arena)
        {
            ResetArena();
            _player.Teleport(arena.TriggerX - ArenaRespawnOffset);
        }
    }

    private void AddLayer(LayerDef layer)
    {
        var texture = GameServices.Texture(layer.Sprite);
        var parallax = new Parallax2D
        {
            ScrollScale = new Vector2(layer.ScrollScale, 1),
            RepeatSize = new Vector2(texture.GetWidth(), 0),
            RepeatTimes = 4,
            ZIndex = layer.ZIndex,
        };
        parallax.AddChild(new Sprite2D { Texture = texture, Centered = false });
        AddChild(parallax);
    }

    private void AddWorldCollision()
    {
        var body = new StaticBody2D { CollisionLayer = PhysicsLayers.World, CollisionMask = 0 };
        var groundSize = new Vector2(_stage.Width + WallThickness * 2, GroundThickness);
        AddSolid(body, new Vector2(_stage.Width / 2, _stage.GroundY + GroundThickness / 2), groundSize);
        var wallSize = new Vector2(WallThickness, _stage.GroundY * 2);
        AddSolid(body, new Vector2(-WallThickness / 2, 0), wallSize);
        AddSolid(body, new Vector2(_stage.Width + WallThickness / 2, 0), wallSize);
        AddChild(body);
    }

    private static void AddSolid(StaticBody2D body, Vector2 position, Vector2 size) =>
        body.AddChild(new CollisionShape2D { Position = position, Shape = new RectangleShape2D { Size = size } });

    private void AddPlatform(PlatformDef platform)
    {
        var texture = GameServices.Texture(platform.Sprite);
        var scale = platform.Width / texture.GetWidth();
        var height = texture.GetHeight() * scale;
        AddChild(new Sprite2D
        {
            Texture = texture,
            Scale = Vector2.One * scale,
            Position = new Vector2(platform.X, platform.Y + height / 2 - platform.TopInset),
            ZIndex = -5,
        });

        var body = new StaticBody2D { CollisionLayer = PhysicsLayers.Platform, CollisionMask = 0 };
        body.AddChild(new CollisionShape2D
        {
            Position = new Vector2(platform.X, platform.Y + 8),
            Shape = new RectangleShape2D { Size = new Vector2(platform.Width - 20, 16) },
            OneWayCollision = true,
        });
        AddChild(body);
    }

    private void RespawnAll()
    {
        ResetArena();
        foreach (var monster in _monsters)
        {
            monster.QueueFree();
        }

        _monsters.Clear();
        _spawnOf.Clear();
        foreach (var spawn in _stage.Spawns)
        {
            _spawnOf[Spawn(spawn)] = spawn;
        }
    }

    private MonsterController Spawn(SpawnDef spawn)
    {
        var data = GameServices.Data;
        var def = data.Monster(spawn.Monster);
        var stats = StatScaler.ForMonster(def, CurrentTier, data.Difficulty(GameServices.DifficultyId), data.Scaling);
        var monster = new MonsterController();
        AddChild(monster);
        monster.Setup(def, stats, data.Feel, _gameFeel, _player, new Vector2(spawn.X, _stage.GroundY - 4 - def.Ai.HoverHeight));
        monster.Healed += OnHealed;
        _monsters.Add(monster);
        return monster;
    }

    private void OnHealed(MonsterController monster)
    {
        _monsters.Remove(monster);
        if (_arenaMonsters.Remove(monster))
        {
            return;
        }

        var spawn = _spawnOf[monster];
        _spawnOf.Remove(monster);
        var seconds = _stage.MonsterRespawnTicks / GameConstants.TicksPerSecond;
        GetTree().CreateTimer(seconds, false).Timeout += () =>
        {
            if (IsInsideTree() && !_spawnOf.ContainsValue(spawn))
            {
                _spawnOf[Spawn(spawn)] = spawn;
            }
        };
    }
}
