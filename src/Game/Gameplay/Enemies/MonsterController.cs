using System;
using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Game.Gameplay.Combat;
using LumiFeast.Game.Gameplay.Player;
using LumiFeast.Logic.Combat;
using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;
using LumiFeast.Logic.Enemies;
using LumiFeast.Logic.Stats;

namespace LumiFeast.Game.Gameplay.Enemies;

/// <summary>Runs any plain monster from its data file. New monsters normally need no new code.</summary>
public partial class MonsterController : CharacterBody2D, IDamageable, IBodyBlocker
{
    private const float WindupSquash = 0.14f;
    private const float FlashStrength = 2.2f;
    private const float HealRisePixels = 90;
    private const float BobPixels = 10;
    private const float BobPerTick = 0.09f;
    private const float AuraScale = 1.12f;
    private const float DownSinkShare = 0.25f;
    private const float StaggerBarWidth = 200;
    private const float StaggerBarHeight = 10;
    private const float DizzyWobble = 0.12f;
    private const string AuraShaderCode = "shader_type canvas_item; void fragment() { COLOR.rgb = vec3(1.0, 0.12, 0.1); }";

    private static readonly Color TelegraphColor = new(1.6f, 0.55f, 0.45f);
    private static readonly Color DizzyColor = new(1.4f, 1.3f, 0.5f);

    private MonsterDef _def = null!;
    private MonsterStats _stats = null!;
    private GameFeelDef _feel = null!;
    private GameFeel _gameFeel = null!;
    private PlayerController _player = null!;
    private MonsterBrain _brain = null!;
    private Health _health = null!;
    private AttackHitbox _hitbox = null!;
    private Sprite2D _sprite = null!;
    private Sprite2D _aura = null!;
    private ColorRect? _staggerBar;
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
    private int _age;

    public event Action<MonsterController>? Healed;

    public string DefinitionId => _def.Id;

    public bool BlocksBody => !_healing && !_flying;

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
        _health = new Health(stats.MaxHp);
        GlobalPosition = position;
        _flying = def.Ai.Behavior == MonsterBehaviors.Flyer;
        _hoverY = position.Y;
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

        _hitbox = new AttackHitbox();
        AddChild(_hitbox);
        _hitbox.Setup(PhysicsLayers.PlayerHurtbox);
    }

    public bool TakeHit(in HitInfo hit)
    {
        if (_healing)
        {
            return false;
        }

        var damage = Mathf.RoundToInt(DamageCalculator.Compute(hit.Attack, hit.Multiplier, _stats.Defense) * _brain.DamageTakenScale);
        if (!_def.Immortal)
        {
            _health.Damage(damage);
        }

        GameLog.Info(MessageKeys.CombatMonsterHit, _def.Id, damage, hit.SourceId, _health.Current, _health.Max);
        if (_brain.RegisterHit((float)_health.Current / _health.Max))
        {
            GameLog.Info(MessageKeys.CombatStoic, _def.Id);
        }

        _gameFeel.ShowDamage(GlobalPosition + new Vector2(0, -_def.Art.Height), damage, Colors.White);
        if (_brain.AddStagger(hit.Stagger))
        {
            GameLog.Info(MessageKeys.CombatDizzy, _def.Id);
        }

        _flashTicks = 6;
        if (_brain.CanBeMoved)
        {
            _knockback = hit.Direction * hit.KnockbackSpeed * _def.KnockbackScale;
            if (hit.LaunchSpeed > 0)
            {
                _velocityY = -hit.LaunchSpeed * _def.KnockbackScale;
            }

            _brain.Stun(hit.StunTicks, hit.Knockdown);
            _hitbox.Close();
        }

        if (_health.IsDead)
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

        var onFloor = _flying || IsOnFloor();
        _brain.Step(_player.GlobalPosition.X - GlobalPosition.X, onFloor, GlobalPosition.Y - _hoverY);
        if (_brain.WantsHop)
        {
            _velocityY = -_def.Ai.HopVelocity;
        }

        StepVertical((float)delta, onFloor);
        _knockback = Mathf.MoveToward(_knockback, 0, _feel.KnockbackDecay * (float)delta);
        Velocity = new Vector2(_brain.MoveVelocityX + _knockback, _velocityY + _brain.MoveVelocityY);
        MoveAndSlide();

        StepStrike();
        ShowState();
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

    private void StepStrike()
    {
        if (!_brain.StrikeActive || _player.Health.IsDead)
        {
            _hitbox.Close();
            return;
        }

        var hit = new HitInfo(_stats.Attack, _def.Attack.DamageMultiplier, _brain.Facing, _def.Attack.KnockbackSpeed, 0, _def.Id);
        var landed = _hitbox.Sweep(_brain.SwingId, _def.Attack.Hitbox, _brain.Facing, hit);
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
        _sprite.Modulate = _flashTicks > 0 ? Colors.White * FlashStrength : dizzy ? DizzyColor : telegraph ? TelegraphColor : Colors.White;
        _age++;
        if (_staggerBar is not null)
        {
            _staggerBar.Size = new Vector2(StaggerBarWidth * _brain.StaggerRatio, StaggerBarHeight);
        }

        if (_flying)
        {
            _sprite.Position = new Vector2(0, -_def.Art.Height / 2 + Mathf.Sin(_age * BobPerTick) * BobPixels);
        }

        _sprite.Texture = _brain.State switch
        {
            MonsterState.Windup => _windup,
            MonsterState.Stunned or MonsterState.Down or MonsterState.Dizzy => _hit,
            _ => _idle,
        };

        var squash = _brain.State == MonsterState.Windup ? WindupSquash : 0;
        var artDirection = _def.Art.FacesLeft ? -1 : 1;
        var flip = _brain.Facing == artDirection ? 1 : -1;
        var textureScale = _def.Art.Height / _sprite.Texture.GetHeight();
        _sprite.Scale = new Vector2(textureScale * flip * (1 + squash), textureScale * (1 - squash));
        var down = _brain.State == MonsterState.Down;
        _sprite.Rotation = down ? Mathf.Pi / 2 * flip : dizzy ? Mathf.Sin(_age * BobPerTick * 2) * DizzyWobble : 0;
        if (!_flying)
        {
            _sprite.Position = new Vector2(0, -_def.Art.Height * (down ? DownSinkShare : 0.5f));
        }

        _aura.Texture = _sprite.Texture;
        _aura.Visible = _brain.IsArmored;
    }

    private void BeginHeal()
    {
        _healing = true;
        _hurtbox.SetDeferred(Area2D.PropertyName.Monitorable, false);
        _sprite.Texture = _healed;
        _sprite.Modulate = Colors.White;
        var seconds = _feel.HealFadeTicks / GameConstants.TicksPerSecond;
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(_sprite, "position:y", _sprite.Position.Y - HealRisePixels, seconds).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        tween.TweenProperty(_sprite, "modulate:a", 0f, seconds).SetDelay(seconds * 0.5f);
        tween.Chain().TweenCallback(Callable.From(FinishHeal));
    }

    private void FinishHeal()
    {
        GameLog.Info(MessageKeys.CombatMonsterHealed, _def.Id);
        Healed?.Invoke(this);
        QueueFree();
    }

    private Texture2D ArtTexture(string file) => GameServices.Texture($"{_def.Art.Folder}/{file}");
}
