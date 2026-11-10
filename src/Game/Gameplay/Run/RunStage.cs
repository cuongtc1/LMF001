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
using LumiFeast.Logic.Enemies;
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

/// <summary>
/// One roguelite run in connected rooms (Little Noah style): Lumi walks through open room edges,
/// can go back, and rooms remember what happened in them. See technical/connected_rooms.md.
/// </summary>
public partial class RunStage : Node2D
{
    private const float FloorClearance = 4;
    private const float CameraHeight = 160;
    private const float CameraDragTop = 0.35f;
    private const float CameraDragBottom = 0.15f;
    private const float CeilingHeadroom = 320;
    private const float TokenLift = 60;
    private const float BrokenCrateSeconds = 1.2f;
    private const float GateInset = 70;
    private const float FadeSeconds = 0.25f;

    /// <summary>No exit right after arriving, so Lumi never bounces straight back through the gap she came in.</summary>
    private const int ArrivalTicks = 20;

    /// <summary>Time to watch the boss heal before the result screen comes up.</summary>
    private const int ClearScreenDelayTicks = 100;

    private const float ShadeSeconds = 0.8f;
    private const string RoomChest = "room";
    private const string LairChest = "lair";
    private const string BonusChest = "bonus";

    /// <summary>A lair turns the room dusky and mysterious while it runs.</summary>
    private static readonly Color LairShade = new(0.62f, 0.57f, 0.86f);

    private static readonly Color BossColor = new("e2506a");
    private static readonly Color LairColor = new("a86ee0");
    private static readonly Color EliteColor = new("f28c3b");
    private static readonly Color ChestColor = new("ffcf5a");
    private static readonly Color ShrineColor = new("7fc4ff");
    private static readonly Color TrialColor = new("ff9a4d");

    private readonly List<MonsterController> _monsters = [];
    private readonly List<Parallax2D> _screenLayers = [];
    private AttackTurns _turns = new(1);

    /// <summary>Keys carried in this run (Little Noah keeps them per run too).</summary>
    private int _keys;
    private readonly List<Node2D> _gates = [];
    private readonly RunStats _stats = new();
    private RunDef _run = null!;
    private RoomDef _room = null!;
    private StageMap _map = new([]);
    private GameFeel _gameFeel = null!;
    private PlayerController _player = null!;
    private PrototypeHud _hud = null!;
    private Minimap _minimap = null!;
    private MapScreen _mapScreen = null!;
    private Camera2D _camera = null!;
    private WaveRunner _waves = null!;
    private Node2D? _roomNode;
    private ColorRect _fade = null!;
    private RewardPicker _picker = null!;
    private RunResultScreen _result = null!;
    private CanvasModulate _shade = null!;
    private SeededRandom _random = new(1);
    private ChallengeRun? _challenge;
    private bool _challengeSettled;
    private bool _open;
    private bool _rewardPending;
    private bool _runOver;
    private int _clearScreenTicks;
    private int _arrivalTicks;

    private RoomState State => _map.CurrentState;

    private bool InBossRoom => _room.Kind == RoomKinds.Boss;

    private Node2D RoomNode => _roomNode!;

