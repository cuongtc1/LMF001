using LumiFeast.Logic.Enemies;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class PlatformChaseTests
{
    private const float Gravity = 3000;

    private static PlatformChase New() => new(TestData.Game.Feel.MonsterClimb);

    private static int Wait => TestData.Game.Feel.MonsterClimb.WaitTicks;

    private static ClimbAction Run(PlatformChase chase, int ticks, float dx, float dy, bool onLedge = false, bool lumiStanding = true)
    {
        var last = ClimbAction.None;
        for (var i = 0; i < ticks && last == ClimbAction.None; i++)
        {
            last = chase.Step(dx, dy, true, onLedge, lumiStanding, Gravity);
        }

        return last;
    }

    [Fact]
    public void AQuickHopOntoALedgeStillDodges() => Assert.Equal(ClimbAction.None, Run(New(), Wait - 2, 50, -250));

    [Fact]
    public void StayingOnALedgeBringsTheMonsterUp()
    {
        var chase = New();
        Assert.Equal(ClimbAction.Jump, Run(chase, Wait + 1, 50, -250));
        var rise = chase.JumpVelocity * chase.JumpVelocity / (2 * Gravity);
        Assert.True(rise > 250);
    }

    [Fact]
    public void ItOnlyJumpsWhenCloseBelow() => Assert.Equal(ClimbAction.None, Run(New(), Wait * 3, 900, -250));

    [Fact]
    public void ItDropsWhenLumiIsBelow() => Assert.Equal(ClimbAction.Drop, Run(New(), Wait + 1, 50, 250, onLedge: true));

    [Fact]
    public void LumiInTheAirDoesNotCount() => Assert.Equal(ClimbAction.None, Run(New(), Wait * 3, 50, -250, lumiStanding: false));

    [Fact]
    public void ReachIsAboutBodyHeight()
    {
        var chase = New();
        Assert.True(chase.CanReach(-60));
        Assert.False(chase.CanReach(-250));
    }
}
