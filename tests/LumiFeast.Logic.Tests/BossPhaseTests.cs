using LumiFeast.Logic.Data;
using LumiFeast.Logic.Enemies;
using LumiFeast.Logic.Stats;
using Xunit;

namespace LumiFeast.Logic.Tests;

/// <summary>Round 84: the Raccoon changes style each third of his HP; big moves warn first.</summary>
public class BossPhaseTests
{
    private static MonsterMoveDef Move(string id, float fromHp) =>
        new(id, MonsterMoveKinds.Strike, 0, 2000, 1, 10, 5, 10, 0, 1, new RectDef(0, -100, 100, 100), false, 0, null, 0, 0, 0, 0, FromHp: fromHp);

    private static MonsterBrain Boss() => new(
        new MonsterAiDef(MonsterBehaviors.Chaser, 2400, 200, 10, 5, 10, 0, 1, 300, 0, 30, 0, true, 0, 100, 50, [], 40, 30, 0, 60, 1.5f, 1, false,
            Moves: [Move("early", 1), Move("late", 0.34f)], Boss: true),
        new MonsterStats(100, 10, 0, 200, 1));

    [Fact]
    public void LateMovesWaitForLowHp()
    {
        var brain = Boss();
        for (var i = 0; i < 40; i++)
        {
            brain.Step(150, true);
            Assert.NotEqual("late", brain.CurrentMove?.Id);
        }
    }

    [Fact]
    public void UnderAThirdOfHpTheLateMoveComes()
    {
        var brain = Boss();
        brain.RegisterHit(0.3f);
        var seen = false;
        for (var i = 0; i < 400 && !seen; i++)
        {
            brain.Step(150, true);
            seen = brain.CurrentMove?.Id == "late";
        }

        Assert.True(seen);
    }

    [Fact]
    public void TheRaccoonHasThreePhasesAndOneDangerMove()
    {
        var moves = TestData.Game.Monster("raccoon_mask").Ai.Moves!;
        Assert.Contains(moves, move => move.FromHp >= 1);
        Assert.Contains(moves, move => move.FromHp is < 1 and > 0.5f);
        var heist = Assert.Single(moves, move => move.Danger);
        Assert.True(heist.FromHp < 0.5f && heist.Bounces);
    }
}
