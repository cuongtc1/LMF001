namespace LumiFeast.Logic.Common;

/// <summary>Keys of data/messages/messages.properties. A test checks that each one exists there.</summary>
public static class MessageKeys
{
    public const string DataFileMissing = "data.file_missing";
    public const string DataFileInvalid = "data.file_invalid";
    public const string DataUnknownId = "data.unknown_id";
    public const string DataDuplicateId = "data.duplicate_id";
    public const string DataBadValue = "data.bad_value";
    public const string DataLoaded = "data.loaded";
    public const string DataReloaded = "data.reloaded";
    public const string GameBooted = "game.booted";
    public const string StageLoaded = "stage.loaded";
    public const string AssetMissing = "asset.missing";
    public const string CombatPlayerHit = "combat.player_hit";
    public const string CombatMonsterHit = "combat.monster_hit";
    public const string CombatMonsterHealed = "combat.monster_healed";
    public const string CombatStoic = "combat.stoic";
    public const string CombatDizzy = "combat.dizzy";
    public const string CombatGuardBlock = "combat.guard_block";
    public const string CombatGuardPerfect = "combat.guard_perfect";
    public const string AutoplayStarted = "debug.autoplay_started";
    public const string AutoplayFinished = "debug.autoplay_finished";
    public const string DebugTitle = "debug.title";
    public const string DebugDifficulty = "debug.difficulty";
    public const string DebugTier = "debug.tier";
    public const string DebugGodMode = "debug.god_mode";
    public const string DebugAirJump = "debug.air_jump";
    public const string DebugHeal = "debug.heal";
    public const string DebugArena = "debug.arena";
    public const string DebugRespawn = "debug.respawn";
    public const string DebugReload = "debug.reload";
    public const string DebugHelp = "debug.help";
    public const string HudCombo = "hud.combo";
    public const string HudWave = "hud.wave";
    public const string HudRoom = "hud.room";
    public const string HudBoss = "hud.boss";
    public const string RunDoorPrompt = "run.door_prompt";
    public const string RunStarted = "run.started";
    public const string RunCleared = "run.cleared";
    public const string DebugPrototype = "debug.prototype";
    public const string HudClear = "hud.clear";
    public const string ArenaWave = "stage.arena_wave";
    public const string ArenaCleared = "stage.arena_cleared";

    public static readonly string[] All =
    [
        DataFileMissing, DataFileInvalid, DataUnknownId, DataDuplicateId, DataBadValue, DataLoaded, DataReloaded,
        GameBooted, StageLoaded, AssetMissing, CombatPlayerHit, CombatMonsterHit, CombatMonsterHealed,
        AutoplayStarted, AutoplayFinished, DebugTitle, DebugDifficulty, DebugTier, DebugGodMode, DebugAirJump, DebugHeal, DebugArena, CombatStoic, CombatDizzy, CombatGuardBlock, CombatGuardPerfect, HudRoom, HudBoss, RunDoorPrompt, RunStarted, RunCleared, DebugPrototype, HudWave, HudClear, ArenaWave, ArenaCleared,
        DebugRespawn, DebugReload, DebugHelp, HudCombo,
    ];
}
