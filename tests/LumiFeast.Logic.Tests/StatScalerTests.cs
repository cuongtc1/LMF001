using LumiFeast.Logic.Combat;
using LumiFeast.Logic.Data;
using LumiFeast.Logic.Stats;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class StatScalerTests
{
    private static readonly SpeedRuleDef Speed = new(1, 0.35f, 1.5f);
    private static readonly ScalingDef Scaling = new(2f, 1.5f, 1, 9, Speed);
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

    [Fact]
    // early-game rhythm, as in Elsword: Z every 0.35 s; gear and buffs may bring it to 0.3 later
    public void ComboHitsAreAboutPointThreeFiveSecondsApart()
    {
        var set = TestData.Game.AttackSet("lumi_sword");
        var z1 = set.Attacks.First(attack => attack.Id == "z1");
        var z2 = set.Attacks.First(attack => attack.Id == z1.Next);
        var ticks = z1.ChainFromTick + z2.StartupTicks - z1.StartupTicks;
        Assert.Equal(0.35f, ticks / 60f, 2);
    }

    [Theory]
    [InlineData(1f, 1f, 1f)]
    [InlineData(1.2f, 1.2f, 1.07f)]
    [InlineData(3f, 1.5f, 1.175f)]
    public void SpeedHelpsAttacksMoreThanMovementAndHasACeiling(float speed, float attack, float move)
    {
        var stats = StatScaler.ForSpeed(speed, Speed);
        Assert.Equal(attack, stats.Attack, 3);
        Assert.Equal(move, stats.Move, 3);
    }

    [Theory]
    [InlineData(20, 1.5f, 0f, 30)]
    [InlineData(20, 1f, 0.5f, 10)]
    [InlineData(1, 0.1f, 0.9f, 1)]
    public void DamageNeverDropsBelowOne(int attack, float multiplier, float defense, int expected) =>
        Assert.Equal(expected, DamageCalculator.Compute(attack, multiplier, defense));
}
