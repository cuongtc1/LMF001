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

public sealed record CharacterCombatDef(string AttackSet, float MpPerSecond, int AttackBufferTicks, string SkillSet);

public sealed record SkillDef(string Id, string Attack, float MpCost, int CooldownTicks, int InvulnerableTicks);

/// <param name="RiseSpeed">Upward launch speed; with <paramref name="Gravity"/> the shot flies in an arc.</param>
/// <param name="RehitTicks">Above 0: hits the same monsters again every this many ticks.</param>
/// <param name="Pull">Hits pull monsters toward the shot (Egg Whisk).</param>
/// <param name="HitSize">Square hit area; 0 keeps the default.</param>
/// <param name="BurstSize">Above 0: the shot bursts in a square this wide when it lands or ends (Berry Bomb).</param>
public sealed record ProjectileDef(string Id, string Sprite, float Speed, int Ticks, float Height, float DamageMultiplier, bool Pierce, float Stagger,
    float Gravity = 0, float RiseSpeed = 0, int RehitTicks = 0, bool Pull = false, float HitSize = 0, float BurstSize = 0);

/// <param name="IconFolder">Each skill's icon is {IconFolder}/{skill id}.png.</param>
public sealed record SkillSetDef(string Id, int Slots, IReadOnlyList<SkillDef> Skills, IReadOnlyList<ProjectileDef> Projectiles, string IconFolder = "");

/// <param name="MinHpRatio">The effect works only while HP is at least this share of max HP.</param>
/// <param name="Target">Attack id the effect is limited to (projectiles, finisher effects); null for all.</param>
/// <param name="Ticks">How long a timed effect lasts (slows, short buffs).</param>
public sealed record AmuletEffectDef(string Kind, float Value, string? Target = null, string? Projectile = null, float MinHpRatio = 0, int Ticks = 0);

public sealed record AmuletDef(string Id, IReadOnlyList<AmuletEffectDef> Effects);

/// <param name="IconFolder">Each amulet's icon is {IconFolder}/{amulet id}.png.</param>
public sealed record AmuletSetDef(string Id, IReadOnlyList<AmuletDef> Amulets, string IconFolder = "");

/// <param name="DefaultAttack">Clip for attacks that have no animation of their own yet.</param>
public sealed record SpineViewDef(
    string Skeleton,
    string Atlas,
    string Skin,
    float Scale,
    float MixSeconds,
    string Idle,
    string Run,
    string Sprint,
    string Jump,
    string Fall,
    string Land,
    string Dash,
    string Guard,
    string GuardHit,
    string Hurt,
    string Knockdown,
    string Getup,
    string DefaultAttack,
    float SprintFromSpeed);

/// <param name="DamageScale">Share of damage taken through a guard.</param>
/// <param name="PerfectTicks">A hit this soon after raising the guard is a perfect guard.</param>
/// <param name="CounterTicks">Time to press Z after a perfect guard.</param>
/// <param name="AutoCounter">Every block makes Lumi counter by herself, no Z press needed (normally off; the Steam Bath amulet turns it on).</param>
public sealed record GuardDef(float DamageScale, int PerfectTicks, int CounterTicks, float PushSpeed, int PerfectHitstopTicks, int HitTicks, bool AutoCounter = false);

public sealed record BodyDef(float Width, float Height);

public sealed record CharacterDef(
    string Id,
    CharacterStatsDef Stats,
    MovementDef Movement,
    CharacterCombatDef Combat,
    GuardDef Guard,
    BodyDef Body,
    SpineViewDef SpineView);

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
    float LaunchSpeed,
    bool EndsOnLanding,
    string? NextHeavy,
    int ChainFromTick,
    int StunTicks,
    bool Knockdown,
    float Mana,
    float PlungeSpeed,
    float Stagger,
    int LandingLagTicks,
    bool GuardBreak,
    string? ViewClip = null,
    int RehitTicks = 0,
    string? Projectile = null)
{
    public int TotalTicks => StartupTicks + ActiveTicks + RecoveryTicks;
}

/// <param name="First">Starts the combo on the ground.</param>
/// <param name="FirstAir">Z in the air.</param>
/// <param name="FirstAirUp">Z while rising with Up held.</param>
/// <param name="FirstAirHeavy">X in the air.</param>
/// <param name="FirstSprint">Z during a dash or the run after it.</param>
/// <param name="FirstHeavy">X on the ground.</param>
/// <param name="FirstCounter">Z right after a perfect guard.</param>
public sealed record AttackSetDef(string Id, string First, string FirstAir, string FirstAirUp, string FirstAirHeavy, string FirstSprint, string FirstHeavy, string FirstCounter, IReadOnlyList<AttackDef> Attacks);

