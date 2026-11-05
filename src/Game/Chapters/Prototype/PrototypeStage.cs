using System.Collections.Generic;
using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Game.Gameplay.Combat;
using LumiFeast.Game.Gameplay.Enemies;
using LumiFeast.Game.Gameplay.Player;
using LumiFeast.Game.Systems.Debug;
using LumiFeast.Game.UI;
using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;
using LumiFeast.Logic.Stats;

namespace LumiFeast.Game.Chapters.Prototype;

/// <summary>Builds the test stage from its data file. Later a shared StageBuilder grows out of this.</summary>
public partial class PrototypeStage : Node2D
{
    private const float WallThickness = 200;
    private const float GroundThickness = 400;

    private readonly List<MonsterController> _monsters = [];
    private readonly Dictionary<MonsterController, SpawnDef> _spawnOf = [];
    private StageDef _stage = null!;
    private GameFeel _gameFeel = null!;
    private PlayerController _player = null!;

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
        _player.Setup(lumi, data.AttackSet(lumi.Combat.AttackSet), data.Feel, _gameFeel, new Vector2(_stage.PlayerSpawnX, _stage.GroundY - 4));
        _player.Blockers = _monsters;
        _player.AddChild(camera);
        camera.Position = new Vector2(0, -160);
        camera.MakeCurrent();

        RespawnAll();

        var hud = new PrototypeHud();
        AddChild(hud);
        hud.Setup(_player);

        var debug = new DebugPanel();
        AddChild(debug);
        debug.Setup(_player, _stage.Tier, RespawnAll);

        if (AutoplayRunner.RequestedScript() is { } script)
        {
            var autoplay = new AutoplayRunner();
            AddChild(autoplay);
            autoplay.Setup(script, _player);
        }

        GameLog.Info(MessageKeys.StageLoaded, _stage.Id, CurrentTier, GameServices.DifficultyId);
    }

    private int CurrentTier => GameServices.TierOverride ?? _stage.Tier;

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

        var body = new StaticBody2D { CollisionLayer = PhysicsLayers.World, CollisionMask = 0 };
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
        foreach (var monster in _monsters)
        {
            monster.QueueFree();
        }

        _monsters.Clear();
        _spawnOf.Clear();
        foreach (var spawn in _stage.Spawns)
        {
            Spawn(spawn);
        }
    }

    private void Spawn(SpawnDef spawn)
    {
        var data = GameServices.Data;
        var def = data.Monster(spawn.Monster);
        var stats = StatScaler.ForMonster(def, CurrentTier, data.Difficulty(GameServices.DifficultyId), data.Scaling);
        var monster = new MonsterController();
        AddChild(monster);
        monster.Setup(def, stats, data.Feel, _gameFeel, _player, new Vector2(spawn.X, _stage.GroundY - 4));
        monster.Healed += OnHealed;
        _monsters.Add(monster);
        _spawnOf[monster] = spawn;
    }

    private void OnHealed(MonsterController monster)
    {
        var spawn = _spawnOf[monster];
        _monsters.Remove(monster);
        _spawnOf.Remove(monster);
        var seconds = _stage.MonsterRespawnTicks / GameConstants.TicksPerSecond;
        GetTree().CreateTimer(seconds, false).Timeout += () =>
        {
            if (IsInsideTree() && !_spawnOf.ContainsValue(spawn))
            {
                Spawn(spawn);
            }
        };
    }
}
