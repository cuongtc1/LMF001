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