public sealed record MonsterStatsDef(int MaxHp, int Attack, float Defense, float Speed);

/// <summary>One boss move. Kind says how the game plays it; numbers come from here.</summary>
public sealed record MonsterMoveDef(
    string Id,
    string Kind,
    float MinRange,
    float MaxRange,
    float Weight,
    int WindupTicks,
    int ActiveTicks,
    int RecoveryTicks,
    float LungeSpeed,
    float DamageMultiplier,
    RectDef Hitbox,
    bool Knockdown,
    float StealMp,
    string? ProjectileSprite,
    int ProjectileTicks,
    float ProjectileGravity,
    float ProjectileHeight,
    int MarkerTicks);

/// <param name="SuperArmor">Armored during its own windup and strike.</param>
/// <param name="StoicThreshold">Hits before it armors up and fights back; 0 means never.</param>
/// <param name="FrontGuardScale">Share of damage taken from the front; 1 means no guard.</param>
/// <param name="StaggerMax">Stagger that makes it dizzy; 0 means it has no stagger bar.</param>
/// <param name="StoicHpMarks">HP fractions, high to low, that also trigger armor once each.</param>
/// <param name="Boss">Always fights, uses the HUD bar, ignores attack turns.</param>
/// <param name="KeepDistance">Ranged monsters step back while reloading when Lumi is closer than this.</param>
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
    float StaggerDamageScale,
    float FrontGuardScale,
    bool BouncesOffWalls,
    float PhaseTwoHp = 0,
    float PhaseTwoSpeed = 1,
    IReadOnlyList<MonsterMoveDef>? Moves = null,
    bool Climbs = false,
    bool Boss = false,
    float KeepDistance = 0);

/// <summary>How climbing monsters follow Lumi onto ledges (all pixels; ticks at 60 per second).</summary>
/// <param name="StepHeight">Height difference that counts as "on another level".</param>
/// <param name="WaitTicks">Lumi must stay on the other level this long first, so a quick hop still dodges.</param>
/// <param name="ReachHeight">Ground monsters only swing when Lumi is within this height.</param>
public sealed record MonsterClimbDef(float StepHeight, int WaitTicks, float JumpReach, float MaxJumpHeight, float Clearance, int CooldownTicks, int DropTicks, float ReachHeight);

public sealed record MonsterAttackDef(float DamageMultiplier, RectDef Hitbox, float KnockbackSpeed, int HitstopTicks, string HitVfx, bool Knockdown);

/// <param name="Motion">Scales the code-driven animation: above 1 bouncy (slimes), below 1 stiff (treants).</param>
public sealed record MonsterArtDef(string Folder, float Height, bool FacesLeft, string Idle, string Windup, string Hit, string Healed, float Motion = 1);

/// <summary>Amounts of the code-driven monster animation. Squash values are shares of the image size; angles in radians.</summary>
public sealed record MonsterPoseDef(
    float Breathe,
    int BreatheTicks,
    float StepSpeed,
    int StepTicks,
    float StepHop,
    float StepSquash,
    float Lean,
    float WindupSquash,
    float WindupShake,
    float ShakeSpeed,
    float AttackStretch,
    float AirStretch,
    float LandSquash,
    int LandTicks,
    float HitSquash,
    int HitTicks,
    float DizzyWobble);

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

/// <summary>
/// Monster scaling by stage level (16_level_and_power.md). Lumi has no level: her stats grow through skill tree nodes,
/// unlocked chapter by chapter, so each chapter's stage levels are balanced against what that chapter's tree allows.
/// </summary>
/// <param name="HpGrowthPerLevel">Monster HP grows by this factor per stage level above 1.</param>
/// <param name="DamageGrowthPerLevel">Monster attack grows by this factor per stage level above 1.</param>
public sealed record ScalingDef(float HpGrowthPerLevel, float DamageGrowthPerLevel, int MinLevel, int MaxLevel, SpeedRuleDef Speed);

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
    string GuardVfx,
    int PlayerDownTicks,
    int PlayerGetupTicks,
    MonsterPoseDef MonsterPose,
    MonsterClimbDef MonsterClimb,
    IReadOnlyList<float> ManaTargetShares,
    MonsterAwarenessDef MonsterAwareness,
    MonsterCrowdDef MonsterCrowd);

