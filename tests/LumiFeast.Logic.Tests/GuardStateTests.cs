using LumiFeast.Logic.Combat;
using LumiFeast.Logic.Data;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class GuardStateTests
{
    private static readonly GuardDef Def = new(0.25f, 3, 5, 100, 4);

    private static GuardState Held(int ticks)
    {
        var guard = new GuardState(Def);
        for (var i = 0; i < ticks; i++)
        {
            guard.Step(true);
        }

        return guard;
    }

    [Fact]
    public void NoGuardNoBlock() => Assert.Equal(GuardResult.None, Held(0).Resolve(-1, 1));

    [Fact]
    public void EarlyHitIsPerfectAndOpensTheCounter()
    {
        var guard = Held(2);
        Assert.Equal(GuardResult.Perfect, guard.Resolve(-1, 1));
        Assert.True(guard.CounterReady);
    }

    [Fact]
    public void LateHitIsOnlyBlocked()
    {
        var guard = Held(10);
        Assert.Equal(GuardResult.Blocked, guard.Resolve(-1, 1));
        Assert.False(guard.CounterReady);
    }

    [Fact]
    public void HitsFromBehindGetThrough() => Assert.Equal(GuardResult.None, Held(10).Resolve(1, 1));

    [Fact]
    public void CounterWindowEnds()
    {
        var guard = Held(1);
        guard.Resolve(-1, 1);
        for (var i = 0; i < 5; i++)
        {
            guard.Step(false);
        }

        Assert.False(guard.CounterReady);
    }
}
