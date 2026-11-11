using System.Collections.Generic;
using System.Linq;
using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Game.Gameplay.Combat;
using LumiFeast.Logic.Combat;
using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;
using LumiFeast.Logic.Player;
using LumiFeast.Logic.Run;
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
    private int _downTicks;
    private int _getupTicks;
    private int _guardHitTicks;
    private int _rushTicks;
    private int _manaSwing = -1;
    private readonly RoomUses _steamUses = new();
    private readonly RoomUses _freeSkillUses = new();
    private int _vfxSwingId;
    private bool _onPlatform;
    private int _skillInvulnerableTicks;

    /// <summary>Lunge of the current pass slash, measured from the monster it passes (null: the attack's own lunge).</summary>
    private float? _passLunge;
    private AttackSetDef _attacks = null!;

    /// <summary>The skill move playing now, with what its level adds.</summary>
    private string? _skillAttack;
    private float _skillDamage = 1;
    private int _skillRepeats;
    private const float PassHeightSlack = 40;
    private SkillSetDef _skills = null!;

    private const float ProjectileForward = 80;
    private const float ProjectileHeight = 110;
    private const float ProjectileFloor = 100000;
    private const int RehitStride = 1000;

    public IPlayerInput InputSource { get; set; } = new DeviceInput();

    public IReadOnlyList<IBodyBlocker> Blockers { get; set; } = [];

    public event System.Action? Respawned;

    /// <summary>Raised once the death pause ends while <see cref="AutoRespawn"/> is off.</summary>
    public event System.Action? Fell;

    /// <summary>A hit that got through: not guarded, not dodged.</summary>
    public event System.Action? Hurt;

    /// <summary>The test stage brings Lumi back by herself; a run shows its result screen instead.</summary>
    public bool AutoRespawn { get; set; } = true;

    public Health Health { get; private set; } = null!;

    /// <summary>Lumi's attack (base stats now; skill tree stat nodes will add to it).</summary>
    public int Attack { get; private set; }

    public bool OnGround => IsOnFloor();

    /// <summary>Left/right held this tick, -1 to 1. Room exits need it, so a lunge never carries Lumi out.</summary>
    public float MoveInput { get; private set; }

    /// <summary>Menus open: Lumi ignores buttons.</summary>
    public bool InputPaused { get; set; }

    public float Mp { get; private set; }

    public int MaxMp => _def.Stats.MaxMp;

    public SkillLoadout Loadout { get; private set; } = null!;

    public string SkillSetId => _skills.Id;

    /// <summary>The amulets of the current run.</summary>
    public RunModifiers Modifiers { get; set; } = new();

    public int ComboHits { get; private set; }

    public int TotalHits { get; private set; }

    public void Setup(CharacterDef def, AttackSetDef attacks, GameFeelDef feel, GameFeel gameFeel, Vector2 spawn)
    {
        _def = def;
        _feel = feel;
        _gameFeel = gameFeel;
        _spawn = spawn;
        _motor = new PlayerMotor(def.Movement);
        _attacks = attacks;
        _combo = new ComboRunner(attacks, def.Combat.AttackBufferTicks);
        _skills = GameServices.Data.SkillSet(def.Combat.SkillSet);
        Loadout = new SkillLoadout(_skills.Slots);
        Health = new Health(def.Stats.MaxHp);
        Attack = def.Stats.Attack;
        _guard = new GuardState(def.Guard);
        ApplyModifiers();
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

    /// <summary>Recomputes what amulets change on Lumi herself.</summary>
    public void ApplyModifiers()
    {
        var hpRatio = (float)Health.Current / Health.Max;
        var rush = _rushTicks > 0 ? Modifiers.Total(AmuletEffects.GuardRush) : 0;
        var speed = StatScaler.ForSpeed(_def.Stats.Speed + Modifiers.Total(AmuletEffects.Speed, hpRatio) + rush, GameServices.Data.Scaling.Speed);
        _motor.Tempo = speed.Move;
        _combo.AttackSpeed = speed.Attack;
        _guard.AutoCounter = _def.Guard.AutoCounter || SteamCountersLeft > 0;
    }

    /// <summary>Where Lumi comes back after she falls.</summary>
    public void SetRespawnPoint(Vector2 point) => _spawn = point;

    public Vector2 RespawnPoint => _spawn;

    public void MoveTo(Vector2 position)
    {
        GlobalPosition = position;
        _motor.Knockback(0, 0);
    }

    public void Teleport(float x)
    {
        GlobalPosition = new Vector2(x, GlobalPosition.Y);
        _motor.Knockback(0, 0);
    }

    public void LoseMp(float amount) => Mp = Mathf.Max(0, Mp - amount);

    /// <summary>Sticky honey and similar statuses (round 83): walking slower for a short time; the strongest slow wins.</summary>
    public void Slow(float share, int ticks)
    {
        _slow = _slowTicks > 0 ? Mathf.Max(_slow, share) : share;
        _slowTicks = Mathf.Max(_slowTicks, ticks);
    }

    /// <summary>Slowed right now (the view tints Lumi).</summary>
    public bool IsSlowed => _slowTicks > 0;

    private float _slow;
    private int _slowTicks;

    public void GainMp(float amount) => Mp = Mathf.Min(MaxMp, Mp + amount);

    /// <summary>Steam Bath counters left in this room.</summary>
    public int SteamCountersLeft => _steamUses.Left(SteamLimit);

    private int SteamLimit => Mathf.RoundToInt(Modifiers.Total(AmuletEffects.AutoCounter));

    /// <summary>Per-room amulet uses start again (Steam Bath, Second Helping).</summary>
    public void EnterRoom()
    {
        _steamUses.Reset();
        _freeSkillUses.Reset();
    }

    public void Heal()
    {
        Health.Refill();
        Mp = MaxMp;
    }

    public bool TakeHit(in HitInfo hit)
    {
        if (GameServices.GodMode || _invulnerableTicks > 0 || _skillInvulnerableTicks > 0 || PassingSafe || Health.IsDead)
        {
            return false;
        }

        var guarded = _guard.Resolve(hit.Direction, _motor.Facing);
        if (guarded != GuardResult.None)
        {
            ApplyGuard(guarded, hit);
            return true;
        }

        var damage = Mathf.Max(1, Mathf.RoundToInt(DamageCalculator.Compute(hit.Attack, hit.Multiplier, _def.Stats.Defense) * Modifiers.Scale(AmuletEffects.DamageTaken)));
        Health.Damage(damage);
        Hurt?.Invoke();
        AudioService.Play(SoundIds.Hurt);
        GameLog.Info(MessageKeys.CombatPlayerHit, damage, Health.Current, Health.Max);
        _gameFeel.ShowDamage(GlobalPosition + new Vector2(0, -_def.Body.Height), damage, Colors.OrangeRed);
        _combo.Cancel();
        _hitbox.Close();
        _stunTicks = _feel.HurtStunTicks;
        _invulnerableTicks = _feel.HurtInvulnerableTicks;
        if (hit.Knockdown)
        {
            _downTicks = _feel.PlayerDownTicks;
            _getupTicks = _feel.PlayerGetupTicks;
            _stunTicks = _downTicks + _getupTicks;
            _invulnerableTicks += _stunTicks;
        }

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
        _guardHitTicks = guard.HitTicks;
        _motor.Knockback(hit.Direction * guard.PushSpeed, 0);
        if (result == GuardResult.Perfect)
        {
            GameLog.Info(MessageKeys.CombatGuardPerfect);
            AudioService.Play(SoundIds.GuardPerfect);
            GainMp(Modifiers.Total(AmuletEffects.PerfectGuardMana));
            _rushTicks = Modifiers.Ticks(AmuletEffects.GuardRush);
            _gameFeel.PlayEffect(_feel.GuardVfx, GlobalPosition + new Vector2(_motor.Facing * _def.Body.Width, -_def.Body.Height / 2), _motor.Facing);
            _gameFeel.Hitstop(guard.PerfectHitstopTicks);
            return;
        }

        var damage = Mathf.Max(1, Mathf.RoundToInt(DamageCalculator.Compute(hit.Attack, hit.Multiplier, _def.Stats.Defense) * guard.DamageScale));
        Health.Damage(damage);
        GameLog.Info(MessageKeys.CombatGuardBlock, damage);
        AudioService.Play(SoundIds.GuardBlock);
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

        var input = InputPaused ? PlayerFrameInput.None : InputSource.Read();
        MoveInput = input.MoveX;
        if ((input.AttackPressed || input.HeavyPressed) && _stunTicks == 0 && _landingLagTicks == 0)
        {
            _combo.PressAttack(input.HeavyPressed && !input.AttackPressed);
        }
        else if (_guardHitTicks == 0 && _stunTicks == 0 && _guard.TakeAutoCounter())
        {
            if (!_def.Guard.AutoCounter)
            {
                _steamUses.TryUse(SteamLimit);
            }

            _combo.PressAttack(false);
        }

        TryStartSkill(input.SkillPressed);
        var before = _combo.Current;
        var beforePhase = _combo.Phase;
        _combo.Advance(CurrentEntry(input));
        StepPass(before, beforePhase);
        StepSkillRepeats();
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
        var wasDashing = _motor.IsDashing;
        var wasRising = _motor.VelocityY < 0;
        _slowTicks = Mathf.Max(0, _slowTicks - 1);
        _motor.Step(new MotorInput(
            input.MoveX * (_slowTicks > 0 ? 1 - _slow : 1),
            input.JumpPressed,
            input.JumpHeld,
            input.DashPressed,
            onFloor,
            attacking is not null || _stunTicks > 0 || _landingLagTicks > 0 || Health.IsDead || _guard.IsGuarding,
            // light gravity through an air slash and, when another slash can follow, its recovery too
            attacking is not null && !onFloor && (_combo.Phase != AttackPhase.Recovery || attacking.Next is not null) ? _def.Movement.AirAttackGravityScale : 1,
            LungeVelocity(),
            input.DownHeld,
            attacking is not null && _combo.Phase == AttackPhase.Active ? attacking.PlungeSpeed : 0));

        PlayMoveSounds(wasDashing, wasRising, input.JumpPressed);
        Velocity = new Vector2(_motor.VelocityX, _motor.VelocityY);
        MoveAndSlide();
        if (!onFloor && IsOnFloor())
        {
            AudioService.Play(SoundIds.Land);
        }
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

    private void PlayMoveSounds(bool wasDashing, bool wasRising, bool jumpPressed)
    {
        if (!wasDashing && _motor.IsDashing)
        {
            AudioService.Play(SoundIds.Dash);
        }
        else if (jumpPressed && !wasRising && _motor.VelocityY < 0)
        {
            AudioService.Play(SoundIds.Jump);
        }
    }

    private void TryStartSkill(int slot)
    {
        if (slot < 0 || _stunTicks > 0 || _landingLagTicks > 0 || Health.IsDead || (_combo.Current is not null && !_combo.CanCancel))
        {
            return;
        }

        var freeLimit = Modifiers.Total(AmuletEffects.FreeFirstSkill) > 0 ? 1 : 0;
        var free = _freeSkillUses.Left(freeLimit) > 0;
        if (Loadout.TryUse(slot, free ? float.MaxValue : Mp, Modifiers.Scale(AmuletEffects.SkillCooldown)) is not { } skill)
        {
            return;
        }

        var bonus = Loadout.Bonus(slot);
        _skillAttack = skill.Attack;
        _skillDamage = bonus?.DamageScale ?? 1;
        _skillRepeats = bonus?.Repeats ?? 0;

        if (free)
        {
            _freeSkillUses.TryUse(freeLimit);
        }
        else
        {
            Mp -= skill.MpCost;
        }
        AudioService.Play(SoundIds.Skill);
        _skillInvulnerableTicks = skill.InvulnerableTicks;
        _combo.StartSkill(skill.Attack);
        _motor.EndRun();
        _hitbox.Close();
    }

    private void LaunchProjectile(string id, AttackDef attack)
    {
        var shot = _skills.Projectiles.First(projectile => projectile.Id == id);
        var hit = new HitInfo(Attack, shot.DamageMultiplier * DamageScale(attack), _motor.Facing, attack.KnockbackSpeed, 0, _def.Id, attack.StunTicks, false, shot.Stagger, true)
        {
            BackMultiplier = Modifiers.Scale(AmuletEffects.BackDamage),
        };
        var projectile = new Projectile();
        GetParent().AddChild(projectile);
        projectile.Landed = OnLanded;
        projectile.RehitTicks = shot.RehitTicks;
        projectile.Pull = shot.Pull;
        projectile.HitSize = shot.HitSize;
        projectile.BurstSize = shot.BurstSize;
        projectile.Burst = at =>
        {
            _gameFeel.PlayEffect(_feel.GuardVfx, at, _motor.Facing);
            _gameFeel.Shake(attack.ShakePixels);
            AudioService.Play(SoundIds.HitHeavy);
        };
        // arcing shots burst at Lumi's feet level; straight shots fly until their time runs out
        var floor = shot.Gravity > 0 ? GlobalPosition.Y : ProjectileFloor;
        projectile.Launch(shot.Sprite, shot.Height, GlobalPosition + new Vector2(_motor.Facing * ProjectileForward, -ProjectileHeight),
            new Vector2(_motor.Facing * shot.Speed, -shot.RiseSpeed), shot.Gravity, floor, hit, PhysicsLayers.EnemyHurtbox, shot.Pierce, shot.Ticks);
    }

    private float DamageScale(AttackDef attack) =>
        Modifiers.Scale(attack.GuardBreak ? AmuletEffects.HeavyDamage : AmuletEffects.LightDamage) * (attack.Id == _skillAttack ? _skillDamage : 1);

    /// <summary>A levelled skill plays again by itself (Jam Spread Lv2: a second wave), free, once the move can cancel.</summary>
    private void StepSkillRepeats()
    {
        if (_combo.Current?.Id != _skillAttack)
        {
            _skillRepeats = 0;
            return;
        }

        if (_skillRepeats > 0 && _combo.CanCancel)
        {
            _skillRepeats--;
            _combo.StartSkill(_skillAttack!);
            _hitbox.Close();
        }
    }

    private void OnLanded(IDamageable target)
    {
        if (target is Node2D node)
        {
            _gameFeel.PlayEffect(_feel.GuardVfx, node.GlobalPosition + new Vector2(0, -70), _motor.Facing);
        }

        AudioService.Play(SoundIds.HitLight);

        TotalHits++;
        ComboHits++;
        _comboResetTicks = _feel.ComboResetTicks;
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

    private ComboEntry CurrentEntry(in PlayerFrameInput input)
    {
        if (!IsOnFloor())
        {
            return input.JumpHeld && _motor.VelocityY < 0 ? ComboEntry.AirUp : ComboEntry.Air;
        }

        if (_guard.CounterReady)
        {
            return ComboEntry.Counter;
        }

        return _motor.IsSprinting || _motor.IsDashing ? ComboEntry.Sprint : ComboEntry.Ground;
    }

    /// <summary>
    /// Combo kit v2 ("Z hits, X goes through"): a pass slash carries Lumi just behind the monster in front, she is
    /// invulnerable while passing, and she turns to face it when the pass ends, so the next Z hits its back.
    /// </summary>
    private void StepPass(AttackDef? before, AttackPhase beforePhase)
    {
        if (before is { TurnAtEnd: true } && beforePhase != AttackPhase.Recovery && (_combo.Current != before || _combo.Phase == AttackPhase.Recovery || _combo.JustStarted))
        {
            _motor.Face(-_motor.Facing);
        }

        if (!_combo.JustStarted || _combo.Current is not { } attack)
        {
            return;
        }

        // the air string hangs in the air: each air slash gives a small hop instead of falling after the first one
        if (attack.EndsOnLanding && attack.PlungeSpeed == 0 && !IsOnFloor() && _attacks.AirHopSpeed > 0)
        {
            _motor.Knockback(_motor.VelocityX, Mathf.Min(_motor.VelocityY, -_attacks.AirHopSpeed));
        }

        _passLunge = null;
        if (attack.PassThrough)
        {
            var set = _attacks;
            var targets = Blockers.Where(body => body.BlocksBody && SameHeight(body))
                .Select(body => (body.FeetPosition.X, body.BodySize.X / 2));
            _passLunge = PassRule.Distance(GlobalPosition.X, _motor.Facing, _def.Body.Width / 2, targets, set.PassReach, set.PassMargin, attack.LungePixels, set.PassMax);
        }

        if (attack.LiftSpeed > 0)
        {
            _motor.Knockback(0, -attack.LiftSpeed);
        }
    }

    /// <summary>A pass slash while it moves: no body blocking.</summary>
    private bool Passing => _combo.Current is { PassThrough: true } && _combo.Phase != AttackPhase.Recovery;

    /// <summary>Only the middle of a pass, while Lumi is inside the monster, is safe (v3: the start and end can be hit).</summary>
    private bool PassingSafe => _combo.Current is { PassThrough: true } && _combo.Phase == AttackPhase.Active;


    private bool SameHeight(IBodyBlocker body)
    {
        var feet = body.FeetPosition;
        return GlobalPosition.Y > feet.Y - body.BodySize.Y - PassHeightSlack && GlobalPosition.Y - _def.Body.Height < feet.Y + PassHeightSlack;
    }

    /// <summary>
    /// Monsters stop Lumi, dashes included (round 77: an air dash gets past only above a monster's head), except while
    /// she passes or uses a skill.
    /// </summary>
    private void PushOutOfBodies()
    {
        if (Passing || _skillInvulnerableTicks > 0 || Health.IsDead)
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

        var lunge = attack.PassThrough && _passLunge is { } pass ? pass : attack.LungePixels;
        return _motor.Facing * lunge / (attack.StartupTicks + attack.ActiveTicks) * GameConstants.TicksPerSecond * _combo.AttackSpeed;
    }

    private void StepAttack()
    {
        if (_combo.Phase == AttackPhase.Active && _combo.SwingId != _vfxSwingId && _combo.Current is { } starting)
        {
            _vfxSwingId = _combo.SwingId;
            var position = GlobalPosition + new Vector2(starting.VfxX * _motor.Facing, starting.VfxY);
            _gameFeel.PlayEffect(starting.Vfx, position, _motor.Facing);
            AudioService.Play(starting.GuardBreak ? SoundIds.SwingHeavy : SoundIds.SwingLight);
            if (starting.Projectile is { } shot)
            {
                LaunchProjectile(shot, starting);
            }

            foreach (var extra in Modifiers.ProjectilesFor(starting.Id))
            {
                LaunchProjectile(extra, starting);
            }
        }

        if (_combo.Phase != AttackPhase.Active || _combo.Current is not { } attack)
        {
            _hitbox.Close();
            return;
        }

        var stagger = attack.Stagger * (attack.GuardBreak ? Modifiers.Scale(AmuletEffects.HeavyStagger) : 1);
        var hit = new HitInfo(Attack, attack.DamageMultiplier * DamageScale(attack), _motor.Facing, attack.KnockbackSpeed, attack.LaunchSpeed, _def.Id, attack.StunTicks, attack.Knockdown, stagger, attack.GuardBreak)
        {
            BackMultiplier = Modifiers.Scale(AmuletEffects.BackDamage),
            Slow = Modifiers.TotalFor(AmuletEffects.AttackSlow, attack.Id),
            SlowTicks = Modifiers.Ticks(AmuletEffects.AttackSlow, attack.Id),
        };
        var swing = attack.RehitTicks > 0 ? _combo.SwingId * RehitStride + _combo.Tick / attack.RehitTicks : _combo.SwingId;
        var landed = _hitbox.Sweep(swing, attack.Hitbox, _motor.Facing, hit);
        if (landed.Count > 0 && swing != _manaSwing)
        {
            // flat bonus once per swing, however many monsters it hits (Finisher Fuel)
            _manaSwing = swing;
            GainMp(Modifiers.TotalFor(AmuletEffects.AttackMana, attack.Id));
        }

        for (var i = 0; i < landed.Count; i++)
        {
            if (landed[i] is Node2D node)
            {
                _gameFeel.PlayEffect(attack.HitVfx, node.GlobalPosition + new Vector2(0, -70), _motor.Facing);
            }

            // crowds give less MP per extra target, so packed rooms do not flood the bar
            GainMp(attack.Mana * Modifiers.Scale(AmuletEffects.ManaGain) * ManaRules.Share(_feel.ManaTargetShares, i));
            ComboHits++;
            TotalHits++;
            _comboResetTicks = _feel.ComboResetTicks;
        }

        if (landed.Count > 0)
        {
            AudioService.Play(attack.GuardBreak ? SoundIds.HitHeavy : SoundIds.HitLight);
            _gameFeel.Hitstop(attack.HitstopTicks);
            _gameFeel.Shake(attack.ShakePixels);
        }
    }

    private void StepTimers()
    {
        _stunTicks = Mathf.Max(0, _stunTicks - 1);
        _dropTicks = Mathf.Max(0, _dropTicks - 1);
        _landingLagTicks = Mathf.Max(0, _landingLagTicks - 1);
        _guardHitTicks = Mathf.Max(0, _guardHitTicks - 1);
        _rushTicks = Mathf.Max(0, _rushTicks - 1);
        _skillInvulnerableTicks = Mathf.Max(0, _skillInvulnerableTicks - 1);
        Loadout.Step();
        ApplyModifiers();
        if (_downTicks > 0)
        {
            _downTicks--;
        }
        else
        {
            _getupTicks = Mathf.Max(0, _getupTicks - 1);
        }

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
        if (_respawnTicks <= 0)
        {
            return;
        }

        _respawnTicks--;
        if (_respawnTicks > 0)
        {
            return;
        }

        if (AutoRespawn)
        {
            Revive();
        }
        else
        {
            Fell?.Invoke();
        }
    }

    /// <summary>Full HP at the respawn point.</summary>
    public void Revive()
    {
        Health.Refill();
        GlobalPosition = _spawn;
        _motor.Knockback(0, 0);
        _invulnerableTicks = _feel.HurtInvulnerableTicks;
        Respawned?.Invoke();
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
            attack?.ViewClip ?? attack?.Id ?? string.Empty,
            _combo.SwingId,
            _combo.Progress,
            _guard.IsGuarding,
            _guard.CounterReady,
            _guardHitTicks > 0,
            _downTicks > 0 || Health.IsDead,
            _getupTicks > 0));
    }
}