/// <param name="MaxAttackers">Monsters of one room that may attack at the same moment.</param>
/// <param name="WaitMargin">Monsters waiting for a turn hold about this far outside their attack range.</param>
/// <param name="BackOffSpeedScale">Share of speed when a waiting monster steps back to its spot.</param>
/// <param name="AppearTicks">A new monster rises out of a light column for this long before it can act or be hit.</param>
/// <param name="FlankCheckTicks">How often an agile waiting monster checks whether to leap to Lumi's other side.</param>
/// <param name="FlankCrowdGap">It leaps when its side has at least this many more fighting monsters than the other.</param>
/// <param name="FlankLeapVelocity">Upward speed of the leap over Lumi.</param>
/// <param name="FlankLandBeyond">How far past Lumi it lands.</param>
public sealed record MonsterCrowdDef(int MaxAttackers, float WaitMargin, float BackOffSpeedScale, int AppearTicks, int FlankCheckTicks, int FlankCrowdGap, float FlankLeapVelocity, float FlankLandBeyond);

/// <summary>Whether a waiting monster should leap over Lumi to her emptier side (15_core_gameplay.md, rule 4).</summary>
public static class FlankRule
{
    /// <param name="side">-1 when the monster is left of Lumi, 1 when right.</param>
    public static bool ShouldLeap(int side, int left, int right, int crowdGap) =>
        side < 0 ? left - right >= crowdGap : right - left >= crowdGap;
}

/// <param name="SightRange">How far ahead a monster sees Lumi.</param>
/// <param name="SightHeight">How far above or below its feet it sees her.</param>
/// <param name="BackSense">Lumi this close behind is noticed too.</param>
/// <param name="NoticeTicks">The "!" pause before the fight starts.</param>
/// <param name="AlertRadius">Monsters this close to one that notices Lumi notice her too.</param>
/// <param name="LoseRange">Farther than this, the monster starts to forget Lumi.</param>
/// <param name="ForgetTicks">Time beyond the lose range before it gives up.</param>
/// <param name="PatrolDistance">How far from its spawn point it walks while unaware.</param>
/// <param name="PatrolSpeedScale">Share of its speed while patrolling.</param>
public sealed record MonsterAwarenessDef(float SightRange, float SightHeight, float BackSense, int NoticeTicks, float AlertRadius, float LoseRange, int ForgetTicks, float PatrolDistance, float PatrolSpeedScale, int PatrolPauseTicks);

/// <summary>MP share for the n-th target of one swing; the last share covers every further target.</summary>
public static class ManaRules
{
    public static float Share(IReadOnlyList<float> shares, int targetIndex) =>
        shares.Count == 0 ? 1 : shares[Math.Min(targetIndex, shares.Count - 1)];
}

/// <summary>One sound: a random file from <paramref name="Files"/> plays each time, with a small random pitch change.</summary>
/// <param name="VolumeDb">Loudness change; 0 keeps the file as it is.</param>
/// <param name="PitchSpread">0.05 plays between 95% and 105% pitch, so repeated sounds do not tire the ear.</param>
public sealed record SoundDef(string Id, IReadOnlyList<string> Files, float VolumeDb = 0, float PitchSpread = 0);

public sealed record SoundSetDef(string Folder, IReadOnlyList<SoundDef> Sounds);

public sealed record VfxDef(string Id, int Frames, int TicksPerFrame, float Scale);

public sealed record VfxSetDef(string Folder, IReadOnlyList<VfxDef> Effects);

public sealed record LayerDef(string Sprite, float ScrollScale, int ZIndex);

public sealed record PlatformDef(string Sprite, float X, float Y, float Width, float TopInset);

/// <param name="Y">Feet height when the monster starts on a ledge; null starts it on the room floor.</param>
public sealed record SpawnDef(string Monster, float X, float? Y = null);

public sealed record WaveDef(IReadOnlyList<SpawnDef> Spawns);

/// <summary>A closed fight: walls rise at the trigger and fall when every wave is healed.</summary>
public sealed record ArenaDef(float TriggerX, float LeftX, float RightX, int WaveDelayTicks, IReadOnlyList<WaveDef> Waves);

public sealed record StageDef(
    string Id,
    int Level,
    float Width,
    float GroundY,
    float PlayerSpawnX,
    int MonsterRespawnTicks,
    IReadOnlyList<LayerDef> Layers,
    IReadOnlyList<PlatformDef> Platforms,
    IReadOnlyList<SpawnDef> Spawns,
    ArenaDef? Arena);

