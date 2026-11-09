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
    public const string CombatFlank = "combat.flank";
    public const string CombatPhaseTwo = "combat.phase_two";
    public const string CombatGuardBlock = "combat.guard_block";
    public const string CombatGuardPerfect = "combat.guard_perfect";
    public const string AutoplayStarted = "debug.autoplay_started";
    public const string AutoplayFinished = "debug.autoplay_finished";
    public const string DebugTitle = "debug.title";
    public const string DebugDifficulty = "debug.difficulty";
    public const string DebugTier = "debug.tier";
    public const string DebugPlayerLevel = "debug.player_level";
    public const string DebugGodMode = "debug.god_mode";
    public const string DebugAirJump = "debug.air_jump";
    public const string DebugHeal = "debug.heal";
    public const string DebugArena = "debug.arena";
    public const string DebugRespawn = "debug.respawn";
    public const string DebugReload = "debug.reload";
    public const string DebugHelp = "debug.help";
    public const string HudCombo = "hud.combo";
    public const string HudWave = "hud.wave";
    public const string RewardSkill = "reward.skill";
    public const string RewardAmulet = "reward.amulet";
    public const string HudSkillSlot = "hud.skill_slot";
    public const string HudSkillReady = "hud.skill_ready";
    public const string HudSkillCooldown = "hud.skill_cooldown";
    public const string HudSkillEmpty = "hud.skill_empty";
    public const string SkillPrefix = "skill.";
    public const string AmuletPrefix = "amulet.";
    public const string DescriptionSuffix = ".desc";
    public const string HudBoss = "hud.boss";
    public const string RunArea = "run.area";
    public const string RunShrinePrompt = "run.shrine_prompt";
    public const string RunRoomHealed = "run.room_healed";
    public const string RunMoved = "run.moved";
    public const string MapHere = "map.here";
    public const string MapShrine = "map.shrine";
    public const string MapTreasure = "map.treasure";
    public const string MapLair = "map.lair";
    public const string MapElite = "map.elite";
    public const string MapTrial = "map.trial";
    public const string MapBoss = "map.boss";
    public const string MapHint = "map.hint";
    public const string RunStarted = "run.started";
    public const string RunCleared = "run.cleared";
    public const string RewardPicked = "run.reward_picked";
    public const string DebugPrototype = "debug.prototype";
    public const string HudClear = "hud.clear";
    public const string ArenaWave = "stage.arena_wave";
    public const string ArenaCleared = "stage.arena_cleared";
    public const string ResultDefeatTitle = "result.defeat_title";
    public const string ResultDefeatLine = "result.defeat_line";
    public const string ResultClearTitle = "result.clear_title";
    public const string ResultClearLine = "result.clear_line";
    public const string ResultTime = "result.time";
    public const string ResultRooms = "result.rooms";
    public const string ResultHealed = "result.healed";
    public const string ResultCombo = "result.combo";
    public const string ResultLoadout = "result.loadout";
    public const string ResultRetry = "result.retry";
    public const string ResultAgain = "result.again";
    public const string RunDefeated = "run.defeated";
    public const string TrialOffer = "trial.offer";
    public const string TrialStarted = "trial.started";
    public const string TrialLair = "trial.lair";
    public const string TrialNoHit = "trial.no_hit";
    public const string TrialWon = "trial.won";
    public const string TrialBonusLost = "trial.bonus_lost";

    public static readonly string[] All =
    [
        DataFileMissing, DataFileInvalid, DataUnknownId, DataDuplicateId, DataBadValue, DataLoaded, DataReloaded,
        GameBooted, StageLoaded, AssetMissing, CombatPlayerHit, CombatMonsterHit, CombatMonsterHealed,
        AutoplayStarted, AutoplayFinished, DebugTitle, DebugDifficulty, DebugTier, DebugPlayerLevel, DebugGodMode, DebugAirJump, DebugHeal, DebugArena, CombatStoic, CombatDizzy, CombatFlank, CombatPhaseTwo, CombatGuardBlock, CombatGuardPerfect, HudBoss, RunArea, RunShrinePrompt, RunRoomHealed, RunMoved, MapHere, MapShrine, MapTreasure, MapLair, MapElite, MapTrial, MapBoss, MapHint, RunStarted, RunCleared, RewardPicked, DebugPrototype, RewardSkill, RewardAmulet, HudSkillSlot, HudSkillReady, HudSkillCooldown, HudSkillEmpty, HudWave, HudClear, ArenaWave, ArenaCleared,
        DebugRespawn, DebugReload, DebugHelp, HudCombo,
        ResultDefeatTitle, ResultDefeatLine, ResultClearTitle, ResultClearLine, ResultTime, ResultRooms, ResultHealed, ResultCombo, ResultLoadout, ResultRetry, ResultAgain, RunDefeated,
        TrialOffer, TrialStarted, TrialLair, TrialNoHit, TrialWon, TrialBonusLost,
    ];
}
