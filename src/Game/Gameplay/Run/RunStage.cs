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
using LumiFeast.Logic.Run;
using LumiFeast.Logic.Stages;

namespace LumiFeast.Game.Gameplay.Run;

public enum RewardKind
{
    Any,
    Skills,
    Amulets,
}

/// <summary>One roguelite run: builds a room, runs its waves, opens the door, moves on.</summary>
public partial class RunStage : Node2D
{
    private const float FloorClearance = 4;
    private const float CameraHeight = 160;
    private const float DoorZoneWidth = 90;
    private const int PromptFontSize = 40;
    private const float TokenLift = 60;
    private const float BrokenCrateSeconds = 1.2f;
    /// <summary>Time to watch the boss heal before the result screen comes up.</summary>
    private const int ClearScreenDelayTicks = 100;

    private const float ShadeSeconds = 0.8f;

    /// <summary>A lair turns the room dusky and mysterious while it runs.</summary>
    private static readonly Color LairShade = new(0.62f, 0.57f, 0.86f);
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
    private bool _rewardPending;
    private RewardPicker _picker = null!;
    private SeededRandom _random = new(1);
    private RunResultScreen _result = null!;
    private readonly RunStats _stats = new();
    private bool _runOver;
    private int _clearScreenTicks;
    private ChallengeRun? _challenge;
    private bool _challengeSettled;
    private CanvasModulate _shade = null!;

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
        _player.AutoRespawn = false;
        _player.Fell += () => ShowResult(false);
        _player.Hurt += () => _challenge?.PlayerHit();
        _player.AddChild(_camera);
        _camera.Position = new Vector2(0, -CameraHeight);
        _camera.MakeCurrent();

        _hud = new PrototypeHud();
        AddChild(_hud);
        _hud.Setup(_player);

        _picker = new RewardPicker();
        AddChild(_picker);
        _shade = new CanvasModulate { Color = Colors.White };
        AddChild(_shade);
        _result = new RunResultScreen();
        AddChild(_result);

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
            if (autoplay.StartRoom is { } room)
            {
                // a room the seeded plan left out takes the second slot
                var plan = _plan.ToList();
                if (!plan.Contains(room))
                {
                    plan[1] = room;
                }

                _plan = plan;
                _roomIndex = plan.IndexOf(room);
                BuildRoom();
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_clearScreenTicks > 0 && --_clearScreenTicks == 0)
        {
            ShowResult(true);
        }

        if (_gameFeel.Frozen || _rewardPending || _runOver)
        {
            return;
        }

        _stats.Step();
        _stats.Combo(_player.ComboHits);

        switch (_waves.Step(_monsters.Count))
        {
            case WaveEvent.SpawnWave:
                SpawnWave();
                break;
            case WaveEvent.Cleared when _challenge is not { NeedsLever: true }:
                OpenDoor();
                break;
        }

