using LumiFeast.Logic.Data;
using LumiFeast.Logic.Run;
using LumiFeast.Logic.Stages;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class ChallengeRunTests
{
    private static ChallengeRun Clock(int seconds) => new(new ChallengeDef(ChallengeKinds.TimeLimit, 0, seconds, 3), 60);

    private static ChallengeRun NoHit() => new(new ChallengeDef(ChallengeKinds.NoHit, 0, 0, 3), 60);

    [Fact]
    public void NothingHappensBeforeTheLever()
    {
        var trial = Clock(1);
        for (var i = 0; i < 200; i++)
        {
            trial.Step(false);
        }

        Assert.Equal(ChallengeState.Waiting, trial.State);
    }

    [Fact]
    public void HealingInTimeWins()
    {
        var trial = Clock(2);
        trial.Start();
        trial.Step(false);
        Assert.Equal(ChallengeState.Won, trial.Step(true));
    }

    [Fact]
    public void TheClockRunsOut()
    {
        var trial = Clock(1);
        trial.Start();
        for (var i = 0; i < 59; i++)
        {
            Assert.Equal(ChallengeState.Running, trial.Step(false));
        }

        Assert.Equal(ChallengeState.Failed, trial.Step(false));
        Assert.Equal(ChallengeState.Failed, trial.Step(true));
    }

    [Fact]
    public void SecondsRoundUp()
    {
        var trial = Clock(2);
        trial.Start();
        trial.Step(false);
        Assert.Equal(2, trial.SecondsLeft);
    }

    [Fact]
    public void AHitFailsOnlyTheNoHitTrial()
    {
        var clock = Clock(5);
        clock.Start();
        clock.PlayerHit();
        Assert.Equal(ChallengeState.Running, clock.State);

        var untouched = NoHit();
        untouched.PlayerHit();
        Assert.Equal(ChallengeState.Waiting, untouched.State);
        untouched.Start();
        untouched.PlayerHit();
        Assert.Equal(ChallengeState.Failed, untouched.State);
    }

    [Fact]
    public void LeverStartsOnlyOnce()
    {
        var trial = Clock(5);
        Assert.True(trial.Start());
        Assert.False(trial.Start());
    }

    [Fact]
    public void ARunHasAtMostOneChallengeRoom()
    {
        var run = TestData.Game.Run("honey_labyrinth_stage_1");
        var challenges = run.Rooms.Where(room => room.Challenge is not null).Select(room => room.Id).ToHashSet();
        for (ulong seed = 0; seed < 200; seed++)
        {
            Assert.True(RunPlan.Build(run, seed).Count(challenges.Contains) <= 1);
        }
    }
}
