using System.Collections.Generic;
using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Game.Gameplay.Combat;
using LumiFeast.Logic.Combat;
using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;
using LumiFeast.Logic.Player;
using LumiFeast.Logic.Stats;

namespace LumiFeast.Game.Gameplay.Player;

/// <summary>Connects input, the pure logic (motor, combo) and the view. Holds no balance numbers.</summary>
public partial class PlayerController : CharacterBody2D, IDamageable
{
    private CharacterDef _def = null!;
    private GameFeelDef _feel = null!;
    private GameFeel _gameFeel = null!;
    private PlayerMotor _motor = null!;
    private ComboRunner _combo = null!;
    private GuardState _guard = null!;
    private ICharacterView _view = null!;
    private AttackHitbox _hitbox = null!;
    private Vector2 _spawn;
    private int _stunTicks;
    private int _invulnerableTicks;
    private int _respawnTicks;
    private int _comboResetTicks;
    private int _dropTicks;
    private int _landingLagTicks;
    private int _vfxSwingId;
    private bool _onPlatform;

    public IPlayerInput InputSource { get; set; } = new DeviceInput();

    public IReadOnlyList<IBodyBlocker> Blockers { get; set; } = [];

    public event System.Action? Respawned;

    public Health Health { get; private set; } = null!;

    public float Mp { get; private set; }

    public int MaxMp => _def.Stats.MaxMp;

    public int ComboHits { get; private set; }

    public int TotalHits { get; private set; }

