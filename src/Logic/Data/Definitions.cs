namespace LumiFeast.Logic.Data;

public sealed record RectDef(float X, float Y, float Width, float Height);

/// <param name="Speed">The one speed stat; 1 is the timing in the data. See SpeedRuleDef.</param>
public sealed record CharacterStatsDef(int MaxHp, int MaxMp, int Attack, float Defense, float Speed);

public sealed record MovementDef(
    float RunSpeed,
    float GroundAcceleration,
    float AirAcceleration,
    float Gravity,
    float FallGravityScale,
    float MaxFallSpeed,
    float JumpVelocity,
    float JumpCutScale,
    int CoyoteTicks,
    int JumpBufferTicks,
    float DashSpeed,
    int DashTicks,
    int DashCooldownTicks,
    int AirDashes,
    float AirAttackGravityScale,
    float SprintSpeed,
    int AirJumps,
    float AirJumpVelocity,
    float FastFallGravityScale,
    float FastFallMaxSpeed,
    int DropThroughTicks);

public sealed record CharacterCombatDef(string AttackSet, float MpPerSecond, int AttackBufferTicks);

public sealed record PlaceholderViewDef(
    string Sprite,
    float Height,
    string SwordSprite,
    float SwordLength,
    float HandX,
    float HandY,
    float SwordRestDegrees,
    float SwordGuardDegrees);

/// <param name="DamageScale">Share of damage taken through a guard.</param>
/// <param name="PerfectTicks">A hit this soon after raising the guard is a perfect guard.</param>
/// <param name="CounterTicks">Time to press Z after a perfect guard.</param>
public sealed record GuardDef(float DamageScale, int PerfectTicks, int CounterTicks, float PushSpeed, int PerfectHitstopTicks);

public sealed record BodyDef(float Width, float Height);

public sealed record CharacterDef(
    string Id,
    CharacterStatsDef Stats,
    MovementDef Movement,
    CharacterCombatDef Combat,
    GuardDef Guard,
    BodyDef Body,
    PlaceholderViewDef PlaceholderView);

public sealed record AttackDef(
    string Id,
    float DamageMultiplier,
    int StartupTicks,
    int ActiveTicks,
    int RecoveryTicks,
    int CancelFromTick,
    string? Next,
    float LungePixels,
    RectDef Hitbox,
    int HitstopTicks,
    float ShakePixels,
    float KnockbackSpeed,
    string Vfx,
    string HitVfx,
    float VfxX,
    float VfxY,
    float SwingFromDegrees,
    float SwingToDegrees,
    float LaunchSpeed,
    bool EndsOnLanding,
    string? NextHeavy,
    int ChainFromTick,
    int StunTicks,
    bool Knockdown,
    float Mana,
    float PlungeSpeed,
    float Stagger,
    int LandingLagTicks)
{
    public int TotalTicks => StartupTicks + ActiveTicks + RecoveryTicks;
}

/// <param name="First">Starts the combo on the ground.</param>
/// <param name="FirstAir">Z in the air.</param>
/// <param name="FirstAirHeavy">X in the air.</param>
/// <param name="FirstSprint">Z during a dash or the run after it.</param>
/// <param name="FirstHeavy">X on the ground.</param>
/// <param name="FirstCounter">Z right after a perfect guard.</param>
public sealed record AttackSetDef(string Id, string First, string FirstAir, string FirstAirHeavy, string FirstSprint, string FirstHeavy, string FirstCounter, IReadOnlyList<AttackDef> Attacks);

public sealed record MonsterStatsDef(int MaxHp, int Attack, float Defense, float Speed);

/// <param name="SuperArmor">Armored during its own windup and strike.</param>
/// <param name="StoicThreshold">Hits before it armors up and fights back; 0 means never.</param>
/// <param name="StaggerMax">Stagger that makes it dizzy; 0 means it has no stagger bar.</param>
/// <param name="StoicHpMarks">HP fractions, high to low, that also trigger armor once each.</param>
public sealed record MonsterAiDef(
    string Behavior,
    float AggroRange,
    float AttackRange,
    int WindupTicks,
    int ActiveTicks,
    int RecoveryTicks,
    int CooldownTicks,
    float StunScale,
    float LungeSpeed,
    float HopVelocity,
    int HopIntervalTicks,
    float HoverHeight,
    bool SuperArmor,
    int StoicThreshold,
    int StoicTicks,
    int StoicResetTicks,
    IReadOnlyList<float> StoicHpMarks,
    int DownTicks,
    int GetupArmorTicks,
    float StaggerMax,
    int StaggerBreakTicks,
    float StaggerDamageScale);

public sealed record MonsterAttackDef(float DamageMultiplier, RectDef Hitbox, float KnockbackSpeed, int HitstopTicks, string HitVfx);

public sealed record MonsterArtDef(string Folder, float Height, bool FacesLeft, string Idle, string Windup, string Hit, string Healed);

public sealed record MonsterDef(
    string Id,
    MonsterStatsDef Stats,
    MonsterAiDef Ai,
    MonsterAttackDef Attack,
    BodyDef Body,
    float KnockbackScale,
    bool Immortal,
    MonsterArtDef Art);

public sealed record DifficultyDef(string Id, float EnemyHp, float EnemyDamage, float EnemySpeed, float EnemyCooldown);

public sealed record DifficultySetDef(string Default, IReadOnlyList<DifficultyDef> Modes);

/// <summary>How much of the speed stat reaches attacks and movement, and its ceiling.</summary>
public sealed record SpeedRuleDef(float AttackWeight, float MoveWeight, float MaxSpeed);

public sealed record ScalingDef(float HpGrowthPerTier, float DamageGrowthPerTier, int MinTier, int MaxTier, SpeedRuleDef Speed);

public sealed record GameFeelDef(
    int HurtStunTicks,
    int HurtInvulnerableTicks,
    float HurtKnockbackSpeed,
    float HurtKnockbackUp,
    int HurtHitstopTicks,
    int RespawnTicks,
    int HealFadeTicks,
    float EnemyGravity,
    float KnockbackDecay,
    float ShakeDecay,
    int ComboResetTicks,
    float DamageNumberRise,
    int DamageNumberTicks,
    float JuggleGravityScale,
    string GuardVfx);

public sealed record VfxDef(string Id, int Frames, int TicksPerFrame, float Scale);

public sealed record VfxSetDef(string Folder, IReadOnlyList<VfxDef> Effects);

public sealed record LayerDef(string Sprite, float ScrollScale, int ZIndex);

public sealed record PlatformDef(string Sprite, float X, float Y, float Width, float TopInset);

public sealed record SpawnDef(string Monster, float X);

public sealed record WaveDef(IReadOnlyList<SpawnDef> Spawns);

/// <summary>A closed fight: walls rise at the trigger and fall when every wave is healed.</summary>
public sealed record ArenaDef(float TriggerX, float LeftX, float RightX, int WaveDelayTicks, IReadOnlyList<WaveDef> Waves);

public sealed record StageDef(
    string Id,
    int Tier,
    float Width,
    float GroundY,
    float PlayerSpawnX,
    int MonsterRespawnTicks,
    IReadOnlyList<LayerDef> Layers,
    IReadOnlyList<PlatformDef> Platforms,
    IReadOnlyList<SpawnDef> Spawns,
    ArenaDef? Arena);

public sealed record InputActionDef(string Action, IReadOnlyList<string> Keys, IReadOnlyList<int> PadButtons, IReadOnlyList<int> PadAxes);

public sealed record InputSetDef(float PadDeadzone, IReadOnlyList<InputActionDef> Actions);