/// <param name="Kind">One of <c>RoomKinds</c>; a layout cell asks for a kind.</param>
/// <param name="RewardOnClear">Fight rooms offer a reward when healed; a treasure room gives it from its chest.</param>
/// <param name="Up">An opening in the top edge: Lumi climbs out, or falls in from the room above.</param>
/// <param name="Down">An open bottom (sky rooms): Lumi falls out, or arrives from the room below.</param>
/// <param name="ChestY">Feet of the chest when it stands on a ledge; the ground otherwise.</param>
/// <param name="HasFloor">Sky rooms have no ground, only ledges.</param>
public sealed record RoomDef(
    string Id,
    float Width,
    float GroundY,
    IReadOnlyList<LayerDef> Layers,
    IReadOnlyList<PlatformDef> Platforms,
    IReadOnlyList<WaveDef> Waves,
    IReadOnlyList<float>? Crates = null,
    float? ChestX = null,
    bool RewardOnClear = true,
    ChallengeDef? Challenge = null,
    float? ShrineX = null,
    string Kind = "fight",
    VerticalExitDef? Up = null,
    VerticalExitDef? Down = null,
    float? ChestY = null,
    bool HasFloor = true,
    float Height = RoomDef.ScreenHeight)
{
    /// <summary>One screen; taller rooms scroll up and down with the camera.</summary>
    public const float ScreenHeight = 1080;
}

/// <summary>A gap in the top or bottom edge of a room.</summary>
/// <param name="X">Center of the gap.</param>
/// <param name="Width">Lumi must pass through this span.</param>
/// <param name="SpawnX">Where Lumi appears when she comes in through this gap.</param>
/// <param name="SpawnY">Feet height on arrival: above the top edge to fall in, or on a ledge above an open bottom.</param>
public sealed record VerticalExitDef(float X, float Width, float SpawnX, float SpawnY);

/// <summary>A hand-made room arrangement; rooms of each kind are drawn at random into its cells.</summary>
public sealed record LayoutDef(string Id, IReadOnlyList<LayoutCellDef> Cells);

/// <param name="Y">Grid row; -1 is above the main row.</param>
public sealed record LayoutCellDef(int X, int Y, string Kind);

/// <summary>A room trial in the style of Little Noah's Monster Lairs and no-hit rooms.</summary>
/// <param name="Kind">One of <c>ChallengeKinds</c>.</param>
/// <param name="LeverX">Where the lair lever stands; unused by no_hit.</param>
/// <param name="LevelBonus">Added to the stage level for the trial's monsters.</param>
/// <param name="ChestX">Where the reward chest appears on a win.</param>
public sealed record ChallengeDef(string Kind, float LeverX, int LevelBonus, float ChestX);

/// <param name="HpChance">Share of crates that drop an HP token; the rest drop MP.</param>
/// <param name="CrateBrokenSprite">Drawn at the same scale as the crate, so both come from one sheet.</param>
public sealed record PickupDef(
    string CrateSprite,
    string CrateBrokenSprite,
    float CrateHeight,
    int CrateHits,
    float HpChance,
    float HpHeal,
    float MpGain,
    string HpTokenSprite,
    string MpTokenSprite,
    string ChestSprite,
    string ChestOpenSprite,
    float ChestHeight,
    string LeverSprite,
    string LeverOnSprite,
    float LeverHeight,
    float BigChestHeight);

/// <param name="ExitZone">Standing this close to an open edge walks Lumi into the next room.</param>
/// <param name="EdgeSpawnInset">Where Lumi appears in the next room, measured from the edge she came through.</param>
public sealed record RunDef(
    string Id,
    int Level,
    int WaveDelayTicks,
    float PlayerSpawnX,
    string GateSprite,
    float GateHeight,
    string ShrineSprite,
    string ShrineUsedSprite,
    float ShrineHeight,
    float ExitZone,
    float EdgeSpawnInset,
    float VerticalExitMargin,
    PickupDef Pickups,
    string AmuletSet,
    int RewardChoices,
    IReadOnlyList<RoomDef> Rooms,
    IReadOnlyList<LayoutDef> Layouts);

public sealed record InputActionDef(string Action, IReadOnlyList<string> Keys, IReadOnlyList<int> PadButtons, IReadOnlyList<int> PadAxes);

public sealed record InputSetDef(float PadDeadzone, IReadOnlyList<InputActionDef> Actions);
