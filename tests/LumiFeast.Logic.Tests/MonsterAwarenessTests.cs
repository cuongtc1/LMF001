using LumiFeast.Logic.Data;
using LumiFeast.Logic.Enemies;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class MonsterAwarenessTests
{
    private static readonly MonsterAwarenessDef Def = new(900, 300, 150, 10, 600, 1500, 30, 100, 0.5f, 5);

    [Fact]
    public void SeesAheadButNotFarBehindOrFarAbove()
    {
        var eyes = new MonsterAwareness(Def, -1);
        Assert.True(eyes.Sees(-800, 0));
        Assert.False(eyes.Sees(400, 0));
        Assert.True(eyes.Sees(100, 0));
        Assert.False(eyes.Sees(-300, -500));
    }

    [Fact]
    public void NoticesWithAPauseBeforeFighting()
    {
        var eyes = new MonsterAwareness(Def, -1);
        eyes.Step(-500, 0, 0);
        Assert.Equal(AwarenessState.Noticing, eyes.State);
        Assert.True(eyes.JustNoticed);
        for (var i = 0; i < 10; i++)
        {
            eyes.Step(-500, 0, 0);
        }

        Assert.True(eyes.IsFighting);
    }

    [Fact]
    public void LumiHighAboveIsNotSeen()
    {
        var eyes = new MonsterAwareness(Def, -1);
        eyes.Step(-200, -600, 0);
        Assert.Equal(AwarenessState.Unaware, eyes.State);
    }

    [Fact]
    public void AHitWakesItAndTurnsItAround()
    {
        var eyes = new MonsterAwareness(Def, -1);
        eyes.Alert(300);
        Assert.Equal(AwarenessState.Noticing, eyes.State);
        Assert.Equal(1, eyes.Facing);
    }

    [Fact]
    public void PatrolsThenTurnsAtTheEndOfItsBeat()
    {
        var eyes = new MonsterAwareness(Def, 1);
        eyes.Step(-2000, 0, 0);
        Assert.Equal(0.5f, eyes.PatrolMove);
        eyes.Step(-2000, 0, 120);
        Assert.Equal(-1, eyes.Facing);
        Assert.Equal(0f, eyes.PatrolMove);
    }

    [Fact]
    public void GivesUpWhenLumiStaysFarAway()
    {
        var eyes = new MonsterAwareness(Def, -1);
        eyes.Alert(-100);
        for (var i = 0; i < 10; i++)
        {
            eyes.Step(-100, 0, 0);
        }

        for (var i = 0; i < 30; i++)
        {
            eyes.Step(-2000, 0, 0);
        }

        Assert.Equal(AwarenessState.Unaware, eyes.State);
    }
}
