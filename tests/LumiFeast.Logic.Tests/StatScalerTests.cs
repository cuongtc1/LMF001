using LumiFeast.Logic.Combat;
using LumiFeast.Logic.Data;
using LumiFeast.Logic.Stats;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class StatScalerTests
{
    private static readonly SpeedRuleDef Speed = new(1, 0.35f, 1.5f);
    private static readonly ScalingDef Scaling = new(2f, 1.5f, 1.1f, 1.2f, 4, 1, 99, Speed);
    private static readonly DifficultyDef Normal = new("normal", 1, 1, 1, 1);
    private static readonly DifficultyDef Hard = new("hard", 1.5f, 2, 1.2f, 0.5f);

    private static MonsterDef Monster => TestData.Game.Monster("honey_slime") with
    {
        Stats = new MonsterStatsDef(100, 10, 0.1f, 200),
    };

    [Fact]
    public void LevelOneNormalKeepsBaseNumbers()
    {
        var stats = StatScaler.ForMonster(Monster, 1, Normal, Scaling);
        Assert.Equal((100, 10, 200f, 1f), (stats.MaxHp, stats.Attack, stats.Speed, stats.CooldownScale));
    }

    [Fact]
    public void LevelAndDifficultyMultiply()
    {
        var stats = StatScaler.ForMonster(Monster, 3, Hard, Scaling);
        Assert.Equal(600, stats.MaxHp);    // 100 x 2^2 x 1.5
        Assert.Equal(45, stats.Attack);    // 10 x 1.5^2 x 2
        Assert.Equal(240f, stats.Speed, 3);
        Assert.Equal(0.5f, stats.CooldownScale);
    }

    [Fact]
    public void LumiCountsAtMostFourLevelsAboveTheStage()
    {
        Assert.Equal(5, StatScaler.EffectiveLevel(20, 1, Scaling));
        Assert.Equal(3, StatScaler.EffectiveLevel(3, 5, Scaling));
        Assert.Equal(7, StatScaler.EffectiveLevel(7, 3, Scaling));
    }

    [Fact]
    public void LumiGrowsPerLevel()
    {
        var stats = new CharacterStatsDef(300, 100, 20, 0, 1);
        Assert.Equal((300, 20), StatScaler.ForPlayer(stats, 1, Scaling));
        Assert.Equal((363, 29), StatScaler.ForPlayer(stats, 3, Scaling)); // 300 x 1.1^2, 20 x 1.2^2
    }

    [Fact]
    public void EqualLevelsKeepTheSameBalanceWithRealData()
    {
        var scaling = TestData.Game.Scaling;
        var lumi = TestData.Game.Character("lumi").Stats;
        var slime = TestData.Game.Monster("honey_slime");
        var normal = TestData.Game.Difficulty("normal");
        foreach (var level in new[] { 1, 10, 30, 60 })
        {
            var (hp, attack) = StatScaler.ForPlayer(lumi, level, scaling);
            var monster = StatScaler.ForMonster(slime, level, normal, scaling);
            // hits to heal a slime and hits Lumi can take stay within 10% of level 1
            Assert.InRange(monster.MaxHp / (float)attack, 0.9f * slime.Stats.MaxHp / lumi.Attack, 1.1f * slime.Stats.MaxHp / lumi.Attack);
            Assert.InRange(hp / (float)monster.Attack, 0.9f * lumi.MaxHp / slime.Stats.Attack, 1.1f * lumi.MaxHp / slime.Stats.Attack);
        }
    }

    // early-game rhythm, as in Elsword: Z about every 0.35 s, the opener a little slower and the follow-up quicker;
    // gear and buffs may bring it to 0.3 later
    [Fact]
    public void ComboHitsAverageAboutPointThreeFiveSecondsAndSpeedUp()
    {
        var set = TestData.Game.AttackSet("lumi_sword");
        var z1 = set.Attacks.First(attack => attack.Id == "z1");
        var z2 = set.Attacks.First(attack => attack.Id == z1.Next);
        var z3 = set.Attacks.First(attack => attack.Id == z2.Next);
        var first = z1.ChainFromTick + z2.StartupTicks - z1.StartupTicks;
        var second = z2.ChainFromTick + z3.StartupTicks - z2.StartupTicks;
        Assert.Equal(0.35f, (first + second) / 2f / 60f, 1);
        Assert.True(second < first);
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
