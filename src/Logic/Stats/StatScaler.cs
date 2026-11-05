using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Stats;

/// <summary>A monster's numbers after tier and difficulty.</summary>
public sealed record MonsterStats(int MaxHp, int Attack, float Defense, float Speed, float CooldownScale);

/// <summary>The only place that turns base stats into in-game stats.</summary>
public static class StatScaler
{
    public static MonsterStats ForMonster(MonsterDef monster, int tier, DifficultyDef difficulty, ScalingDef scaling)
    {
        var steps = tier - 1;
        var hp = monster.Stats.MaxHp * MathF.Pow(scaling.HpGrowthPerTier, steps) * difficulty.EnemyHp;
        var attack = monster.Stats.Attack * MathF.Pow(scaling.DamageGrowthPerTier, steps) * difficulty.EnemyDamage;
        return new MonsterStats(
            (int)MathF.Round(hp),
            (int)MathF.Round(attack),
            monster.Stats.Defense,
            monster.Stats.Speed * difficulty.EnemySpeed,
            difficulty.EnemyCooldown);
    }
}