        StepChallenge();
        StepDoor();
    }

    private void StartRun()
    {
        var seed = AutoplayRunner.RequestedScript() is null ? Time.GetTicksUsec() : GameConstants.ScriptedRunSeed;
        _plan = RunPlan.Build(_run, seed);
        _random = new SeededRandom(seed);
        _player.Loadout.Clear();
        _player.Modifiers.Clear();
        _player.ApplyModifiers();
        _roomIndex = 0;
        _stats.Reset();
        _clearScreenTicks = 0;
        GameLog.Info(MessageKeys.RunStarted, string.Join(' ', _plan));
        BuildRoom();
        _player.Revive();
        OfferReward(RewardKind.Skills); // the run starts with one skill, never empty-handed
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
        _hud.TrackBoss(null);
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
        BuildProps();
        _camera.LimitRight = (int)_room.Width;
        var spawn = new Vector2(_run.PlayerSpawnX, _room.GroundY - FloorClearance);
        _player.SetRespawnPoint(spawn);
        _player.MoveTo(spawn);
        _roomCleared = false;
        _groundedTicks = 0;
        _waves = new WaveRunner(_room.Waves.Count, _run.WaveDelayTicks);
        _challengeSettled = false;
        _challenge = _room.Challenge is { } trial ? new ChallengeRun(trial) : null;
        _shade.Color = Colors.White;
        _hud.ShowStatus(GameServices.Messages.Get(
            _plan[_roomIndex] == _run.BossRoom ? MessageKeys.HudBoss : MessageKeys.HudRoom, _roomIndex + 1, _plan.Count));
        if (_challenge is { NeedsLever: true })
        {
            // a lair is optional, so its door is open until the lever is hit
            _roomCleared = true;
            _door.Modulate = Colors.White;
            _hud.ShowStatus(GameServices.Messages.Get(MessageKeys.TrialOffer));
        }
        else
        {
            _waves.Start();
        }
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
        _prompt = UiCommon.OutlinedLabel(PromptFontSize, GameServices.Messages.Get(MessageKeys.RunDoorPrompt));
        _prompt.Position = new Vector2(_door.Position.X - 40, _room.GroundY - _run.DoorHeight - 60);
        _prompt.Visible = false;
        _roomNode.AddChild(_prompt);
    }

    private void BuildProps()
    {
        var pickups = _run.Pickups;
        foreach (var x in _room.Crates ?? [])
        {
            var crate = new HittableProp();
            _roomNode.AddChild(crate);
            crate.Setup(pickups.CrateSprite, pickups.CrateHeight, new Vector2(x, _room.GroundY), pickups.CrateHits);
            crate.Broken += BreakCrate;
        }

        if (_room.Challenge is { Kind: ChallengeKinds.Lair } trial)
        {
            var lever = new HittableProp();
            _roomNode.AddChild(lever);
            lever.Setup(pickups.LeverSprite, pickups.LeverHeight, new Vector2(trial.LeverX, _room.GroundY), 1);
            lever.Broken += StartChallenge;
        }

        if (_room.ChestX is { } chestX)
        {
            SpawnChest(chestX, pickups.ChestHeight);
        }
    }

    private void SpawnChest(float x, float height)
    {
        var chest = new HittableProp();
        _roomNode.AddChild(chest);
        chest.Setup(_run.Pickups.ChestSprite, height, new Vector2(x, _room.GroundY), 1);
        chest.Broken += OpenChest;
    }

    private void BreakCrate(HittableProp crate)
    {
        var token = new Pickup();
        _roomNode.AddChild(token);
        token.Setup(_player, _run.Pickups, _random.NextDouble() < _run.Pickups.HpChance, crate.GlobalPosition + new Vector2(0, -TokenLift));
        crate.ChangeSprite(_run.Pickups.CrateBrokenSprite);
        crate.FadeAway(BrokenCrateSeconds);
    }

    private void OpenChest(HittableProp chest)
    {
        chest.ChangeSprite(_run.Pickups.ChestOpenSprite);
        OfferReward(RewardKind.Amulets);
    }

    /// <summary>Accepting a lair darkens the room and shuts the door until every monster is healed.</summary>
    private void StartChallenge(HittableProp lever)
    {
        lever.ChangeSprite(_run.Pickups.LeverOnSprite);
        if (_challenge?.Start() != true)
        {
            return;
        }

        GameLog.Info(MessageKeys.TrialStarted, _challenge.Def.Kind);
        _roomCleared = false;
        _door.Modulate = ClosedDoor;
        CreateTween().TweenProperty(_shade, "color", LairShade, ShadeSeconds);
        _waves.Start();
    }

    /// <summary>Shows the trial on the status line and settles it once: a win brings a chest.</summary>
    private void StepChallenge()
    {
        // a hit can lose the bonus between steps, so settle any state past Waiting
        if (_challenge is not { } trial || trial.State == ChallengeState.Waiting || _challengeSettled)
        {
            return;
        }

        var messages = GameServices.Messages;
        switch (trial.Step(_waves.IsCleared))
        {
            case ChallengeState.Running:
                _hud.ShowStatus(messages.Get(trial.NeedsLever ? MessageKeys.TrialLair : MessageKeys.TrialNoHit));
                break;
            case ChallengeState.Won:
                _challengeSettled = true;
                GameLog.Info(MessageKeys.TrialWon);
                _hud.ShowStatus(messages.Get(MessageKeys.TrialWon));
                if (trial.NeedsLever)
                {
                    _roomCleared = true;
                    _door.Modulate = Colors.White;
                    _stats.RoomCleared();
                    CreateTween().TweenProperty(_shade, "color", Colors.White, ShadeSeconds);
                }

                SpawnChest(trial.Def.ChestX, trial.NeedsLever ? _run.Pickups.BigChestHeight : _run.Pickups.ChestHeight);
                break;
            case ChallengeState.Failed:
                _challengeSettled = true;
                GameLog.Info(MessageKeys.TrialBonusLost);
                _hud.ShowStatus(messages.Get(MessageKeys.TrialBonusLost));
                break;
        }
    }

    private void SpawnWave()
    {
        foreach (var spawn in _room.Waves[_waves.Wave].Spawns)
        {
            var monster = MonsterSpawner.Spawn(_roomNode, spawn.Monster, spawn.X, _room.GroundY, _run.Tier + (_challenge?.Def.TierBonus ?? 0), _gameFeel, _player);
            monster.Healed += OnHealed;
            _monsters.Add(monster);
            if (_plan[_roomIndex] == _run.BossRoom)
            {
                _hud.TrackBoss(monster.Health);
            }
        }
    }

    private void OnHealed(MonsterController monster)
    {
        _monsters.Remove(monster);
        _stats.Healed();
    }

    private void OpenDoor()
    {
        _roomCleared = true;
        _stats.RoomCleared();
        _hud.TrackBoss(null);
        _door.Modulate = Colors.White;
        var last = _roomIndex + 1 >= _plan.Count;
        if (!last && _room.RewardOnClear)
        {
            OfferReward(RewardKind.Any);
        }

        if (last)
        {
            GameLog.Info(MessageKeys.RunCleared);
            _hud.ShowStatus(GameServices.Messages.Get(MessageKeys.HudClear));
            _clearScreenTicks = ClearScreenDelayTicks;
        }
    }

    /// <summary>Freezes the room behind a summary; attack starts the next run.</summary>
    private void ShowResult(bool cleared)
    {
        var messages = GameServices.Messages;
        if (!cleared)
        {
            GameLog.Info(MessageKeys.RunDefeated, _roomIndex + 1);
        }

        var picked = _player.Loadout.Slots.OfType<SkillDef>().Select(skill => messages.Get(MessageKeys.SkillPrefix + skill.Id))
            .Concat(_player.Modifiers.Amulets.Select(amulet => messages.Get(MessageKeys.AmuletPrefix + amulet.Id)));
        var lines = new List<string>
        {
            messages.Get(MessageKeys.ResultTime, _stats.Time((int)GameConstants.TicksPerSecond)),
            messages.Get(MessageKeys.ResultRooms, _stats.RoomsCleared, _plan.Count),
            messages.Get(MessageKeys.ResultHealed, _stats.MonstersHealed),
            messages.Get(MessageKeys.ResultCombo, _stats.BestCombo),
            messages.Get(MessageKeys.ResultLoadout, string.Join(", ", picked)),
        };
        _runOver = true;
        _player.InputPaused = true;
        GetTree().Paused = true;
        _result.Open(
            cleared,
            messages.Get(cleared ? MessageKeys.ResultClearTitle : MessageKeys.ResultDefeatTitle),
            messages.Get(cleared ? MessageKeys.ResultClearLine : MessageKeys.ResultDefeatLine, _roomIndex + 1, _plan.Count),
            lines,
            messages.Get(cleared ? MessageKeys.ResultAgain : MessageKeys.ResultRetry),
            () =>
            {
                GetTree().Paused = false;
                _runOver = false;
                _player.InputPaused = false;
                StartRun();
            });
    }

    /// <summary>One of three: skills while a slot is free, and amulets not taken yet.</summary>
    private void OfferReward(RewardKind kind, int? choices = null)
    {
        var data = GameServices.Data;
        var skills = data.SkillSet(data.Character(GameConstants.PlayerCharacterId).Combat.SkillSet).Skills;
        var amulets = data.AmuletSet(_run.AmuletSet).Amulets;
        var pool = new List<string>();
        if (!_player.Loadout.IsFull && kind != RewardKind.Amulets)
        {
            pool.AddRange(skills.Where(skill => !_player.Loadout.Has(skill.Id)).Select(skill => MessageKeys.SkillPrefix + skill.Id));
        }

        if (kind != RewardKind.Skills)
        {
            pool.AddRange(amulets.Where(amulet => !_player.Modifiers.Has(amulet.Id)).Select(amulet => MessageKeys.AmuletPrefix + amulet.Id));
        }
        var picks = RewardDraw.Pick(pool, choices ?? _run.RewardChoices, _random);
        if (picks.Count == 0)
        {
            return;
        }

        var messages = GameServices.Messages;
        var cards = picks.Select(key => (messages.Get(key), messages.Get(key + MessageKeys.DescriptionSuffix) + "\n" + (key.StartsWith(MessageKeys.SkillPrefix, System.StringComparison.Ordinal)
            ? messages.Get(MessageKeys.RewardSkill, skills.First(skill => MessageKeys.SkillPrefix + skill.Id == key).MpCost)
            : messages.Get(MessageKeys.RewardAmulet)))).ToList();
        _rewardPending = true;
        _player.InputPaused = true;
        _picker.Open(cards, index =>
        {
            var key = picks[index];
            if (key.StartsWith(MessageKeys.SkillPrefix, System.StringComparison.Ordinal))
            {
                _player.Loadout.Add(skills.First(skill => MessageKeys.SkillPrefix + skill.Id == key));
            }
            else
            {
                _player.Modifiers.Add(amulets.First(amulet => MessageKeys.AmuletPrefix + amulet.Id == key));
                _player.ApplyModifiers();
            }

            GameLog.Info(MessageKeys.RewardPicked, key);
            _rewardPending = false;
            _player.InputPaused = false;
        });
        if (AutoplayRunner.RequestedScript() is not null)
        {
            _picker.Choose(0);
        }
    }

    /// <summary>The prompt waits until Lumi has stood still on the ground, so nobody presses it from the air.</summary>
    private void StepDoor()
    {
        var atDoor = _roomCleared && !_rewardPending && Mathf.Abs(_player.GlobalPosition.X - _door.Position.X) < DoorZoneWidth && _player.OnGround;
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
