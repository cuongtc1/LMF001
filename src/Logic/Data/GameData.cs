using LumiFeast.Logic.Common;

namespace LumiFeast.Logic.Data;

/// <summary>Everything loaded from data/. Lookups by id fail with a clear error, never a null.</summary>
public sealed class GameData
{
    public required DifficultySetDef Difficulties { get; init; }

    public required ScalingDef Scaling { get; init; }

    public required GameFeelDef Feel { get; init; }

    public required InputSetDef Input { get; init; }

    public required VfxSetDef Vfx { get; init; }

    public required IReadOnlyDictionary<string, CharacterDef> Characters { get; init; }

    public required IReadOnlyDictionary<string, AttackSetDef> AttackSets { get; init; }

    public required IReadOnlyDictionary<string, MonsterDef> Monsters { get; init; }

    public required IReadOnlyDictionary<string, StageDef> Stages { get; init; }

    public CharacterDef Character(string id) => Find(Characters, id);

    public AttackSetDef AttackSet(string id) => Find(AttackSets, id);

    public MonsterDef Monster(string id) => Find(Monsters, id);

    public StageDef Stage(string id) => Find(Stages, id);

    public DifficultyDef Difficulty(string id) =>
        Difficulties.Modes.FirstOrDefault(mode => mode.Id == id) ?? throw new GameDataException(MessageKeys.DataUnknownId, id);

    private static T Find<T>(IReadOnlyDictionary<string, T> items, string id) =>
        items.TryGetValue(id, out var item) ? item : throw new GameDataException(MessageKeys.DataUnknownId, id);
}
