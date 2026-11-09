using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Stats;

/// <summary>A monster's numbers after tier and difficulty.</summary>
public sealed record MonsterStats(int MaxHp, int Attack, float Defense, float Speed, float CooldownScale);

/// <summary>What the speed stat becomes: attack ticks per game tick, and the movement tempo.</summary>
public readonly record struct SpeedStats(float Attack, float Move);

/// <summary>The only place that turns base stats into in-game stats.</summary>
public static class StatScaler
{
    public static SpeedStats ForSpeed(float speed, SpeedRuleDef rule)
    {
        var bonus = Math.Min(speed, rule.MaxSpeed) - 1;
        return new SpeedStats(1 + bonus * rule.AttackWeight, 1 + bonus * rule.MoveWeight);
    }

    public static MonsterStats ForMonster(MonsterDef monster, int level, DifficultyDef difficulty, ScalingDef scaling)
    {
        var steps = level - 1;
        var hp = monster.Stats.MaxHp * MathF.Pow(scaling.HpGrowthPerLevel, steps) * difficulty.EnemyHp;
        var attack = monster.Stats.Attack * MathF.Pow(scaling.DamageGrowthPerLevel, steps) * difficulty.EnemyDamage;
        return new MonsterStats(
            (int)MathF.Round(hp),
            (int)MathF.Round(attack),
            monster.Stats.Defense,
            monster.Stats.Speed * difficulty.EnemySpeed,
            difficulty.EnemyCooldown);
    }

    /// <summary>The level Lumi counts as inside a stage: her own, but at most <see cref="ScalingDef.LevelLeadCap"/> above it.</summary>
    public static int EffectiveLevel(int playerLevel, int stageLevel, ScalingDef scaling) =>
        Math.Clamp(Math.Min(playerLevel, stageLevel + scaling.LevelLeadCap), scaling.MinLevel, scaling.MaxLevel);

    /// <summary>Lumi's max HP and attack at a level (level 1 = the character data).</summary>
    public static (int MaxHp, int Attack) ForPlayer(CharacterStatsDef stats, int level, ScalingDef scaling)
    {
        var steps = level - 1;
        return ((int)MathF.Round(stats.MaxHp * MathF.Pow(scaling.PlayerHpGrowthPerLevel, steps)),
                (int)MathF.Round(stats.Attack * MathF.Pow(scaling.PlayerAttackGrowthPerLevel, steps)));
    }
}
