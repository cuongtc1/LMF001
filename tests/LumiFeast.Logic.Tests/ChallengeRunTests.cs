using LumiFeast.Logic.Data;
using LumiFeast.Logic.Run;
using LumiFeast.Logic.Stages;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class ChallengeRunTests
{
    private static ChallengeRun Lair() => new(new ChallengeDef(ChallengeKinds.Lair, 500, 1, 960));

    private static ChallengeRun NoHit() => new(new ChallengeDef(ChallengeKinds.NoHit, 0, 0, 960));

    [Fact]
    public void ALairWaitsForTheLever()
    {
        var lair = Lair();
        Assert.Equal(ChallengeState.Waiting, lair.Step(true));
        Assert.False(lair.LocksDoor);
    }

    [Fact]
    public void AnAcceptedLairLocksTheDoorUntilHealed()
    {
        var lair = Lair();
        Assert.True(lair.Start());
        Assert.False(lair.Start());
        Assert.True(lair.LocksDoor);
        lair.PlayerHit();
        Assert.Equal(ChallengeState.Running, lair.Step(false));
        Assert.Equal(ChallengeState.Won, lair.Step(true));
        Assert.False(lair.LocksDoor);
    }

    [Fact]
    public void ANoHitTrialRunsFromTheStart()
    {
        var trial = NoHit();
        Assert.Equal(ChallengeState.Running, trial.State);
        Assert.False(trial.LocksDoor);
        Assert.Equal(ChallengeState.Won, trial.Step(true));
    }

    [Fact]
    public void AHitLosesTheNoHitBonus()
    {
        var trial = NoHit();
        trial.PlayerHit();
        Assert.Equal(ChallengeState.Failed, trial.Step(true));
    }

    [Fact]
    public void ARunHasAtMostOneChallengeRoom()
    {
        var run = TestData.Game.Run("honey_labyrinth_stage_1");
        var challenges = run.Rooms.Where(room => room.Challenge is not null).Select(room => room.Id).ToHashSet();
        Assert.Equal(2, challenges.Count);
        for (ulong seed = 0; seed < 200; seed++)
        {
            Assert.True(RunPlan.Build(run, seed).Count(challenges.Contains) <= 1);
        }
    }
}
