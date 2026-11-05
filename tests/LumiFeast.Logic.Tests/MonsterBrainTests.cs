using LumiFeast.Logic.Data;
using LumiFeast.Logic.Enemies;
using LumiFeast.Logic.Stats;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class MonsterBrainTests
{
    private static MonsterAiDef Ai(string behavior = MonsterBehaviors.Chaser, float hop = 0) =>
        new(behavior, 500, 100, 10, 5, 10, 20, 12, 300, hop, 30);

    private static readonly MonsterStats Stats = new(100, 10, 0, 200, 1);

    [Fact]
    public void IgnoresTargetOutOfRange()
    {
        var brain = new MonsterBrain(Ai(), Stats);
        brain.Step(900, true);
        Assert.Equal(MonsterState.Idle, brain.State);
        Assert.Equal(0, brain.MoveVelocityX);
    }

    [Fact]
    public void WalksToTargetThenWindsUpAndStrikes()
    {
        var brain = new MonsterBrain(Ai(), Stats);
        brain.Step(300, true);
        Assert.Equal(200, brain.MoveVelocityX);
        brain.Step(80, true);
        Assert.Equal(MonsterState.Windup, brain.State);
        for (var i = 0; i < 10; i++)
        {
            brain.Step(80, true);
        }

        Assert.True(brain.StrikeActive);
        Assert.Equal(1, brain.SwingId);
    }

    [Fact]
    public void CooldownScaleShortensTheWindup()
    {
        var fast = new MonsterBrain(Ai(), Stats with { CooldownScale = 0.5f });
        fast.Step(80, true);
        for (var i = 0; i < 5; i++)
        {
            fast.Step(80, true);
        }

        Assert.True(fast.StrikeActive);
    }

    [Fact]
    public void StunInterruptsAttack()
    {
        var brain = new MonsterBrain(Ai(), Stats);
        brain.Step(80, true);
        brain.Stun();
        Assert.Equal(MonsterState.Stunned, brain.State);
    }

    [Fact]
    public void HopperOnlyHopsOnInterval()
    {
        var brain = new MonsterBrain(Ai(hop: 500), Stats);
        brain.Step(300, true);
        Assert.True(brain.WantsHop);
        brain.Step(300, true);
        Assert.False(brain.WantsHop);
    }

    [Fact]
    public void PassiveMonsterNeverMoves()
    {
        var brain = new MonsterBrain(Ai(MonsterBehaviors.Passive), Stats);
        brain.Step(50, true);
        Assert.Equal((MonsterState.Idle, 0f), (brain.State, brain.MoveVelocityX));
    }
}
