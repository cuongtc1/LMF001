using LumiFeast.Logic.Data;
using LumiFeast.Logic.Enemies;
using LumiFeast.Logic.Stats;
using Xunit;

namespace LumiFeast.Logic.Tests;

/// <summary>Round 81: Chapter 1 monsters get their own personality (17_direction_review section 7).</summary>
public class MonsterPersonalityTests
{
    private static readonly MonsterAiDef Base =
        new(MonsterBehaviors.Chaser, 500, 100, 10, 5, 10, 20, 1, 300, 0, 30, 200, false, 0, 100, 50, [], 40, 30, 0, 60, 1.5f, 1, false);

    private static readonly MonsterStats Stats = new(100, 10, 0, 200, 1);

    private static readonly MonsterMoveDef Burst = new("burst", MonsterMoveKinds.Strike, 0, 200, 1, 8, 4, 10, 0, 1, new RectDef(-100, -100, 200, 100), false, 0, null, 0, 0, 0, 0);

    [Fact]
    public void AHeavyMonsterTakesTimeToTurnAround()
    {
        var brain = new MonsterBrain(Base with { TurnTicks = 20 }, Stats);
        for (var i = 0; i < 25; i++)
        {
            brain.Step(300, true);
        }

        Assert.Equal(1, brain.Facing);
        for (var i = 0; i < 10; i++)
        {
            brain.Step(-80, true); // Lumi passed behind it
        }

        Assert.Equal(1, brain.Facing);
        Assert.NotEqual(MonsterState.Windup, brain.State);
        for (var i = 0; i < 15; i++)
        {
            brain.Step(-80, true);
        }

        Assert.Equal(-1, brain.Facing);
    }

    [Fact]
    public void AChargeIntoAWallLeavesItDizzy()
    {
        var brain = new MonsterBrain(Base with { WallStunTicks = 60 }, Stats);
        brain.Step(80, true);
        for (var i = 0; i < 11; i++)
        {
            brain.Step(80, true);
        }

        Assert.True(brain.StrikeActive);
        brain.BounceOffWall();
        Assert.Equal(MonsterState.Dizzy, brain.State);
    }

    [Fact]
    public void ArmoringUpStartsTheCounterMove()
    {
        var brain = new MonsterBrain(Base with { StoicThreshold = 2, CounterMove = Burst }, Stats);
        brain.RegisterHit(1);
        Assert.True(brain.RegisterHit(1));
        brain.Step(150, true); // outside its normal reach, inside the burst
        Assert.Equal(MonsterState.Windup, brain.State);
        Assert.Equal("burst", brain.CurrentMove?.Id);
    }
}
