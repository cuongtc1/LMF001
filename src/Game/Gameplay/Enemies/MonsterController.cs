using System;
using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Game.Gameplay.Combat;
using LumiFeast.Game.Gameplay.Player;
using LumiFeast.Game.UI;
using LumiFeast.Logic.Combat;
using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;
using LumiFeast.Logic.Enemies;
using LumiFeast.Logic.Stats;

namespace LumiFeast.Game.Gameplay.Enemies;

/// <summary>Runs any plain monster from its data file. New monsters normally need no new code.</summary>
public partial class MonsterController : CharacterBody2D, IDamageable, IBodyBlocker
{
    private const float FlashStrength = 2.2f;
    private const float HealRisePixels = 90;
    private const float BobPixels = 10;
    private const float BobPerTick = 0.09f;
    private const float AuraScale = 1.12f;
    private const float DownSinkShare = 0.25f;
    private const float StaggerBarWidth = 200;
    private const float StaggerBarHeight = 10;
    private const int PosePhaseSpread = 97;
    private const float LedgeMargin = 20;
    private const float ThrowForward = 80;
    /// <summary>Throws leave the hands at this share of the art height.</summary>
    private const float ThrowHeightShare = 0.6f;
    private const float ThrowAimHeight = 100;
    private const float MarkerLift = 30;
    private const int MarkerEveryTicks = 10;
    private const string AuraShaderCode = "shader_type canvas_item; void fragment() { COLOR.rgb = vec3(1.0, 0.12, 0.1); }";

    private static readonly Color TelegraphColor = new(1.4f, 1.2f, 1.1f);
    private static readonly Color DizzyColor = new(1.4f, 1.3f, 0.5f);
    private static readonly Color StickyColor = new(1f, 0.82f, 0.45f);

    private MonsterDef _def = null!;
    private MonsterStats _stats = null!;
    private GameFeelDef _feel = null!;
    private GameFeel _gameFeel = null!;
    private PlayerController _player = null!;
    private MonsterBrain _brain = null!;
    private MonsterPose _pose = null!;
    private PlatformChase? _climb;
    private float _floorY;
    private int _dropTicks;
    private const float RunOffSeconds = 0.6f;

    private AttackHitbox _hitbox = null!;
    private Sprite2D _sprite = null!;
    private Sprite2D _aura = null!;
    private ColorRect? _staggerBar;
    private float _ambushX;
    private bool _wasHidden;
    private int _thrownSwing;
    private Hurtbox _hurtbox = null!;
    private Texture2D _idle = null!;
    private Texture2D _windup = null!;
    private Texture2D _hit = null!;
    private Texture2D _healed = null!;
    private float _spriteScale;
    private float _knockback;
    private float _velocityY;
    private int _flashTicks;
    private bool _healing;
    private bool _flying;
    private float _hoverY;

    /// <summary>Flyers have no floor collision; their feet stop here so a swoop touches the ground but never sinks into it.</summary>
    private float _flyerGroundY;
    private float _slow;
    private int _slowTicks;
    private MonsterAwareness? _awareness;
    private float _homeX;
    private Label _notice = null!;
    private int _noticeTicks;
    private MonsterHealthBar? _healthBar;
    private AttackTurns? _turns;
    private int _appearTicks;
    private int _flankTimer;
    private float _leapVelocityX;

    private static readonly Color Silhouette = new(0.35f, 0.15f, 0.55f, 0f);

    private const string MonsterGroup = "monsters";
    private const int NoticeShowExtraTicks = 12;
    private const float NoticeLift = 70;
    private const float HealthBarMinWidth = 90;
    private const float HealthBarGap = 10;
    /// <summary>Flyers swoop only at Lumi below them, down to about their hover height plus this.</summary>
    private const float FlyerReachBelow = 140;
    private const float FlyerReachAbove = 40;
    /// <summary>Flank leaps only when Lumi stands on the same floor or ledge.</summary>
    private const float FlankSameLevel = 30;
    private int _age;

    public event Action<MonsterController>? Healed;

    public string DefinitionId => _def.Id;

    public Health Health { get; private set; } = null!;

    private bool IsHidden => _brain.CurrentMove is { Kind: MonsterMoveKinds.Ambush } && _brain.State == MonsterState.Windup;

    /// <summary>A monster leaping over Lumi passes through her instead of shoving her along (round 64 bug).</summary>
    public bool BlocksBody => !_healing && !_flying && _leapVelocityX == 0 && _appearTicks == 0;

