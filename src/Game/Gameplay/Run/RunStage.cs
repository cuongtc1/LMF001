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
    private const string ClearChest = "clear";
    private const float ClearChestEdge = 320;

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

    /// <summary>Gold carried in this run; lost on a defeat (Little Noah: "all gold will be lost").</summary>
    private RunWallet _wallet = new();

    /// <summary>Gold of coins still flying to Lumi; the HUD counts a coin when it arrives.</summary>
    private int _goldInFlight;
    private const float CoinSpreadX = 380;
    private const float CoinPopY = 900;
    private const float CoinPileScale = 1.7f;

    private Sprite2D? _merchant;
    private bool _merchantGreeted;
    private int _merchantPoseTicks;
    private const float MerchantGreetDistance = 700;
    private const int MerchantPoseTicks = 90;
    private const float ShopIconHeight = 96;
    private const float ShopIconLift = 70;
    private const float ShopPriceLift = 40;
    private const int ShopPriceFont = 30;
    private static readonly Color ShopColor = new("52c49a");
    private static readonly Color GoldColor = new("ffd34d");
    private readonly List<Node2D> _gates = [];
    private readonly RunStats _stats = new();
    private RunDef _run = null!;
    private RoomDef _room = null!;
    private StageMap _map = new([]);
    private SkillLoadoutScreen _loadoutScreen = null!;

    /// <summary>The area Lumi is in (null: the run is one map without areas).</summary>
    private AreaDef? _area;

    /// <summary>Rooms of earlier areas, so the next area never repeats one.</summary>
    private HashSet<string> _usedRooms = [];
    private const float DoorLabelLift = 60;
    private const int DoorLabelFont = 30;
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
        _hud.Gold = () => _wallet.Gold - _goldInFlight;
        _hud.GoldSprite = _run.Shop.CoinSprites[0];
        _hud.Setup(_player);
        _minimap = new Minimap();
        AddChild(_minimap);
        _mapScreen = new MapScreen();
        AddChild(_mapScreen);
        _loadoutScreen = new SkillLoadoutScreen();
        AddChild(_loadoutScreen);

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
            _wallet.Earn(autoplay.StartGold);
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
        StepMerchant();
    }

    private void StartRun()
    {
        var seed = AutoplayRunner.RequestedScript() is null ? Time.GetTicksUsec() : GameConstants.ScriptedRunSeed;
        _random = new SeededRandom(seed);
        AutoplayRunner.ClimbTarget = () =>
            _room.Up is { } up && _map.Neighbor(_map.Current, 0, -1) is var above && above >= 0 && !_map.State(above).Visited
                ? new Vector2(up.X, -80)
                : null;
        _usedRooms = [];
        _area = _run.Areas?[0];
        _map = _area is null
            ? StageMap.Generate(_run, seed, GameServices.MerchantMet)
            : StageMap.Generate(_run, _area.Layouts, _random, GameServices.MerchantMet, _usedRooms);
        _wallet = new RunWallet();
        _player.Loadout.Clear();
        EquipSkills();
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
        if (_room.Waves.Count == 0)
        {
            State.Cleared = true; // nothing to heal here (the shrine changes skills and never locks the room)
        }

        _goldInFlight = 0; // coins of the last room were freed with it; the wallet already has their gold
        BuildProps();
        BuildShop();
        BuildDoors();
        var messages = GameServices.Messages;
        _hud.ShowStatus(InBossRoom ? messages.Get(MessageKeys.HudBoss) : AreaName);
        if (_room.ShrineX is not null)
        {
            _hud.ShowStatus(messages.Get(MessageKeys.RunShrinePrompt));
        }

        if (State.Cleared || _challenge is { NeedsLever: true })
        {
            SetOpen(true);
            if (_challenge is { NeedsLever: true })
            {
                _hud.ShowStatus(messages.Get(MessageKeys.TrialOffer));
            }
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
            (messages.Get(MessageKeys.MapShop), ShopColor),
            (messages.Get(MessageKeys.MapBoss), BossColor),
        };
        _mapScreen.Open(_map, AreaName, messages.Get(MessageKeys.MapHint), Accents(), legend, Warp);
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

    private void ShowMinimap() => _minimap.Show(_map, AreaName, Accents());

    /// <summary>The area's own name over the map (Little Noah shows "Breezy Fields"), or the run's name without areas.</summary>
    private string AreaName => GameServices.Messages.Get(_area is null ? MessageKeys.RunArea : MessageKeys.AreaPrefix + _area.Id + MessageKeys.AreaTitleSuffix);

    private Dictionary<int, Color> Accents()
    {
        var colors = new Dictionary<int, Color>();
        for (var i = 0; i < _map.Rooms.Count; i++)
        {
            var room = _run.Rooms.First(item => item.Id == _map.Rooms[i]);
            Color? accent = room.Kind == RoomKinds.Boss ? BossColor
                : room.Kind == RoomKinds.Elite ? EliteColor
                : room.Kind == RoomKinds.Shop ? ShopColor
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
            shrine.Setup(_run.ShrineSprite, _run.ShrineHeight, new Vector2(shrineX, _room.GroundY), 1);
            // H2 (round 75): the shrine changes the equipped skills, as often as Lumi likes, before she leaves
            shrine.CanOpen = () =>
            {
                OpenLoadout();
                return false;
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
            ClearChest => (State.ClearChestX, pickups.ChestHeight),
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

            GiveReward(locked ? RewardKind.Any : RewardKind.Amulets);
        };
    }

    /// <summary>
    /// The Raccoon's stall: items on stands with their price above (Little Noah, 26:15). Hitting a stand opens a card
    /// with the item, its price and the gold left; "Leave it" is selected first, so a mashed combo never buys.
    /// </summary>
    private void BuildShop()
    {
        _merchant = null;
        if (_room.Kind != RoomKinds.Shop)
        {
            return;
        }

        var shop = _run.Shop;
        var data = GameServices.Data;
        var skills = _player.Loadout.Upgradable.Select(skill => skill.Id).ToList(); // H2: the stall sells the next level
        var amulets = data.AmuletSet(_run.AmuletSet).Amulets.Where(amulet => !_player.Modifiers.Has(amulet.Id)).Select(amulet => amulet.Id).ToList();
        if (State.Stock.Count == 0)
        {
            State.Stock.AddRange(ShopStock.Draw(shop, skills, amulets, _random));
        }
        else
        {
            ShopStock.Refresh(State.Stock, State.Sold, skills, amulets, _random);
        }

        var cart = GameServices.Texture(shop.CartSprite);
        RoomNode.AddChild(new Sprite2D
        {
            Texture = cart,
            Scale = Vector2.One * shop.CartHeight / cart.GetHeight(),
            Position = new Vector2(_room.MerchantX!.Value + shop.CartOffset, _room.GroundY - shop.CartHeight / 2),
            ZIndex = -4,
        });
        var merchant = GameServices.Texture(shop.MerchantSprite);
        _merchant = new Sprite2D
        {
            Texture = merchant,
            Scale = Vector2.One * shop.MerchantHeight / merchant.GetHeight(),
            Position = new Vector2(_room.MerchantX!.Value, _room.GroundY - shop.MerchantHeight / 2),
            ZIndex = -3,
        };
        RoomNode.AddChild(_merchant);
        _merchantGreeted = false;
        _merchantPoseTicks = 0;
        for (var i = 0; i < State.Stock.Count && i < _room.Stands!.Count; i++)
        {
            AddStand(i, State.Stock[i], _room.Stands[i]);
        }

        _hud.ShowStatus(GameServices.Messages.Get(MessageKeys.ShopWelcome));
    }

    private void AddStand(int index, ShopItem item, float x)
    {
        var shop = _run.Shop;
        var sold = State.Sold.Contains(index);
        var stand = new HittableProp();
        RoomNode.AddChild(stand);
        stand.Setup(shop.StandSprite, shop.StandHeight, new Vector2(x, _room.GroundY), sold ? 0 : 1);
        if (sold)
        {
            stand.ChangeSprite(shop.SoldStandSprite);
        }

        var top = _room.GroundY - shop.StandHeight;
        var icon = GameServices.Texture(ItemSprite(item));
        var picture = new Sprite2D
        {
            Texture = icon,
            Scale = Vector2.One * ShopIconHeight / icon.GetHeight(),
            Position = new Vector2(x, top - ShopIconLift),
            Visible = !sold,
            ZIndex = 4,
        };
        RoomNode.AddChild(picture);
        var price = UiCommon.OutlinedLabel(ShopPriceFont, sold ? GameServices.Messages.Get(MessageKeys.ShopSoldOut) : item.Price.ToString());
        price.AddThemeColorOverride("font_color", GoldColor);
        price.Position = new Vector2(x - 40, top - ShopIconLift - ShopIconHeight / 2 - ShopPriceLift - ShopPriceFont);
        price.ZIndex = 4;
        RoomNode.AddChild(price);
        stand.CanOpen = () =>
        {
            OfferItem(index, item, stand);
            return false;
        };
        stand.Broken += _ =>
        {
            stand.ChangeSprite(shop.SoldStandSprite);
            picture.Visible = false;
            price.Text = GameServices.Messages.Get(MessageKeys.ShopSoldOut);
        };
    }

    private void EquipSkills()
    {
        var set = GameServices.Data.SkillSet(_player.SkillSetId);
        foreach (var id in GameServices.EquippedSkills ?? set.DefaultLoadout ?? [])
        {
            _player.Loadout.Add(set.Skills.First(skill => skill.Id == id));
        }
    }

    private void OpenLoadout()
    {
        if (_rewardPending)
        {
            return;
        }

        AudioService.Play(SoundIds.Shrine);
        _rewardPending = true;
        _player.InputPaused = true;
        var set = GameServices.Data.SkillSet(_player.SkillSetId);
        var equipped = _player.Loadout.Slots.OfType<SkillDef>().Select(skill => skill.Id).ToList();
        _loadoutScreen.Open(set, equipped, chosen =>
        {
            GameServices.EquippedSkills = chosen;
            _player.Loadout.Clear();
            EquipSkills();
            _rewardPending = false;
            _player.InputPaused = false;
        });
    }

    /// <summary>
    /// The gate room at the end of an area: one door per next area with its name above (Little Noah 18:55). Hitting a
    /// door leads into that area's first room; the area's own map replaces the old one.
    /// </summary>
    private void BuildDoors()
    {
        if (_room.Kind != RoomKinds.Gate || _area is null || _room.Doors is null)
        {
            return;
        }

        var messages = GameServices.Messages;
        for (var i = 0; i < _area.Next.Count && i < _room.Doors.Count; i++)
        {
            var next = _area.Next[i];
            var x = _room.Doors[i];
            var door = new HittableProp();
            RoomNode.AddChild(door);
            var art = _run.Areas!.First(area => area.Id == next).DoorSprite;
            var height = art is null ? _run.GateHeight : _run.DoorHeight;
            door.Setup(art ?? _run.GateSprite, height, new Vector2(x, _room.GroundY), 1);
            var label = UiCommon.OutlinedLabel(DoorLabelFont, messages.Get(MessageKeys.AreaPrefix + next));
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Size = new Vector2(420, 0);
            label.Position = new Vector2(x - 210, _room.GroundY - height - DoorLabelLift - DoorLabelFont);
            label.ZIndex = 4;
            RoomNode.AddChild(label);
            door.Broken += _ => EnterArea(next);
        }

        _hud.ShowStatus(messages.Get(MessageKeys.AreaChoose));
    }

    private void EnterArea(string id)
    {
        _area = _run.Areas!.First(area => area.Id == id);
        foreach (var room in _map.Rooms)
        {
            _usedRooms.Add(room);
        }

        _map = StageMap.Generate(_run, _area.Layouts, _random, GameServices.MerchantMet, _usedRooms);
        GameLog.Info(MessageKeys.AreaEntered, id, string.Join(' ', _map.Rooms));
        _fade.Modulate = Colors.White;
        CreateTween().TweenProperty(_fade, "modulate:a", 0f, FadeSeconds);
        EnterRoom(0, Entry.Start);
    }

    /// <summary>The Raccoon waves when Lumi first comes near and holds up a coin after each sale, then stands idle.</summary>
    private void StepMerchant()
    {
        if (_merchant is null)
        {
            return;
        }

        if (!_merchantGreeted && Mathf.Abs(_player.GlobalPosition.X - _merchant.GlobalPosition.X) < MerchantGreetDistance)
        {
            _merchantGreeted = true;
            SetMerchantPose(_run.Shop.MerchantWaveSprite);
        }

        if (_merchantPoseTicks > 0 && --_merchantPoseTicks == 0)
        {
            _merchant.Texture = GameServices.Texture(_run.Shop.MerchantSprite);
        }
    }

    private void SetMerchantPose(string sprite)
    {
        if (_merchant is null)
        {
            return;
        }

        _merchant.Texture = GameServices.Texture(sprite);
        _merchantPoseTicks = MerchantPoseTicks;
    }

    /// <summary>
    /// Gold pops out as coins that fly to Lumi. The wallet gets it at once (leaving the room loses nothing); the HUD
    /// counts each coin as it arrives.
    /// </summary>
    private void DropGold(Vector2 position, int gold, bool pile)
    {
        _wallet.Earn(gold);
        var shop = _run.Shop;
        var count = Mathf.Clamp(Mathf.CeilToInt(gold / (float)shop.CoinValue), 1, shop.MaxCoins);
        for (var i = 0; i < count; i++)
        {
            var amount = gold / count + (i < gold % count ? 1 : 0);
            var big = pile && i == 0;
            var coin = new GoldCoin();
            RoomNode.AddChild(coin);
            var velocity = new Vector2((GD.Randf() * 2 - 1) * CoinSpreadX, -CoinPopY * (0.7f + 0.3f * GD.Randf()));
            coin.Setup(_player, big ? [shop.CoinPileSprite] : shop.CoinSprites, big ? shop.CoinHeight * CoinPileScale : shop.CoinHeight, amount, position, velocity);
            _goldInFlight += amount;
            coin.Arrived = arrived => _goldInFlight -= arrived;
        }
    }

    private string ItemSprite(ShopItem item) => item.Kind switch
    {
        ShopItemKinds.Skill => $"{GameServices.Data.SkillSet(_player.SkillSetId).IconFolder}/{item.Id}.png",
        ShopItemKinds.Amulet => $"{GameServices.Data.AmuletSet(_run.AmuletSet).IconFolder}/{item.Id}.png",
        ShopItemKinds.Heal => _run.Shop.HealSprite,
        _ => _run.Pickups.KeySprite,
    };

    private string ItemName(ShopItem item)
    {
        var messages = GameServices.Messages;
        return item.Kind switch
        {
            ShopItemKinds.Skill => messages.Get(MessageKeys.SkillLevelName, messages.Get(MessageKeys.SkillPrefix + item.Id), _player.Loadout.LevelOf(item.Id) + 1),
            ShopItemKinds.Amulet => messages.Get(MessageKeys.AmuletPrefix + item.Id),
            ShopItemKinds.Heal => messages.Get(MessageKeys.ShopHeal),
            _ => messages.Get(MessageKeys.ShopKey),
        };
    }

    /// <summary>Shows the item card: "Leave it" or "Buy"; buying spends gold and opens the stand.</summary>
    private void OfferItem(int index, ShopItem item, HittableProp stand)
    {
        if (_rewardPending)
        {
            return;
        }

        var messages = GameServices.Messages;
        var name = ItemName(item);
        var description = item.Kind switch
        {
            ShopItemKinds.Skill => messages.Get(MessageKeys.SkillPrefix + item.Id + MessageKeys.SkillLevelSuffix + (_player.Loadout.LevelOf(item.Id) + 1)),
            ShopItemKinds.Amulet => messages.Get(MessageKeys.AmuletPrefix + item.Id + MessageKeys.DescriptionSuffix),
            ShopItemKinds.Heal => messages.Get(MessageKeys.ShopHealDescription, Mathf.RoundToInt(_run.Shop.HealShare * 100)),
            _ => messages.Get(MessageKeys.ShopKeyDescription),
        };
        var cards = new List<(string, string, Texture2D?)>
        {
            (messages.Get(MessageKeys.ShopLeave), string.Empty, null),
            (name, description + "\n" + messages.Get(MessageKeys.ShopItem, item.Price, _wallet.Gold), GameServices.Texture(ItemSprite(item))),
        };
        _rewardPending = true;
        _player.InputPaused = true;
        _picker.Open(cards, choice =>
        {
            _rewardPending = false;
            _player.InputPaused = false;
            if (choice == 1 && Buy(index, item))
            {
                stand.Open();
            }
        });
    }

    private bool Buy(int index, ShopItem item)
    {
        var messages = GameServices.Messages;
        if (!_wallet.TrySpend(item.Price))
        {
            _hud.ShowStatus(messages.Get(MessageKeys.ShopPoor, item.Price));
            return false;
        }

        var data = GameServices.Data;
        switch (item.Kind)
        {
            case ShopItemKinds.Skill:
                _player.Loadout.LevelUp(item.Id);
                break;
            case ShopItemKinds.Amulet:
                _player.Modifiers.Add(data.AmuletSet(_run.AmuletSet).Amulets.First(amulet => amulet.Id == item.Id));
                _player.ApplyModifiers();
                break;
            case ShopItemKinds.Heal:
                _player.Health.Heal(Mathf.RoundToInt(_player.Health.Max * _run.Shop.HealShare));
                break;
            default:
                _keys++;
                break;
        }

        State.Sold.Add(index);
        SetMerchantPose(_run.Shop.MerchantCoinSprite);
        AudioService.Play(SoundIds.UiConfirm);
        GameLog.Info(MessageKeys.ShopBought, ItemName(item), _wallet.Gold);
        _hud.ShowStatus(messages.Get(MessageKeys.ShopBought, ItemName(item), _wallet.Gold));
        return true;
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
        DropGold(crate.GlobalPosition + new Vector2(0, -TokenLift), _run.Shop.CrateGold, false);
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
            AddMonster(monster);
            // the mini boss is the first monster of an elite room; it shows on the big bar like a boss
            if (InBossRoom || (_room.Kind == RoomKinds.Elite && monster == _monsters[0]))
            {
                _hud.TrackBoss(monster.Health);
            }
        }
    }

    private void AddMonster(MonsterController monster)
    {
        monster.Healed += OnHealed;
        monster.Summoning += (id, at) =>
        {
            // helpers come out of the same light column as every new monster
            var helper = MonsterSpawner.Spawn(RoomNode, id, at.X, _room.GroundY, _run.Level + (_challenge?.Def.LevelBonus ?? 0), _gameFeel, _player);
            helper.SetRoomFloor(_room.GroundY);
            helper.ShareTurns(_turns);
            AddMonster(helper);
        };
        _monsters.Add(monster);
    }

    private void OnHealed(MonsterController monster)
    {
        _monsters.Remove(monster);
        _stats.Healed();
        var gold = monster.IsElite ? _run.Shop.GoldPerElite : _run.Shop.GoldPerMonster;
        DropGold(monster.BodyCenter, gold, monster.IsElite);
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
            GameServices.MerchantMet = true; // the healed Raccoon opens his stall in later runs
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
            // Little Noah 28:40: "FOES DEFEATED!", then a chest appears; opening it gives one random reward, no choice
            State.RewardTaken = true;
            State.ClearChestX = Mathf.Clamp(_player.GlobalPosition.X, ClearChestEdge, _room.Width - ClearChestEdge);
            State.Chests[ClearChest] = false;
            SpawnChest(ClearChest);
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

    /// <summary>
    /// A chest's reward (round 73, Little Noah 28:44): one random skill or amulet, taken at once and shown in a short
    /// card on the HUD, so the run does not stop for reading. Only the skill shrine still offers a choice.
    /// </summary>
    private void GiveReward(RewardKind kind)
    {
        var data = GameServices.Data;
        var skillSet = data.Character(GameConstants.PlayerCharacterId).Combat.SkillSet;
        var pool = RewardPool(kind);
        var picks = RewardDraw.Pick(pool, 1, _random);
        if (picks.Count == 0)
        {
            DropGold(_player.GlobalPosition + new Vector2(0, -200), _run.Shop.CrateGold * 4, true);
            return;
        }

        var key = picks[0];
        var isSkill = key.StartsWith(MessageKeys.SkillPrefix, System.StringComparison.Ordinal);
        Take(key);
        var messages = GameServices.Messages;
        if (isSkill)
        {
            var id = key[MessageKeys.SkillPrefix.Length..];
            var level = _player.Loadout.LevelOf(id);
            _hud.ShowReward(GameIcons.Skill(skillSet, id), messages.Get(MessageKeys.SkillLevelName, messages.Get(key), level), messages.Get(key + MessageKeys.SkillLevelSuffix + level));
            return;
        }

        _hud.ShowReward(GameIcons.Amulet(_run.AmuletSet, key[MessageKeys.AmuletPrefix.Length..]), messages.Get(key), messages.Get(key + MessageKeys.DescriptionSuffix));
    }

    private List<string> RewardPool(RewardKind kind)
    {
        var data = GameServices.Data;
        var skills = data.SkillSet(data.Character(GameConstants.PlayerCharacterId).Combat.SkillSet).Skills;
        var amulets = data.AmuletSet(_run.AmuletSet).Amulets;
        var pool = new List<string>();
        if (kind != RewardKind.Amulets)
        {
            pool.AddRange(_player.Loadout.Upgradable.Select(skill => MessageKeys.SkillPrefix + skill.Id));
        }

        if (kind != RewardKind.Skills)
        {
            pool.AddRange(amulets.Where(amulet => !_player.Modifiers.Has(amulet.Id)).Select(amulet => MessageKeys.AmuletPrefix + amulet.Id));
        }

        return pool;
    }

    private void Take(string key)
    {
        var data = GameServices.Data;
        var skills = data.SkillSet(data.Character(GameConstants.PlayerCharacterId).Combat.SkillSet).Skills;
        var amulets = data.AmuletSet(_run.AmuletSet).Amulets;
        if (key.StartsWith(MessageKeys.SkillPrefix, System.StringComparison.Ordinal))
        {
            _player.Loadout.LevelUp(key[MessageKeys.SkillPrefix.Length..]);
        }
        else
        {
            _player.Modifiers.Add(amulets.First(amulet => MessageKeys.AmuletPrefix + amulet.Id == key));
            _player.ApplyModifiers();
        }

        GameLog.Info(MessageKeys.RewardPicked, key);
    }
}
