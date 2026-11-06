using LumiFeast.Logic.Data;
using LumiFeast.Logic.Enemies;
using LumiFeast.Logic.Stats;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class MonsterBrainTests
{
    private static MonsterAiDef Ai(string behavior = MonsterBehaviors.Chaser, float hop = 0, bool armor = false, int threshold = 0, float[]? marks = null, float stagger = 0) =>
        new(behavior, 500, 100, 10, 5, 10, 20, 1, 300, hop, 30, 200, armor, threshold, 100, 50, marks ?? [], 40, 30, stagger, 60, 1.5f);

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
        brain.Stun(12);
        Assert.Equal(MonsterState.Stunned, brain.State);
    }

    [Fact]
    public void SuperArmorKeepsTheAttackGoing()
    {
        var brain = new MonsterBrain(Ai(armor: true), Stats);
        brain.Step(80, true);
        brain.Stun(12);
        Assert.Equal(MonsterState.Windup, brain.State);
    }

    [Fact]
    public void ArmorDoesNotProtectBetweenAttacks()
    {
        var brain = new MonsterBrain(Ai(armor: true), Stats);
        brain.Step(300, true);
        brain.Stun(12);
        Assert.Equal(MonsterState.Stunned, brain.State);
    }

    [Fact]
    public void FlyerClimbsBackToItsHoverHeight()
    {
        var brain = new MonsterBrain(Ai(MonsterBehaviors.Flyer), Stats);
        brain.Step(300, true, 100);
        Assert.Equal(-200, brain.MoveVelocityY);
        brain.Step(300, true, 0);
        Assert.Equal(0, brain.MoveVelocityY);
    }

    [Fact]
    public void FlyerDivesDuringTheStrike()
    {
        var brain = new MonsterBrain(Ai(MonsterBehaviors.Flyer), Stats);
        brain.Step(80, true);
        for (var i = 0; i < 11; i++)
        {
            brain.Step(80, true);
        }

        Assert.True(brain.StrikeActive);
        Assert.Equal(300, brain.MoveVelocityY);
    }

    [Fact]
    public void StoicStartsAtTheHitThreshold()
    {
        var brain = new MonsterBrain(Ai(threshold: 3), Stats);
        Assert.False(brain.RegisterHit(0.9f));
        Assert.False(brain.RegisterHit(0.9f));
        Assert.True(brain.RegisterHit(0.9f));
        Assert.True(brain.IsArmored);
    }

    [Fact]
    public void HitsDuringStoicDoNotCount()
    {
        var brain = new MonsterBrain(Ai(threshold: 2), Stats);
        brain.RegisterHit(0.9f);
        brain.RegisterHit(0.9f);
        for (var i = 0; i < 5; i++)
        {
            Assert.False(brain.RegisterHit(0.9f));
        }
    }

    [Fact]
    public void HitCountForgetsAfterAPause()
    {
        var brain = new MonsterBrain(Ai(threshold: 2), Stats);
        brain.RegisterHit(0.9f);
        for (var i = 0; i < 60; i++)
        {
            brain.Step(900, true);
        }

        Assert.False(brain.RegisterHit(0.9f));
    }

    [Fact]
    public void EachHpMarkTriggersOnce()
    {
        var brain = new MonsterBrain(Ai(marks: [0.5f]), Stats);
        Assert.False(brain.RegisterHit(0.8f));
        Assert.True(brain.RegisterHit(0.5f));
        for (var i = 0; i < 101; i++)
        {
            brain.Step(900, true);
        }

        Assert.False(brain.IsArmored);
        Assert.False(brain.RegisterHit(0.4f));
    }

    [Fact]
    public void StoicBreaksOutOfStunAndCannotBeStunned()
    {
        var brain = new MonsterBrain(Ai(threshold: 1), Stats);
        brain.Stun(30);
        brain.RegisterHit(0.9f);
        Assert.Equal(MonsterState.Idle, brain.State);
        brain.Stun(30, knockdown: true);
        Assert.Equal(MonsterState.Idle, brain.State);
    }

    [Fact]
    public void KnockdownCannotBeExtendedAndEndsInArmor()
    {
        var brain = new MonsterBrain(Ai(), Stats);
        brain.Stun(30, knockdown: true);
        Assert.Equal(MonsterState.Down, brain.State);
        for (var i = 0; i < 39; i++)
        {
            brain.Stun(30);
            brain.Step(900, true);
        }

        brain.Step(900, true);
        Assert.Equal(MonsterState.Idle, brain.State);
        Assert.True(brain.IsArmored);
    }

    [Fact]
    public void StunUsesTheAttackTicksAndTheScale()
    {
        var brain = new MonsterBrain(Ai() with { StunScale = 0.5f }, Stats);
        brain.Stun(20);
        for (var i = 0; i < 9; i++)
        {
            brain.Step(900, true);
        }

        Assert.Equal(MonsterState.Stunned, brain.State);
        brain.Step(900, true);
        Assert.Equal(MonsterState.Idle, brain.State);
    }

    [Fact]
    public void FullStaggerBarBreaksArmorAndMakesItDizzy()
    {
        var brain = new MonsterBrain(Ai(threshold: 1, stagger: 10), Stats);
        brain.RegisterHit(0.9f);
        Assert.False(brain.AddStagger(6));
        Assert.True(brain.AddStagger(6));
        Assert.Equal(MonsterState.Dizzy, brain.State);
        Assert.False(brain.IsArmored);
        Assert.Equal(1.5f, brain.DamageTakenScale);
    }

    [Fact]
    public void DizzyEndsAndTheBarStartsEmpty()
    {
        var brain = new MonsterBrain(Ai(stagger: 10), Stats);
        brain.AddStagger(10);
        brain.Stun(100);
        Assert.Equal(MonsterState.Dizzy, brain.State);
        for (var i = 0; i < 61; i++)
        {
            brain.Step(900, true);
        }

        Assert.Equal(MonsterState.Idle, brain.State);
        Assert.Equal(0, brain.StaggerRatio);
    }

    [Fact]
    public void MonstersWithoutABarNeverGetDizzy()
    {
        Assert.False(new MonsterBrain(Ai(), Stats).AddStagger(1000));
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