    public void Setup(CharacterDef def, AttackSetDef attacks, GameFeelDef feel, GameFeel gameFeel, Vector2 spawn)
    {
        _def = def;
        _feel = feel;
        _gameFeel = gameFeel;
        _spawn = spawn;
        var speed = StatScaler.ForSpeed(def.Stats.Speed, GameServices.Data.Scaling.Speed);
        _motor = new PlayerMotor(def.Movement) { Tempo = speed.Move };
        _combo = new ComboRunner(attacks, def.Combat.AttackBufferTicks) { AttackSpeed = speed.Attack };
        _guard = new GuardState(def.Guard);
        Health = new Health(def.Stats.MaxHp);
        GlobalPosition = spawn;
        CollisionLayer = 0;
        CollisionMask = PhysicsLayers.World | PhysicsLayers.Platform;

        var size = new Vector2(def.Body.Width, def.Body.Height);
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = size }, Position = new Vector2(0, -size.Y / 2) });

        ZIndex = GameConstants.PlayerZIndex; // Lumi stays visible in front of monsters
        var view = new SpineLumiView();
        AddChild(view);
        view.Setup(def.SpineView);
        _view = view;

        var hurtbox = new Hurtbox();
        AddChild(hurtbox);
        hurtbox.Setup(this, size, PhysicsLayers.PlayerHurtbox);

        _hitbox = new AttackHitbox();
        AddChild(_hitbox);
        _hitbox.Setup(PhysicsLayers.EnemyHurtbox);
    }

    /// <summary>Where Lumi comes back after she falls.</summary>
    public void SetRespawnPoint(Vector2 point) => _spawn = point;

    public void Teleport(float x)
    {
        GlobalPosition = new Vector2(x, GlobalPosition.Y);
        _motor.Knockback(0, 0);
    }

    public void Heal()
    {
        Health.Refill();
        Mp = MaxMp;
    }

    public bool TakeHit(in HitInfo hit)
    {
        if (GameServices.GodMode || _invulnerableTicks > 0 || Health.IsDead)
        {
            return false;
        }

        var guarded = _guard.Resolve(hit.Direction, _motor.Facing);
        if (guarded != GuardResult.None)
        {
            ApplyGuard(guarded, hit);
            return true;
        }

        var damage = DamageCalculator.Compute(hit.Attack, hit.Multiplier, _def.Stats.Defense);
        Health.Damage(damage);
        GameLog.Info(MessageKeys.CombatPlayerHit, damage, Health.Current, Health.Max);
        _gameFeel.ShowDamage(GlobalPosition + new Vector2(0, -_def.Body.Height), damage, Colors.OrangeRed);
        _combo.Cancel();
        _hitbox.Close();
        _stunTicks = _feel.HurtStunTicks;
        _invulnerableTicks = _feel.HurtInvulnerableTicks;
        _motor.Knockback(hit.Direction * _feel.HurtKnockbackSpeed, -_feel.HurtKnockbackUp);
        _gameFeel.Hitstop(_feel.HurtHitstopTicks);
        if (Health.IsDead)
        {
            _respawnTicks = _feel.RespawnTicks;
        }

        return true;
    }

    private void ApplyGuard(GuardResult result, in HitInfo hit)
    {
        var guard = _def.Guard;
        _motor.Knockback(hit.Direction * guard.PushSpeed, 0);
        if (result == GuardResult.Perfect)
        {
            GameLog.Info(MessageKeys.CombatGuardPerfect);
            _gameFeel.PlayEffect(_feel.GuardVfx, GlobalPosition + new Vector2(_motor.Facing * _def.Body.Width, -_def.Body.Height / 2), _motor.Facing);
            _gameFeel.Hitstop(guard.PerfectHitstopTicks);
            return;
        }

        var damage = Mathf.Max(1, Mathf.RoundToInt(DamageCalculator.Compute(hit.Attack, hit.Multiplier, _def.Stats.Defense) * guard.DamageScale));
        Health.Damage(damage);
        GameLog.Info(MessageKeys.CombatGuardBlock, damage);
        _gameFeel.ShowDamage(GlobalPosition + new Vector2(0, -_def.Body.Height), damage, Colors.LightGray);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_gameFeel.Frozen)
        {
            return;
        }

        if (Health.IsDead)
        {
            StepDead();
        }

        var input = InputSource.Read();
        if ((input.AttackPressed || input.HeavyPressed) && _stunTicks == 0 && _landingLagTicks == 0)
        {
            _combo.PressAttack(input.HeavyPressed);
        }

        _combo.Advance(CurrentEntry());
        if (_combo.JustStarted && input.MoveX != 0)
        {
            _motor.Face(Mathf.Sign(input.MoveX));
        }

        if (_combo.JustStarted)
        {
            _motor.EndRun();
            _guard.ClearCounter();
        }

        if (_combo.CanCancel && (_motor.HasQueuedAction || input.GuardHeld))
        {
            _combo.Cancel();
            _hitbox.Close();
        }

        var attacking = _combo.Current;
        var onFloor = IsOnFloor();
        _guard.Step(input.GuardHeld && onFloor && attacking is null && _stunTicks == 0 && _landingLagTicks == 0 && !_motor.IsDashing && !Health.IsDead);
        if (input.DownPressed && _onPlatform && attacking is null && _stunTicks == 0)
        {
            _dropTicks = _def.Movement.DropThroughTicks;
            CollisionMask = PhysicsLayers.World;
        }

        _motor.AirJumpUnlocked = GameServices.AirJumpUnlocked;
        _motor.Step(new MotorInput(
            input.MoveX,
            input.JumpPressed,
            input.JumpHeld,
            input.DashPressed,
            onFloor,
            attacking is not null || _stunTicks > 0 || _landingLagTicks > 0 || Health.IsDead || _guard.IsGuarding,
            attacking is not null && !onFloor && _combo.Phase != AttackPhase.Recovery ? _def.Movement.AirAttackGravityScale : 1,
            LungeVelocity(),
            input.DownHeld,
            attacking is not null && _combo.Phase == AttackPhase.Active ? attacking.PlungeSpeed : 0));

        Velocity = new Vector2(_motor.VelocityX, _motor.VelocityY);
        MoveAndSlide();
        _onPlatform = StandsOnPlatform();
        PushOutOfBodies();
        if (_combo.Current is { EndsOnLanding: true } landed && IsOnFloor())
        {
            _landingLagTicks = landed.LandingLagTicks;
            _combo.Cancel();
            _hitbox.Close();
        }

        StepTimers();
        StepAttack();
        ShowView(attacking);
    }

    private bool StandsOnPlatform()
    {
        for (var i = 0; i < GetSlideCollisionCount(); i++)
        {
            var hit = GetSlideCollision(i);
            if (hit.GetCollider() is CollisionObject2D body && (body.CollisionLayer & PhysicsLayers.Platform) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private ComboEntry CurrentEntry()
    {
        if (!IsOnFloor())
        {
            return ComboEntry.Air;
        }

        if (_guard.CounterReady)
        {
            return ComboEntry.Counter;
        }

        return _motor.IsSprinting || _motor.IsDashing ? ComboEntry.Sprint : ComboEntry.Ground;
    }

    /// <summary>Monsters stop Lumi, except while she dashes through them.</summary>
    private void PushOutOfBodies()
    {
        if (_motor.IsDashing || Health.IsDead)
        {
            return;
        }

        foreach (var body in Blockers)
        {
            var feet = body.FeetPosition;
            var overlapsHeight = GlobalPosition.Y > feet.Y - body.BodySize.Y && GlobalPosition.Y - _def.Body.Height < feet.Y;
            if (body.BlocksBody && overlapsHeight)
            {
                var push = BodyBlock.PushOut(GlobalPosition.X, _def.Body.Width / 2, feet.X, body.BodySize.X / 2);
                if (push != 0)
                {
                    MoveAndCollide(new Vector2(push, 0));
                }
            }
        }
    }

    private float LungeVelocity()
    {
        if (_combo.Current is not { } attack || _combo.Phase == AttackPhase.Recovery)
        {
            return 0;
        }

        return _motor.Facing * attack.LungePixels / (attack.StartupTicks + attack.ActiveTicks) * GameConstants.TicksPerSecond * _combo.AttackSpeed;
    }

    private void StepAttack()
    {
        if (_combo.Phase == AttackPhase.Active && _combo.SwingId != _vfxSwingId && _combo.Current is { } starting)
        {
            _vfxSwingId = _combo.SwingId;
            var position = GlobalPosition + new Vector2(starting.VfxX * _motor.Facing, starting.VfxY);
            _gameFeel.PlayEffect(starting.Vfx, position, _motor.Facing);
        }

        if (_combo.Phase != AttackPhase.Active || _combo.Current is not { } attack)
        {
            _hitbox.Close();
            return;
        }

        var hit = new HitInfo(_def.Stats.Attack, attack.DamageMultiplier, _motor.Facing, attack.KnockbackSpeed, attack.LaunchSpeed, _def.Id, attack.StunTicks, attack.Knockdown, attack.Stagger);
        var landed = _hitbox.Sweep(_combo.SwingId, attack.Hitbox, _motor.Facing, hit);
        foreach (var target in landed)
        {
            if (target is Node2D node)
            {
                _gameFeel.PlayEffect(attack.HitVfx, node.GlobalPosition + new Vector2(0, -70), _motor.Facing);
            }

            Mp = Mathf.Min(MaxMp, Mp + attack.Mana);
            ComboHits++;
            TotalHits++;
            _comboResetTicks = _feel.ComboResetTicks;
        }

        if (landed.Count > 0)
        {
            _gameFeel.Hitstop(attack.HitstopTicks);
            _gameFeel.Shake(attack.ShakePixels);
        }
    }

    private void StepTimers()
    {
        _stunTicks = Mathf.Max(0, _stunTicks - 1);
        _dropTicks = Mathf.Max(0, _dropTicks - 1);
        _landingLagTicks = Mathf.Max(0, _landingLagTicks - 1);
        if (_dropTicks == 0)
        {
            CollisionMask = PhysicsLayers.World | PhysicsLayers.Platform;
        }

        _invulnerableTicks = Mathf.Max(0, _invulnerableTicks - 1);
        Mp = Mathf.Min(MaxMp, Mp + _def.Combat.MpPerSecond / GameConstants.TicksPerSecond);
        _comboResetTicks = Mathf.Max(0, _comboResetTicks - 1);
        if (_comboResetTicks == 0)
        {
            ComboHits = 0;
        }
    }

    private void StepDead()
    {
        _respawnTicks--;
        if (_respawnTicks <= 0)
        {
            Health.Refill();
            GlobalPosition = _spawn;
            _motor.Knockback(0, 0);
            _invulnerableTicks = _feel.HurtInvulnerableTicks;
            Respawned?.Invoke();
        }
    }

    private void ShowView(AttackDef? attack)
    {
        _view.Show(new ViewState(
            _motor.Facing,
            _motor.VelocityX,
            _motor.VelocityY,
            IsOnFloor(),
            _motor.IsDashing,
            _invulnerableTicks > 0,
            _stunTicks > 0 || Health.IsDead,
            attack is not null,
            attack?.Id ?? string.Empty,
            _combo.SwingId,
            _combo.Progress,
            _guard.IsGuarding,
            _guard.CounterReady));
    }
}