    /// <summary>Still in the fight: not being healed and not hiding (off-screen arrows point only at these).</summary>
    public bool IsActive => !_healing && !IsHidden && _appearTicks == 0;

    /// <summary>Super armor monsters (mini bosses) and bosses give more gold.</summary>
    public bool IsElite => _def.Ai.SuperArmor || _def.Ai.Boss;

    /// <summary>Middle of the body, for markers.</summary>
    public Vector2 BodyCenter => GlobalPosition + new Vector2(0, -_def.Art.Height / 2);

    public Vector2 FeetPosition => GlobalPosition;

    public Vector2 BodySize => new(_def.Body.Width, _def.Body.Height);

    public void Setup(MonsterDef def, MonsterStats stats, GameFeelDef feel, GameFeel gameFeel, PlayerController player, Vector2 position)
    {
        _def = def;
        _stats = stats;
        _feel = feel;
        _gameFeel = gameFeel;
        _player = player;
        _brain = new MonsterBrain(def.Ai, stats);
        // monsters of one wave should not breathe in step
        _climb = def.Ai.Climbs ? new PlatformChase(feel.MonsterClimb) : null;
        _floorY = position.Y;
        _pose = new MonsterPose(feel.MonsterPose, def.Art.Motion, Mathf.PosMod((int)position.X, PosePhaseSpread));
        Health = new Health(stats.MaxHp);
        GlobalPosition = position;
        _flying = def.Ai.Behavior == MonsterBehaviors.Flyer;
        _hoverY = position.Y;
        _flyerGroundY = position.Y + def.Ai.HoverHeight;
        CollisionLayer = 0;
        CollisionMask = _flying ? 0 : PhysicsLayers.World | PhysicsLayers.Platform;

        var size = new Vector2(def.Body.Width, def.Body.Height);
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = size }, Position = new Vector2(0, -size.Y / 2) });

        _idle = ArtTexture(def.Art.Idle);
        _windup = ArtTexture(def.Art.Windup);
        _hit = ArtTexture(def.Art.Hit);
        _healed = ArtTexture(def.Art.Healed);
        _spriteScale = def.Art.Height / _idle.GetHeight();
        _sprite = new Sprite2D { Texture = _idle, Position = new Vector2(0, -def.Art.Height / 2) };
        AddChild(_sprite);
        _aura = new Sprite2D
        {
            Scale = Vector2.One * AuraScale,
            ShowBehindParent = true,
            Visible = false,
            Material = new ShaderMaterial { Shader = new Shader { Code = AuraShaderCode } },
        };
        _sprite.AddChild(_aura);

        if (def.Ai.StaggerMax > 0)
        {
            _staggerBar = new ColorRect
            {
                Color = Colors.Gold,
                Position = new Vector2(-StaggerBarWidth / 2, -def.Art.Height - 24),
                Size = new Vector2(0, StaggerBarHeight),
            };
            AddChild(_staggerBar);
        }

        _hurtbox = new Hurtbox();
        AddChild(_hurtbox);
        _hurtbox.Setup(this, size, PhysicsLayers.EnemyHurtbox);
        // rises out of a light column first (bosses make their own entrance)
        if (!def.Ai.Boss && feel.MonsterCrowd.AppearTicks > 0)
        {
            _appearTicks = feel.MonsterCrowd.AppearTicks;
            _hurtbox.Monitorable = false;
            var portal = new SpawnPortal();
            AddChild(portal);
            portal.Setup(_appearTicks);
        }

        _hitbox = new AttackHitbox();
        AddChild(_hitbox);
        _hitbox.Setup(PhysicsLayers.PlayerHurtbox);

        // bosses fight from the start and show their health on the HUD
        var boss = def.Ai.Boss;
        _homeX = position.X;
        _awareness = boss ? null : new MonsterAwareness(feel.MonsterAwareness, player.GlobalPosition.X < position.X ? -1 : 1);
        AddToGroup(MonsterGroup);
        // the first frames (rising out of the light column) are drawn before ShowState sets the size
        var facing = _awareness?.Facing ?? _brain.Facing;
        _sprite.Scale = new Vector2(_spriteScale * (facing == (def.Art.FacesLeft ? -1 : 1) ? 1 : -1), _spriteScale);
        _notice = UiCommon.OutlinedLabel(64, "!");
        _notice.AddThemeColorOverride("font_color", new Color("ff4d4d"));
        _notice.Position = new Vector2(-14, -def.Art.Height - NoticeLift - 40);
        _notice.Visible = false;
        _notice.ZIndex = 20;
        AddChild(_notice);
        if (!boss)
        {
            _healthBar = new MonsterHealthBar();
            AddChild(_healthBar);
            _healthBar.Setup(Health, Mathf.Max(HealthBarMinWidth, def.Body.Width), HealthBarGap);
            _healthBar.Refresh();
        }
    }

    /// <summary>The room floor, when the monster starts on a ledge above it (climbers use it to know they stand on a ledge).</summary>
    public void SetRoomFloor(float groundY)
    {
        if (!_flying)
        {
            _floorY = groundY;
        }
    }

    /// <summary>Joins the room's attack turns (bosses do not: they always attack when ready).</summary>
    public void ShareTurns(AttackTurns turns)
    {
        if (_def.Ai.Boss)
        {
            return;
        }

        _turns = turns;
        _brain.Crowd = _feel.MonsterCrowd;
    }

    /// <summary>A nearby monster saw Lumi: this one notices her too.</summary>
    public void HearAlert()
    {
        if (_awareness is { State: AwarenessState.Unaware } eyes)
        {
            eyes.Alert(_player.GlobalPosition.X - GlobalPosition.X);
            OnNoticed(callOthers: false);
        }
    }

    private void OnNoticed(bool callOthers)
    {
        _noticeTicks = _feel.MonsterAwareness.NoticeTicks + NoticeShowExtraTicks;
        if (!callOthers)
        {
            return;
        }

        foreach (var node in GetTree().GetNodesInGroup(MonsterGroup))
        {
            if (node is MonsterController other && other != this && other.GlobalPosition.DistanceTo(GlobalPosition) <= _feel.MonsterAwareness.AlertRadius)
            {
                other.HearAlert();
            }
        }
    }

    public bool TakeHit(in HitInfo hit)
    {
        if (_healing || IsHidden)
        {
            return false;
        }

        var guarded = _brain.GuardsAgainst(hit.Direction, hit.GuardBreak);
        // a hit travelling the way the monster looks comes from behind it
        var fromBehind = hit.Direction == _brain.Facing;
        var scale = _brain.DamageTakenScale * (guarded ? _def.Ai.FrontGuardScale : 1) * (fromBehind ? hit.BackMultiplier : 1);
        var damage = Mathf.Max(1, Mathf.RoundToInt(DamageCalculator.Compute(hit.Attack, hit.Multiplier, _stats.Defense) * scale));
        if (!_def.Immortal)
        {
            Health.Damage(damage);
        }

        GameLog.Info(MessageKeys.CombatMonsterHit, _def.Id, damage, hit.SourceId, Health.Current, Health.Max);
        _healthBar?.Refresh();
        if (_awareness is { State: AwarenessState.Unaware } eyes)
        {
            eyes.Alert(_player.GlobalPosition.X - GlobalPosition.X);
            OnNoticed(callOthers: true);
        }
        var wasPhaseOne = !_brain.InPhaseTwo;
        if (_brain.RegisterHit((float)Health.Current / Health.Max))
        {
            GameLog.Info(wasPhaseOne && _brain.InPhaseTwo ? MessageKeys.CombatPhaseTwo : MessageKeys.CombatStoic, _def.Id);
        }

        _gameFeel.ShowDamage(GlobalPosition + new Vector2(0, -_def.Art.Height), damage, Colors.White);
        if (_brain.AddStagger(hit.Stagger))
        {
            GameLog.Info(MessageKeys.CombatDizzy, _def.Id);
        }

        _flashTicks = 6;
        _pose.Hit();
        if (hit.SlowTicks > 0)
        {
            _slow = hit.Slow;
            _slowTicks = hit.SlowTicks;
        }

        if (_brain.CanBeMoved && !guarded)
        {
            var direction = hit.PullToX is { } pullX ? Mathf.Sign(pullX - GlobalPosition.X) : hit.Direction;
            _knockback = direction * hit.KnockbackSpeed * _def.KnockbackScale;
            if (hit.LaunchSpeed > 0)
            {
                _velocityY = -hit.LaunchSpeed * _def.KnockbackScale;
            }

            _brain.Stun(hit.StunTicks, hit.Knockdown);
            _hitbox.Close();
        }

        if (Health.IsDead)
        {
            BeginHeal();
        }

        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_gameFeel.Frozen || _healing)
        {
            return;
        }

        if (_appearTicks > 0)
        {
            StepAppear();
            return;
        }

        var onFloor = _flying || IsOnFloor();
        var deltaX = _player.GlobalPosition.X - GlobalPosition.X;
        var deltaY = _player.GlobalPosition.Y - GlobalPosition.Y;
        // bosses throw; flyers swoop only at Lumi below them; ground monsters do not swing at the air
        var canReach = _def.Ai.Moves is not null
            || (_flying ? deltaY >= -FlyerReachAbove && deltaY <= _def.Ai.HoverHeight + FlyerReachBelow : Mathf.Abs(deltaY) <= _feel.MonsterClimb.ReachHeight);
        var fighting = StepAwareness(deltaX, deltaY);
        var wasWindup = _brain.State == MonsterState.Windup;
        var patrol = 0f;
        if (fighting || _brain.State is not (MonsterState.Idle or MonsterState.Chase))
        {
            _brain.Step(deltaX, onFloor, GlobalPosition.Y - _hoverY, canReach, _turns?.MayAttack(this) ?? true);
        }
        else
        {
            _brain.Wait(_awareness!.Facing, GlobalPosition.Y - _hoverY);
            patrol = _awareness.PatrolMove * _stats.Speed;
        }

        if (!wasWindup && _brain.State == MonsterState.Windup && !IsHidden)
        {
            AudioService.Play(SoundIds.MonsterAttack);
            _turns?.Take(this);
        }
        else if (_brain.State is not (MonsterState.Windup or MonsterState.Attack or MonsterState.Recover))
        {
            _turns?.Release(this);
        }

        if (_brain.WantsHop)
        {
            _velocityY = -_def.Ai.HopVelocity;
        }

        if (fighting)
        {
            StepClimb(deltaX, deltaY, onFloor);
        }

        StepFlank(deltaX, onFloor, fighting);

        StepVertical((float)delta, onFloor);
        _knockback = Mathf.MoveToward(_knockback, 0, _feel.KnockbackDecay * (float)delta);
        _slowTicks = Mathf.Max(0, _slowTicks - 1);
        var walk = _leapVelocityX != 0 ? _leapVelocityX : (_brain.MoveVelocityX + patrol) * (_slowTicks > 0 ? 1 - _slow : 1);
        Velocity = new Vector2(walk + _knockback, _velocityY + _brain.MoveVelocityY);
        MoveAndSlide();
        if (_flying && GlobalPosition.Y > _flyerGroundY)
        {
            GlobalPosition = new Vector2(GlobalPosition.X, _flyerGroundY);
            _velocityY = Mathf.Min(_velocityY, 0);
        }

        if (IsOnWall())
        {
            _brain.BounceOffWall();
        }

        StepStrike();
        ShowState();
    }

    /// <summary>A dark shape fades into the monster; then it can be hit and starts to look around.</summary>
    private void StepAppear()
    {
        _appearTicks--;
        var shown = 1 - (float)_appearTicks / _feel.MonsterCrowd.AppearTicks;
        _sprite.Modulate = _appearTicks > 0 ? Silhouette.Lerp(Colors.White, shown * shown) with { A = shown } : Colors.White;
        if (_appearTicks == 0)
        {
            _hurtbox.SetDeferred(Area2D.PropertyName.Monitorable, true);
        }
    }

    /// <summary>
    /// Agile monsters waiting for a turn leap over Lumi when her other side is emptier, so crowds come from both sides.
    /// </summary>
    private void StepFlank(float deltaX, bool onFloor, bool fighting)
    {
        if (_leapVelocityX != 0)
        {
            if (onFloor && _velocityY >= 0)
            {
                _leapVelocityX = 0;
            }

            return;
        }

        var crowd = _feel.MonsterCrowd;
        if (!fighting || !_def.Ai.Climbs || _turns is null || _turns.MayAttack(this) || !onFloor || --_flankTimer > 0)
        {
            return;
        }

        _flankTimer = crowd.FlankCheckTicks;
        if (!_player.OnGround || Mathf.Abs(_player.GlobalPosition.Y - GlobalPosition.Y) > FlankSameLevel)
        {
            return;
        }

        var side = deltaX > 0 ? -1 : 1;
        var (left, right) = (0, 0);
        foreach (var node in GetTree().GetNodesInGroup(MonsterGroup))
        {
            if (node is MonsterController { IsActive: true } other && other._awareness?.IsFighting != false)
            {
                if (other.GlobalPosition.X < _player.GlobalPosition.X)
                {
                    left++;
                }
                else
                {
                    right++;
                }
            }
        }

        if (!FlankRule.ShouldLeap(side, left, right, crowd.FlankCrowdGap))
        {
            return;
        }

        // a jump that lands a little past Lumi on her other side
        GameLog.Info(MessageKeys.CombatFlank, _def.Id);
        var airTime = 2 * crowd.FlankLeapVelocity / _feel.EnemyGravity;
        _velocityY = -crowd.FlankLeapVelocity;
        _leapVelocityX = (deltaX + Mathf.Sign(deltaX) * crowd.FlankLandBeyond) / airTime;
    }

    /// <summary>True while the monster fights; before that it patrols, then shows "!" when it notices Lumi.</summary>
    private bool StepAwareness(float deltaX, float deltaY)
    {
        _noticeTicks = Mathf.Max(0, _noticeTicks - 1);
        _notice.Visible = _noticeTicks > 0;
        if (_awareness is not { } eyes)
        {
            return true;
        }

        eyes.Step(deltaX, deltaY, GlobalPosition.X - _homeX);
        if (eyes.JustNoticed)
        {
            OnNoticed(callOthers: true);
        }

        return eyes.IsFighting;
    }

    /// <summary>Climbers follow Lumi onto ledges and drop back down after her.</summary>
    private void StepClimb(float deltaX, float deltaY, bool onFloor)
    {
        if (_dropTicks > 0 && --_dropTicks == 0)
        {
            CollisionMask = PhysicsLayers.World | PhysicsLayers.Platform;
        }

        if (_climb is null || _brain.State is not (MonsterState.Idle or MonsterState.Chase))
        {
            return;
        }

        var onLedge = onFloor && GlobalPosition.Y < _floorY - LedgeMargin;
        switch (_climb.Step(deltaX, deltaY, onFloor, onLedge, _player.OnGround, _feel.EnemyGravity))
        {
            case ClimbAction.Jump:
                _velocityY = -_climb.JumpVelocity;
                break;
            case ClimbAction.Drop:
                _dropTicks = _feel.MonsterClimb.DropTicks;
                CollisionMask = PhysicsLayers.World;
                break;
        }
    }

    private void StepVertical(float delta, bool onFloor)
    {
        if (_flying)
        {
            _velocityY = Mathf.MoveToward(_velocityY, 0, _feel.KnockbackDecay * delta);
            return;
        }

        var juggled = _brain.State == MonsterState.Stunned && !onFloor;
        var gravity = _feel.EnemyGravity * (juggled ? _feel.JuggleGravityScale : 1);
        _velocityY = onFloor && _velocityY > 0 ? 0 : _velocityY + gravity * delta;
    }

    private void StepAmbush(MonsterMoveDef move)
    {
        var hidden = IsHidden;
        if (hidden && _brain.StateTicksLeft > move.MarkerTicks)
        {
            _ambushX = _player.GlobalPosition.X;
        }
        else if (hidden && _brain.StateTicksLeft % MarkerEveryTicks == 0)
        {
            _gameFeel.PlayEffect(_def.Attack.HitVfx, new Vector2(_ambushX, GlobalPosition.Y - MarkerLift), _brain.Facing);
        }

        if (!hidden && _wasHidden)
        {
            GlobalPosition = new Vector2(_ambushX, GlobalPosition.Y);
        }

        if (hidden != _wasHidden)
        {
            _hurtbox.SetDeferred(Area2D.PropertyName.Monitorable, !hidden);
        }

        _wasHidden = hidden;
        _sprite.Visible = !hidden;
    }

    private void ThrowProjectile(MonsterMoveDef move)
    {
        var from = GlobalPosition + new Vector2(_brain.Facing * ThrowForward, -_def.Art.Height * ThrowHeightShare);
        var seconds = move.ProjectileTicks / GameConstants.TicksPerSecond;
        var target = _player.GlobalPosition + new Vector2(0, -ThrowAimHeight);
        var velocity = new Vector2((target.X - from.X) / seconds, (target.Y - from.Y) / seconds - move.ProjectileGravity * seconds / 2);
        var pot = new Projectile();
        GetParent().AddChild(pot);
        pot.Launch(move.ProjectileSprite!, move.ProjectileHeight, from, velocity, move.ProjectileGravity, GlobalPosition.Y,
            new HitInfo(_stats.Attack, move.DamageMultiplier, _brain.Facing, _def.Attack.KnockbackSpeed, 0, _def.Id), PhysicsLayers.PlayerHurtbox);
    }

    private void StepStrike()
    {
        var move = _brain.CurrentMove;
        if (move is { Kind: MonsterMoveKinds.Ambush })
        {
            StepAmbush(move);
        }

        if (!_brain.StrikeActive || _player.Health.IsDead)
        {
            _hitbox.Close();
            return;
        }

        if (move is { Kind: MonsterMoveKinds.Throw })
        {
            if (_thrownSwing != _brain.SwingId)
            {
                _thrownSwing = _brain.SwingId;
                ThrowProjectile(move);
            }

            return;
        }

        var hit = new HitInfo(_stats.Attack, move?.DamageMultiplier ?? _def.Attack.DamageMultiplier, _brain.Facing, _def.Attack.KnockbackSpeed, 0, _def.Id, 0, move?.Knockdown ?? _def.Attack.Knockdown);
        var landed = _hitbox.Sweep(_brain.SwingId, move?.Hitbox ?? _def.Attack.Hitbox, _brain.Facing, hit);
        if (landed.Count > 0 && move is { StealMp: > 0 })
        {
            _player.LoseMp(move.StealMp);
        }

        if (landed.Count > 0)
        {
            _gameFeel.PlayEffect(_def.Attack.HitVfx, _player.GlobalPosition + new Vector2(0, -100), _brain.Facing);
            _gameFeel.Hitstop(_def.Attack.HitstopTicks);
        }
    }

    private void ShowState()
    {
        _flashTicks = Mathf.Max(0, _flashTicks - 1);
        var telegraph = _brain.State == MonsterState.Windup;
        var dizzy = _brain.State == MonsterState.Dizzy;
        _sprite.Modulate = _flashTicks > 0 ? Colors.White * FlashStrength : dizzy ? DizzyColor : telegraph ? TelegraphColor : _slowTicks > 0 ? StickyColor : Colors.White;
        _age++;
        if (_staggerBar is not null)
        {
            _staggerBar.Size = new Vector2(StaggerBarWidth * _brain.StaggerRatio, StaggerBarHeight);
        }

        _sprite.Texture = _brain.State switch
        {
            MonsterState.Windup => _windup,
            MonsterState.Stunned or MonsterState.Down or MonsterState.Dizzy => _hit,
            _ => _idle,
        };

        var pose = _pose.Step(_brain.State, _brain.StateTicksLeft, _brain.MoveVelocityX, _flying || IsOnFloor(), _velocityY, _brain.Facing);
        var artDirection = _def.Art.FacesLeft ? -1 : 1;
        var flip = _brain.Facing == artDirection ? 1 : -1;
        var textureScale = _def.Art.Height / _sprite.Texture.GetHeight();
        _sprite.Scale = new Vector2(textureScale * flip * pose.ScaleX, textureScale * pose.ScaleY);
        var down = _brain.State == MonsterState.Down;
        _sprite.Rotation = down ? Mathf.Pi / 2 * flip : pose.Rotation;

        // squash keeps the feet on the floor: the sprite center moves with its height
        var centerY = down ? -_def.Art.Height * DownSinkShare : -_def.Art.Height * pose.ScaleY / 2;
        var hover = _flying ? Mathf.Sin(_age * BobPerTick) * BobPixels : 0;
        _sprite.Position = new Vector2(pose.OffsetX, centerY + pose.OffsetY + hover);
        _aura.Texture = _sprite.Texture;
        _aura.Visible = _brain.IsArmored;
    }

    private void BeginHeal()
    {
        _healing = true;
        _turns?.Release(this);
        AudioService.Play(SoundIds.MonsterHealed);
        _hurtbox.SetDeferred(Area2D.PropertyName.Monitorable, false);
        _sprite.Texture = _healed;
        _sprite.Modulate = Colors.White;
        var seconds = _feel.HealFadeTicks / GameConstants.TicksPerSecond;
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(_sprite, "position:y", _sprite.Position.Y - HealRisePixels, seconds).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        tween.TweenProperty(_sprite, "modulate:a", 0f, seconds).SetDelay(seconds * 0.5f);
        tween.Chain().TweenCallback(Callable.From(FinishHeal));
    }

    /// <summary>Leaves the fight without being healed: a lost trial sends the rest away.</summary>
    public void RunOff()
    {
        _healing = true;
        _hurtbox.SetDeferred(Area2D.PropertyName.Monitorable, false);
        _hitbox.Close();
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0f, RunOffSeconds);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    private void FinishHeal()
    {
        GameLog.Info(MessageKeys.CombatMonsterHealed, _def.Id);
        Healed?.Invoke(this);
        QueueFree();
    }

    private Texture2D ArtTexture(string file) => GameServices.Texture($"{_def.Art.Folder}/{file}");
}
