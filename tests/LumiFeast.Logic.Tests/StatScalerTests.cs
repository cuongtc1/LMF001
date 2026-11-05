using LumiFeast.Logic.Combat;
using LumiFeast.Logic.Data;
using LumiFeast.Logic.Stats;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class StatScalerTests
{
    private static readonly ScalingDef Scaling = new(2f, 1.5f, 1, 9);
    private static readonly DifficultyDef Normal = new("normal", 1, 1, 1, 1);
    private static readonly DifficultyDef Hard = new("hard", 1.5f, 2, 1.2f, 0.5f);

    private static MonsterDef Monster => TestData.Game.Monster("honey_slime") with
    {
        Stats = new MonsterStatsDef(100, 10, 0.1f, 200),
    };

    [Fact]
    public void TierOneNormalKeepsBaseNumbers()
    {
        var stats = StatScaler.ForMonster(Monster, 1, Normal, Scaling);
        Assert.Equal((100, 10, 200f, 1f), (stats.MaxHp, stats.Attack, stats.Speed, stats.CooldownScale));
    }

    [Fact]
    public void TierAndDifficultyMultiply()
    {
        var stats = StatScaler.ForMonster(Monster, 3, Hard, Scaling);
        Assert.Equal(600, stats.MaxHp);    // 100 x 2^2 x 1.5
        Assert.Equal(45, stats.Attack);    // 10 x 1.5^2 x 2
        Assert.Equal(240f, stats.Speed, 3);
        Assert.Equal(0.5f, stats.CooldownScale);
    }

    [Theory]
    [InlineData(20, 1.5f, 0f, 30)]
    [InlineData(20, 1f, 0.5f, 10)]
    [InlineData(1, 0.1f, 0.9f, 1)]
    public void DamageNeverDropsBelowOne(int attack, float multiplier, float defense, int expected) =>
        Assert.Equal(expected, DamageCalculator.Compute(attack, multiplier, defense));
}
