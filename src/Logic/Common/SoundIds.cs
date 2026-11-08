namespace LumiFeast.Logic.Common;

/// <summary>Sound ids the code plays. Each must exist in data/audio/sfx.json (checked at boot); files and volume live there.</summary>
public static class SoundIds
{
    public const string SwingLight = "swing_light";
    public const string SwingHeavy = "swing_heavy";
    public const string HitLight = "hit_light";
    public const string HitHeavy = "hit_heavy";
    public const string Jump = "jump";
    public const string Land = "land";
    public const string Dash = "dash";
    public const string GuardBlock = "guard_block";
    public const string GuardPerfect = "guard_perfect";
    public const string Hurt = "hurt";
    public const string Skill = "skill";
    public const string MonsterAttack = "monster_attack";
    public const string MonsterHealed = "monster_healed";
    public const string CrateHit = "crate_hit";
    public const string CrateBreak = "crate_break";
    public const string ChestOpen = "chest_open";
    public const string TokenPickup = "token_pickup";
    public const string Lever = "lever";
    public const string GateShut = "gate_shut";
    public const string GateOpen = "gate_open";
    public const string Shrine = "shrine";
    public const string UiMove = "ui_move";
    public const string UiConfirm = "ui_confirm";
    public const string UiBack = "ui_back";
    public const string MapOpen = "map_open";
    public const string RoomHealed = "room_healed";
    public const string RunClear = "run_clear";
    public const string RunDefeat = "run_defeat";

    public static readonly IReadOnlyList<string> All =
    [
        SwingLight, SwingHeavy, HitLight, HitHeavy, Jump, Land, Dash, GuardBlock, GuardPerfect, Hurt, Skill,
        MonsterAttack, MonsterHealed, CrateHit, CrateBreak, ChestOpen, TokenPickup, Lever, GateShut, GateOpen, Shrine,
        UiMove, UiConfirm, UiBack, MapOpen, RoomHealed, RunClear, RunDefeat,
    ];
}
