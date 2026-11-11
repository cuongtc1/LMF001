using LumiFeast.Logic.Combat;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class PassRuleTests
{
    private static float Pass(params (float X, float HalfWidth)[] targets) =>
        PassRule.Distance(0, 1, 30, targets, reach: 200, margin: 20, fallback: 300, max: 600);

    [Fact]
    public void LandsJustBehindTheMonster()
    {
        Assert.Equal(100 + 40 + 30 + 20, Pass((100, 40)));
    }

    [Fact]
    public void BigMonstersMeanLongerPasses()
    {
        Assert.True(Pass((150, 120)) > Pass((150, 40)));
    }

    [Fact]
    public void TheNearestMonsterInFrontIsTheTarget()
    {
        Assert.Equal(100 + 40 + 30 + 20, Pass((400, 40), (100, 40), (-80, 40)));
    }

    [Fact]
    public void NobodyInReachMeansTheFixedDistance()
    {
        Assert.Equal(300, Pass((-100, 40), (500, 40)));
    }

    [Fact]
    public void ThePassIsCapped()
    {
        Assert.Equal(600, Pass((220, 400)));
    }

    [Fact]
    public void FacingLeftWorksTheSameWay()
    {
        Assert.Equal(190, PassRule.Distance(0, -1, 30, [(-100, 40)], 200, 20, 300, 600));
    }
}
