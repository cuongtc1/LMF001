using LumiFeast.Logic.Stages;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class RunPlanTests
{
    private static Data.RunDef Run => TestData.Game.Run("honey_labyrinth_stage_1");

    [Fact]
    public void StartsAtTheGateAndEndsAtTheBoss()
    {
        var plan = RunPlan.Build(Run, 7);
        Assert.Equal(Run.FirstRoom, plan[0]);
        Assert.Equal(Run.BossRoom, plan[^1]);
        Assert.Equal(Run.RoomsPerRun + 1, plan.Count);
    }

    [Fact]
    public void NoRoomRepeats() => Assert.Equal(RunPlan.Build(Run, 3).Count, RunPlan.Build(Run, 3).Distinct().Count());

    [Fact]
    public void SameSeedSameRun() => Assert.Equal(RunPlan.Build(Run, 42), RunPlan.Build(Run, 42));

    [Fact]
    public void DifferentSeedsGiveDifferentRuns()
    {
        var plans = Enumerable.Range(0, 20).Select(seed => string.Join(' ', RunPlan.Build(Run, (ulong)seed))).ToHashSet();
        Assert.True(plans.Count > 1);
    }
}
