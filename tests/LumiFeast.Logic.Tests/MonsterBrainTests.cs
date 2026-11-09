using LumiFeast.Logic.Data;
using LumiFeast.Logic.Enemies;
using LumiFeast.Logic.Stats;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class MonsterBrainTests
{
    private static MonsterAiDef Ai(string behavior = MonsterBehaviors.Chaser, float hop = 0, bool armor = false, int threshold = 0, float[]? marks = null, float stagger = 0, float guard = 1, bool bounce = false) =>
        new(behavior, 500, 100, 10, 5, 10, 20, 1, 300, hop, 30, 200, armor, threshold, 100, 50, marks ?? [], 40, 30, stagger, 60, 1.5f, guard, bounce);

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
    public void FrontGuardHoldsLightHitsFromTheFrontOnly()
    {
        var brain = new MonsterBrain(Ai(guard: 0.2f), Stats);
        Assert.True(brain.GuardsAgainst(1, false));
        Assert.False(brain.GuardsAgainst(-1, false));
        Assert.False(brain.GuardsAgainst(1, true));
    }

    [Fact]
    public void NoGuardOnceReeling()
    {
        var brain = new MonsterBrain(Ai(guard: 0.2f), Stats);
        brain.Stun(30);
        Assert.False(brain.GuardsAgainst(1, false));
    }

    [Fact]
    public void RollerTurnsAtWalls()
    {
        var brain = new MonsterBrain(Ai(bounce: true), Stats);
        brain.Step(80, true);
        for (var i = 0; i < 11; i++)
        {
            brain.Step(80, true);
        }

        var facing = brain.Facing;
        brain.BounceOffWall();
        Assert.Equal(-facing, brain.Facing);
    }

    private static MonsterMoveDef Move(string id, float low, float high) =>
        new(id, MonsterMoveKinds.Strike, low, high, 1, 4, 2, 2, 100, 1, new RectDef(0, 0, 1, 1), false, 0, null, 0, 0, 0, 0);

    private static MonsterBrain Boss(params MonsterMoveDef[] moves) =>
        new(Ai() with { Moves = moves, CooldownTicks = 0, PhaseTwoHp = 0.5f, PhaseTwoSpeed = 2 }, Stats);

    private static void PlayMove(MonsterBrain brain, float deltaX)
    {
        for (var i = 0; i < 12; i++)
        {
            brain.Step(deltaX, true);
        }
    }

    [Fact]
    public void BossPicksAMoveThatFitsTheDistance()
    {
        var brain = Boss(Move("near", 0, 100), Move("far", 100, 450));
        brain.Step(300, true);
        Assert.Equal("far", brain.CurrentMove?.Id);
    }

    [Fact]
    public void BossNeverRepeatsAMoveThreeTimes()
    {
        var brain = Boss(Move("a", 0, 450), Move("b", 0, 450));
        var seen = new List<string>();
        for (var i = 0; i < 400; i++)
        {
            brain.Step(80, true);
            if (brain.CurrentMove is { } move && (seen.Count == 0 || brain.State == MonsterState.Windup && brain.StateTicksLeft == 4 && seen[^1] != "*"))
            {
                seen.Add(move.Id);
                seen.Add("*");
            }

            if (brain.CurrentMove is null && seen.Count > 0 && seen[^1] == "*")
            {
                seen.RemoveAt(seen.Count - 1);
            }
        }

        var ids = seen.Where(id => id != "*").ToList();
        Assert.True(ids.Count > 3);
        for (var i = 2; i < ids.Count; i++)
        {
            Assert.False(ids[i] == ids[i - 1] && ids[i] == ids[i - 2]);
        }
    }

    [Fact]
    public void BossCanOnlyWalkWhenNoMoveFits()
    {
        var brain = Boss(Move("near", 0, 100));
        brain.Step(400, true);
        Assert.Null(brain.CurrentMove);
        Assert.Equal(200, brain.MoveVelocityX);
    }

    [Fact]
    public void PhaseTwoStartsOnceAtItsHpMarkAndShortensMoves()
    {
        var brain = Boss(Move("a", 0, 450));
        brain.RegisterHit(0.9f);
        Assert.False(brain.InPhaseTwo);
        Assert.True(brain.RegisterHit(0.5f));
        Assert.True(brain.InPhaseTwo);
        Assert.True(brain.IsArmored);
        brain.Step(80, true);
        Assert.Equal(2, brain.StateTicksLeft);
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

    [Fact]
    public void WaitsForATurnJustOutsideItsRange()
    {
        var brain = new MonsterBrain(Ai(), Stats) { Crowd = new MonsterCrowdDef(2, 160, 0.5f) };
        brain.Step(80, true, mayAttack: false);
        Assert.NotEqual(MonsterState.Windup, brain.State);
        Assert.Equal(-100, brain.MoveVelocityX); // steps back, facing Lumi on its right
        brain.Step(220, true, mayAttack: false);
        Assert.Equal(0, brain.MoveVelocityX);
        brain.Step(80, true);
        Assert.Equal(MonsterState.Windup, brain.State);
    }

    [Fact]
    public void AttackTurnsLetOnlyAFewAttackAtOnce()
    {
        var turns = new AttackTurns(2);
        object a = new(), b = new(), c = new();
        turns.Take(a);
        turns.Take(b);
        Assert.False(turns.MayAttack(c));
        Assert.True(turns.MayAttack(a));
        turns.Release(a);
        Assert.True(turns.MayAttack(c));
    }
}
