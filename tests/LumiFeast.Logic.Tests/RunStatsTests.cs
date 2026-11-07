using LumiFeast.Logic.Run;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class RunStatsTests
{
    [Fact]
    public void BestComboKeepsTheHighest()
    {
        var stats = new RunStats();
        stats.Combo(12);
        stats.Combo(5);
        Assert.Equal(12, stats.BestCombo);
    }

    [Fact]
    public void TimeReadsMinutesAndSeconds()
    {
        var stats = new RunStats();
        for (var i = 0; i < 60 * 125; i++)
        {
            stats.Step();
        }

        Assert.Equal("2:05", stats.Time(60));
    }

    [Fact]
    public void ResetStartsOver()
    {
        var stats = new RunStats();
        stats.RoomCleared();
        stats.Healed();
        stats.Step();
        stats.Reset();
        Assert.Equal((0, 0, 0, 0), (stats.RoomsCleared, stats.MonstersHealed, stats.Ticks, stats.BestCombo));
    }
}