    public override void _Ready()
    {
        if (GameServices.Data is null)
        {
            GameServices.Load();
        }

        var data = GameServices.Data!;
        _run = data.Run(GameConstants.FirstRunId);
        // small jumps do not move the camera up and down; climbing to another level does (Little Noah's tall rooms)
        _camera = new Camera2D
        {
            LimitLeft = 0,
            LimitTop = 0,
            LimitBottom = (int)RoomDef.ScreenHeight,
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 9,
            DragVerticalEnabled = true,
            DragTopMargin = CameraDragTop,
            DragBottomMargin = CameraDragBottom,
        };
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
        _hud.AmuletSetId = _run.AmuletSet;
        _hud.KeySprite = _run.Pickups.KeyIconSprite;
        _hud.Keys = () => _keys;
        _hud.Setup(_player);
        _minimap = new Minimap();
        AddChild(_minimap);
        _mapScreen = new MapScreen();
        AddChild(_mapScreen);

        _picker = new RewardPicker();
        AddChild(_picker);
        _shade = new CanvasModulate { Color = Colors.White };
        AddChild(_shade);
        var markers = new OffscreenMarkers
        {
            Targets = () => _monsters.Where(monster => IsInstanceValid(monster) && monster.IsActive).Select(monster => monster.BodyCenter),
        };
        AddChild(markers);
        _result = new RunResultScreen();
        AddChild(_result);
        var fadeLayer = new CanvasLayer { Layer = 20 };
        AddChild(fadeLayer);
        _fade = new ColorRect
        {
            Color = Colors.Black,
            Size = new Vector2(UiCommon.ScreenWidth, UiCommon.ScreenHeight),
            Modulate = Colors.Transparent,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        fadeLayer.AddChild(_fade);

        var debug = new DebugPanel();
        AddChild(debug);
        debug.Setup(_player, _run.Level, StartRun, () => GoTo(_map.Neighbor(_map.Current, 1), 1, 0));

        var script = AutoplayRunner.RequestedScript();
        StartRun();
        if (script is not null)
        {
            var autoplay = new AutoplayRunner();
            AddChild(autoplay);
            autoplay.Setup(script, _player);
            _keys = autoplay.StartKeys;
            if (autoplay.StartRoom is { } room)
            {
                JumpTo(room, autoplay.StartCleared);
            }
        }
    }

    /// <summary>Far layers of tall rooms follow the camera exactly, so the screen is always covered.</summary>
    public override void _Process(double delta)
    {
        var topLeft = _camera.GetScreenCenterPosition() - new Vector2(UiCommon.ScreenWidth, UiCommon.ScreenHeight) / 2;
        foreach (var layer in _screenLayers)
        {
            layer.ScreenOffset = topLeft;
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

        if (Input.IsActionJustPressed(InputActions.Map) && !_mapScreen.IsOpen)
        {
            OpenMap();
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
                OnRoomHealed();
                break;
        }

        StepChallenge();
        StepExits();
    }

    private void StartRun()
    {
        var seed = AutoplayRunner.RequestedScript() is null ? Time.GetTicksUsec() : GameConstants.ScriptedRunSeed;
        _random = new SeededRandom(seed);
        _map = StageMap.Generate(_run, seed);
        _player.Loadout.Clear();
        _player.Modifiers.Clear();
        _player.ApplyModifiers();
        _stats.Reset();
        _clearScreenTicks = 0;
        GameLog.Info(MessageKeys.RunStarted, string.Join(' ', _map.Rooms));
        EnterRoom(0, Entry.Start);
        _player.Revive();
    }

    /// <summary>Scripts start in a given room: one the map left out replaces a cell of its kind (or a fight cell).</summary>
    private void JumpTo(string room, bool cleared)
    {
        var rooms = _map.Rooms.ToList();
        if (!rooms.Contains(room))
        {
            // prefer a cell of the same kind whose stacked neighbors match the room's gaps
            var def = RoomById(room);
            var slots = Enumerable.Range(1, rooms.Count - 1).Where(i => RoomById(rooms[i]).Kind == def.Kind)
                .OrderByDescending(i => (_map.Neighbor(i, 0, -1) >= 0) == (def.Up is not null)).ToList();
            rooms[slots.Count > 0 ? slots[0] : rooms.FindIndex(id => RoomById(id).Kind == RoomKinds.Fight)] = room;
        }

        var cells = Enumerable.Range(0, rooms.Count).Select(_map.Cell).ToList();
        _map = new StageMap(rooms, cells);
        _map.State(0).Visited = true;
        _map.State(0).Cleared = true;
        _map.State(rooms.IndexOf(room)).Cleared = cleared;
        EnterRoom(rooms.IndexOf(room), Entry.West);
    }

    private RoomDef RoomById(string id) => _run.Rooms.First(room => room.Id == id);

    /// <summary>Moves through an edge: (dx, dy) is the direction of travel. Does nothing where no room is.</summary>
    private void GoTo(int index, int dx, int dy)
    {
        if (index < 0)
        {
            return;
        }

        GameLog.Info(MessageKeys.RunMoved, _map.Rooms[index]);
        _fade.Modulate = Colors.White;
        CreateTween().TweenProperty(_fade, "modulate:a", 0f, FadeSeconds);
        EnterRoom(index, (dx, dy) switch
        {
            (> 0, _) => Entry.West,
            (< 0, _) => Entry.East,
            (_, < 0) => Entry.Bottom,
            _ => Entry.Top,
        });
    }

    /// <summary>Where Lumi comes into a room.</summary>
    private enum Entry
    {
        Start,
        West,
        East,

        /// <summary>Falling in through the top gap.</summary>
        Top,

        /// <summary>Climbing up through the open bottom of a sky room.</summary>
        Bottom,
    }

    private void EnterRoom(int index, Entry entry)
    {
        _map.Enter(index);
        _player.EnterRoom();
        _arrivalTicks = ArrivalTicks;
        _monsters.Clear();
        _gates.Clear();
        _hud.TrackBoss(null);
        _roomNode?.QueueFree();
        _screenLayers.Clear();
        _room = _run.Rooms.First(room => room.Id == _map.CurrentRoom);
        _roomNode = new Node2D();
        AddChild(_roomNode);
        foreach (var layer in _room.Layers)
        {
            var added = StageBuilder.AddLayer(RoomNode, layer, _room.Height);
            if (added.IgnoreCameraScroll)
            {
                _screenLayers.Add(added);
            }
        }

        StageBuilder.AddBounds(RoomNode, _room.Width, _room.GroundY, _room.HasFloor);
        foreach (var platform in _room.Platforms)
        {
            StageBuilder.AddPlatform(RoomNode, platform);
        }

        _camera.LimitRight = (int)_room.Width;
        _camera.LimitBottom = (int)_room.Height;
        // with a way up, the camera may look above the ceiling so Lumi on the top ledge is not under the HUD
        _camera.LimitTop = _room.Up is null ? 0 : -(int)CeilingHeadroom;
        var ground = _room.GroundY - FloorClearance;
        var spawn = entry switch
        {
            Entry.West => new Vector2(_run.EdgeSpawnInset, ground),
            Entry.East => new Vector2(_room.Width - _run.EdgeSpawnInset, ground),
            Entry.Top when _room.Up is { } up => new Vector2(up.SpawnX, up.SpawnY),
            Entry.Bottom when _room.Down is { } down => new Vector2(down.SpawnX, down.SpawnY - FloorClearance),
            _ => new Vector2(_run.PlayerSpawnX, ground),
        };
        _player.SetRespawnPoint(spawn);
        _player.MoveTo(spawn);
        _camera.ResetSmoothing();

        _waves = new WaveRunner(_room.Waves.Count, _run.WaveDelayTicks);
        _turns = new AttackTurns(GameServices.Data.Feel.MonsterCrowd.MaxAttackers);
        _challengeSettled = false;
        _challenge = _room.Challenge is { } trial && !State.Cleared ? new ChallengeRun(trial) : null;
        _shade.Color = Colors.White;
        if (_room.Waves.Count == 0 && _room.ShrineX is null)
        {
            State.Cleared = true; // nothing to heal here
        }

        BuildProps();
        var messages = GameServices.Messages;
        _hud.ShowStatus(messages.Get(InBossRoom ? MessageKeys.HudBoss : MessageKeys.RunArea));
        if (State.Cleared || _challenge is { NeedsLever: true })
        {
            SetOpen(true);
            if (_challenge is { NeedsLever: true })
            {
                _hud.ShowStatus(messages.Get(MessageKeys.TrialOffer));
            }
        }
        else if (_room.ShrineX is not null)
        {
            SetOpen(false);
            _hud.ShowStatus(messages.Get(MessageKeys.RunShrinePrompt));
        }
        else
        {
            SetOpen(false);
            _waves.Start();
        }

        ShowMinimap();
    }

    /// <summary>Gate pillars stand on the edges that lead somewhere while the room is shut.</summary>
    private void SetOpen(bool open)
    {
        _open = open;
        foreach (var gate in _gates)
        {
            gate.QueueFree();
        }

        _gates.Clear();
        if (open)
        {
            return;
        }

        foreach (var side in new[] { -1, 1 })
        {
            if (_map.Neighbor(_map.Current, side) < 0)
            {
                continue;
            }

            var texture = GameServices.Texture(_run.GateSprite);
            var gate = new Sprite2D
            {
                Texture = texture,
                Scale = Vector2.One * _run.GateHeight / texture.GetHeight(),
                Position = new Vector2(side < 0 ? GateInset : _room.Width - GateInset, _room.GroundY - _run.GateHeight / 2),
                ZIndex = 4,
            };
            RoomNode.AddChild(gate);
            _gates.Add(gate);
        }
    }

    private void StepExits()
    {
        var (x, y) = (_player.GlobalPosition.X, _player.GlobalPosition.Y);
        if (y > _room.Height + _run.VerticalExitMargin && (!_open || _room.Down is null || _map.Neighbor(_map.Current, 0, 1) < 0))
        {
            _player.MoveTo(_player.RespawnPoint); // fell where no room is: back to where she came in
            return;
        }

        if (!_open || --_arrivalTicks > 0)
        {
            return;
        }

        var side = x < _run.ExitZone ? -1 : x > _room.Width - _run.ExitZone ? 1 : 0;
        if (side != 0 && Mathf.Sign(_player.MoveInput) == side)
        {
            GoTo(_map.Neighbor(_map.Current, side), side, 0);
        }
        else if (y < -_run.VerticalExitMargin && _room.Up is { } up && Mathf.Abs(x - up.X) <= up.Width / 2)
        {
            GoTo(_map.Neighbor(_map.Current, 0, -1), 0, -1);
        }
        else if (y > _room.Height + _run.VerticalExitMargin && _room.Down is not null)
        {
            GoTo(_map.Neighbor(_map.Current, 0, 1), 0, 1);
        }
    }

    private void OpenMap()
    {
        var messages = GameServices.Messages;
        var legend = new List<(string, Color)>
        {
            (messages.Get(MessageKeys.MapHere), new Color("f29bbd")),
            (messages.Get(MessageKeys.MapShrine), ShrineColor),
            (messages.Get(MessageKeys.MapTreasure), ChestColor),
            (messages.Get(MessageKeys.MapLair), LairColor),
            (messages.Get(MessageKeys.MapTrial), TrialColor),
            (messages.Get(MessageKeys.MapElite), EliteColor),
            (messages.Get(MessageKeys.MapBoss), BossColor),
        };
        _mapScreen.Open(_map, messages.Get(MessageKeys.RunArea), messages.Get(MessageKeys.MapHint), Accents(), legend, Warp);
    }

    /// <summary>Warp from an open room to a healed room already visited (Little Noah allows the same).</summary>
    private bool Warp(int index)
    {
        if (!_open || !_map.State(index).Visited || !_map.State(index).Cleared)
        {
            return false;
        }

        GameLog.Info(MessageKeys.RunMoved, _map.Rooms[index]);
        _fade.Modulate = Colors.White;
        CreateTween().TweenProperty(_fade, "modulate:a", 0f, FadeSeconds);
        EnterRoom(index, Entry.West);
        return true;
    }

    private void ShowMinimap() => _minimap.Show(_map, GameServices.Messages.Get(MessageKeys.RunArea), Accents());

    private Dictionary<int, Color> Accents()
    {
        var colors = new Dictionary<int, Color>();
        for (var i = 0; i < _map.Rooms.Count; i++)
        {
            var room = _run.Rooms.First(item => item.Id == _map.Rooms[i]);
            Color? accent = room.Kind == RoomKinds.Boss ? BossColor
                : room.Kind == RoomKinds.Elite ? EliteColor
                : room.Challenge?.Kind == ChallengeKinds.Lair ? LairColor
                : room.Challenge is not null ? TrialColor
                : room.ChestX is not null ? ChestColor
                : room.ShrineX is not null ? ShrineColor
                : null;
            if (accent is { } color)
            {
                colors[i] = color;
            }
        }

        return colors;
    }

    private void BuildProps()
    {
        var pickups = _run.Pickups;
        var crates = _room.Crates ?? [];
        for (var i = 0; i < crates.Count; i++)
        {
            if (State.BrokenCrates.Contains(i))
            {
                continue;
            }

            var index = i;
            var crate = new HittableProp();
            RoomNode.AddChild(crate);
            crate.Setup(pickups.CrateSprite, pickups.CrateHeight, new Vector2(crates[i], _room.GroundY), pickups.CrateHits);
            crate.Broken += prop =>
            {
                State.BrokenCrates.Add(index);
                BreakCrate(prop);
            };
        }

        if (_room.Challenge is { Kind: ChallengeKinds.Lair } trial)
        {
            var lever = new HittableProp();
            RoomNode.AddChild(lever);
            lever.Setup(pickups.LeverSprite, pickups.LeverHeight, new Vector2(trial.LeverX, _room.GroundY), State.Cleared ? 0 : 1);
            if (State.Cleared)
            {
                lever.ChangeSprite(pickups.LeverOnSprite);
            }

            lever.Broken += StartChallenge;
        }

        if (_room.ShrineX is { } shrineX)
        {
            var shrine = new HittableProp();
            RoomNode.AddChild(shrine);
            shrine.Setup(_run.ShrineSprite, _run.ShrineHeight, new Vector2(shrineX, _room.GroundY), State.Cleared ? 0 : 1);
            if (State.Cleared)
            {
                shrine.ChangeSprite(_run.ShrineUsedSprite);
            }

            shrine.Broken += _ =>
            {
                AudioService.Play(SoundIds.Shrine);
                OfferReward(RewardKind.Skills, () =>
                {
                    shrine.ChangeSprite(_run.ShrineUsedSprite);
                    State.Cleared = true;
                    SetOpen(true);
                    ShowMinimap();
                });
            };
        }

        if (_room.ChestX is not null)
        {
            State.Chests.TryAdd(RoomChest, false);
        }

        foreach (var name in State.Chests.Keys.ToList())
        {
            SpawnChest(name);
        }

        for (var i = 0; i < (_room.Keys?.Count ?? 0); i++)
        {
            SpawnKey(i, new Vector2(_room.Keys![i].X, _room.Keys[i].Y));
        }

        if (_room.KeyOnClear && State.Cleared)
        {
            SpawnKey(RoomState.ClearKey, new Vector2(_room.Width / 2, _room.GroundY));
        }
    }

    private void SpawnChest(string name)
    {
        var pickups = _run.Pickups;
        var (x, height) = name switch
        {
            LairChest => (_room.Challenge!.ChestX, pickups.BigChestHeight),
            BonusChest => (_room.Challenge!.ChestX, pickups.ChestHeight),
            _ => (_room.ChestX ?? 0, pickups.ChestHeight),
        };
        var feet = name == RoomChest ? _room.ChestY ?? _room.GroundY : _room.GroundY;
        var opened = State.Chests[name];
        var locked = name == RoomChest && _room.ChestLocked;
        var chest = new HittableProp();
        RoomNode.AddChild(chest);
        chest.Setup(locked ? pickups.LockedChestSprite : pickups.ChestSprite, height, new Vector2(x, feet), opened ? 0 : 1);
        if (opened)
        {
            chest.ChangeSprite(pickups.ChestOpenSprite);
        }

        if (locked)
        {
            // a locked chest takes one key and gives a skill or an amulet
            chest.CanOpen = () => _keys > 0;
            chest.Refused += () => _hud.ShowStatus(GameServices.Messages.Get(MessageKeys.RunChestLocked));
        }

        chest.Broken += prop =>
        {
            State.Chests[name] = true;
            AudioService.Play(SoundIds.ChestOpen);
            prop.ChangeSprite(pickups.ChestOpenSprite);
            if (locked)
            {
                _keys--;
                GameLog.Info(MessageKeys.RunKeyUsed, _keys);
            }

            OfferReward(locked ? RewardKind.Any : RewardKind.Amulets);
        };
    }

    /// <summary>A key lying in the room (or dropped on clear); taking it adds to the run's keys.</summary>
    private void SpawnKey(int index, Vector2 feet)
    {
        if (State.TakenKeys.Contains(index))
        {
            return;
        }

        var key = new Pickup();
        RoomNode.AddChild(key);
        key.Setup(_player, _run.Pickups, TokenKind.Key, feet + new Vector2(0, -TokenLift));
        var state = State;
        key.Taken = () =>
        {
            state.TakenKeys.Add(index);
            _keys++;
            GameLog.Info(MessageKeys.RunKeyFound, _keys);
            _hud.ShowStatus(GameServices.Messages.Get(MessageKeys.RunKeyFound, _keys));
        };
    }

    private void AddChest(string name)
    {
        State.Chests[name] = false;
        SpawnChest(name);
    }

    private void BreakCrate(HittableProp crate)
    {
        var token = new Pickup();
        RoomNode.AddChild(token);
        AudioService.Play(SoundIds.CrateBreak);
        token.Setup(_player, _run.Pickups, _random.NextDouble() < _run.Pickups.HpChance ? TokenKind.Hp : TokenKind.Mp, crate.GlobalPosition + new Vector2(0, -TokenLift));
        crate.ChangeSprite(_run.Pickups.CrateBrokenSprite);
        crate.FadeAway(BrokenCrateSeconds);
    }

    /// <summary>Accepting a lair darkens the room and shuts its edges until every monster is healed.</summary>
    private void StartChallenge(HittableProp lever)
    {
        lever.ChangeSprite(_run.Pickups.LeverOnSprite);
        AudioService.Play(SoundIds.Lever);
        if (_challenge?.Start() != true)
        {
            return;
        }

        AudioService.Play(SoundIds.GateShut);

        GameLog.Info(MessageKeys.TrialStarted, _challenge.Def.Kind);
        SetOpen(false);
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
                    MarkCleared();
                    CreateTween().TweenProperty(_shade, "color", Colors.White, ShadeSeconds);
                }

                AddChest(trial.NeedsLever ? LairChest : BonusChest);
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
            var monster = MonsterSpawner.Spawn(RoomNode, spawn.Monster, spawn.X, spawn.Y ?? _room.GroundY, _run.Level + (_challenge?.Def.LevelBonus ?? 0), _gameFeel, _player);
            monster.SetRoomFloor(_room.GroundY);
            monster.ShareTurns(_turns);
            monster.Healed += OnHealed;
            _monsters.Add(monster);
            // the mini boss is the first monster of an elite room; it shows on the big bar like a boss
            if (InBossRoom || (_room.Kind == RoomKinds.Elite && monster == _monsters[0]))
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

    private void MarkCleared()
    {
        State.Cleared = true;
        _stats.RoomCleared();
        if (!_open)
        {
            AudioService.Play(SoundIds.GateOpen);
        }

        SetOpen(true);
        ShowMinimap();
    }

    /// <summary>Every wave healed: the gates open, then the reward pick; the boss ends the run.</summary>
    private void OnRoomHealed()
    {
        MarkCleared();
        _hud.TrackBoss(null);
        if (InBossRoom)
        {
            GameLog.Info(MessageKeys.RunCleared);
            _hud.ShowStatus(GameServices.Messages.Get(MessageKeys.HudClear));
            _clearScreenTicks = ClearScreenDelayTicks;
            return;
        }

        _hud.ShowStatus(GameServices.Messages.Get(MessageKeys.RunRoomHealed));
        if (_room.KeyOnClear)
        {
            SpawnKey(RoomState.ClearKey, new Vector2(_player.GlobalPosition.X, _room.GroundY));
        }
        AudioService.Play(SoundIds.RoomHealed);
        if (_room.RewardOnClear && !State.RewardTaken)
        {
            State.RewardTaken = true;
            OfferReward(RewardKind.Any);
        }
    }

    /// <summary>Freezes the room behind a summary; attack starts the next run.</summary>
    private void ShowResult(bool cleared)
    {
        var messages = GameServices.Messages;
        AudioService.Play(cleared ? SoundIds.RunClear : SoundIds.RunDefeat);
        if (!cleared)
        {
            GameLog.Info(MessageKeys.RunDefeated, _map.Current + 1);
        }

        var picked = _player.Loadout.Slots.OfType<SkillDef>().Select(skill => messages.Get(MessageKeys.SkillPrefix + skill.Id))
            .Concat(_player.Modifiers.Amulets.Select(amulet => messages.Get(MessageKeys.AmuletPrefix + amulet.Id)));
        var lines = new List<string>
        {
            messages.Get(MessageKeys.ResultTime, _stats.Time((int)GameConstants.TicksPerSecond)),
            messages.Get(MessageKeys.ResultRooms, _stats.RoomsCleared, _map.Rooms.Count),
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
            messages.Get(cleared ? MessageKeys.ResultClearLine : MessageKeys.ResultDefeatLine, _map.Current + 1, _map.Rooms.Count),
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
    private void OfferReward(RewardKind kind, System.Action? onPicked = null)
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
        var picks = RewardDraw.Pick(pool, _run.RewardChoices, _random);
        if (picks.Count == 0)
        {
            return;
        }

        var messages = GameServices.Messages;
        var skillSet = data.Character(GameConstants.PlayerCharacterId).Combat.SkillSet;
        var cards = picks.Select(key =>
        {
            var isSkill = key.StartsWith(MessageKeys.SkillPrefix, System.StringComparison.Ordinal);
            var body = messages.Get(key + MessageKeys.DescriptionSuffix) + "\n" + (isSkill
                ? messages.Get(MessageKeys.RewardSkill, skills.First(skill => MessageKeys.SkillPrefix + skill.Id == key).MpCost)
                : messages.Get(MessageKeys.RewardAmulet));
            var picture = isSkill
                ? GameIcons.Skill(skillSet, key[MessageKeys.SkillPrefix.Length..])
                : GameIcons.Amulet(_run.AmuletSet, key[MessageKeys.AmuletPrefix.Length..]);
            return (messages.Get(key), body, (Texture2D?)picture);
        }).ToList();
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
            onPicked?.Invoke();
        });
        if (AutoplayRunner.RequestedScript() is not null && AutoplayRunner.PicksRewards)
        {
            _picker.Choose(0);
        }
    }
}
