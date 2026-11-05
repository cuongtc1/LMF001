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
public partial class MonsterController : CharacterBody2D, IDamageable
{
    private const float WindupSquash = 0.14f;
    private const float FlashStrength = 2.2f;
    private const float HealRisePixels = 90;

    private MonsterDef _def = null!;
    private MonsterStats _stats = null!;
    private GameFeelDef _feel = null!;
    private GameFeel _gameFeel = null!;
    private PlayerController _player = null!;
    private MonsterBrain _brain = null!;
    private Health _health = null!;
    private AttackHitbox _hitbox = null!;
    private Sprite2D _sprite = null!;
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

    public event Action<MonsterController>? Healed;

    public string DefinitionId => _def.Id;

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
        CollisionLayer = 0;
        CollisionMask = PhysicsLayers.World;

        var size = new Vector2(def.Body.Width, def.Body.Height);
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = size }, Position = new Vector2(0, -size.Y / 2) });

        _idle = ArtTexture(def.Art.Idle);
        _windup = ArtTexture(def.Art.Windup);
        _hit = ArtTexture(def.Art.Hit);
        _healed = ArtTexture(def.Art.Healed);
        _spriteScale = def.Art.Height / _idle.GetHeight();
        _sprite = new Sprite2D { Texture = _idle, Position = new Vector2(0, -def.Art.Height / 2) };
        AddChild(_sprite);

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

        var damage = DamageCalculator.Compute(hit.Attack, hit.Multiplier, _stats.Defense);
        if (!_def.Immortal)
        {
            _health.Damage(damage);
        }

        GameLog.Info(MessageKeys.CombatMonsterHit, _def.Id, damage, hit.SourceId, _health.Current, _health.Max);
        _gameFeel.ShowDamage(GlobalPosition + new Vector2(0, -_def.Art.Height), damage, Colors.White);
        _knockback = hit.Direction * hit.KnockbackSpeed * _def.KnockbackScale;
        _flashTicks = 6;
        _brain.Stun();
        _hitbox.Close();
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

        var onFloor = IsOnFloor();
        _brain.Step(_player.GlobalPosition.X - GlobalPosition.X, onFloor);
        if (_brain.WantsHop)
        {
            _velocityY = -_def.Ai.HopVelocity;
        }

        _velocityY = onFloor && _velocityY > 0 ? 0 : _velocityY + _feel.EnemyGravity * (float)delta;
        _knockback = Mathf.MoveToward(_knockback, 0, _feel.KnockbackDecay * (float)delta);
        Velocity = new Vector2(_brain.MoveVelocityX + _knockback, _velocityY);
        MoveAndSlide();

        StepStrike();
        ShowState();
    }

    private void StepStrike()
    {
        if (!_brain.StrikeActive || _player.Health.IsDead)
        {
            _hitbox.Close();
            return;
        }

        var hit = new HitInfo(_stats.Attack, _def.Attack.DamageMultiplier, _brain.Facing, _def.Attack.KnockbackSpeed, _def.Id);
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
        _sprite.Modulate = _flashTicks > 0 ? Colors.White * FlashStrength : Colors.White;
        _sprite.Texture = _brain.State switch
        {
            MonsterState.Windup => _windup,
            MonsterState.Stunned => _hit,
            _ => _idle,
        };

        var squash = _brain.State == MonsterState.Windup ? WindupSquash : 0;
        var artDirection = _def.Art.FacesLeft ? -1 : 1;
        var flip = _brain.Facing == artDirection ? 1 : -1;
        var textureScale = _def.Art.Height / _sprite.Texture.GetHeight();
        _sprite.Scale = new Vector2(textureScale * flip * (1 + squash), textureScale * (1 - squash));
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
