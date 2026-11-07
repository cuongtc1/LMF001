using LumiFeast.Logic.Stages;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class StageMapTests
{
    private static StageMap Map() => new(["entrance", "gate", "lair", "den"]);

    [Fact]
    public void NeighborsStopAtTheEnds()
    {
        var map = Map();
        Assert.Equal(-1, map.Neighbor(0, -1));
        Assert.Equal(1, map.Neighbor(0, 1));
        Assert.Equal(-1, map.Neighbor(3, 1));
    }

    [Fact]
    public void OnlyRoomsNextToVisitedOnesAreKnown()
    {
        var map = Map();
        map.Enter(0);
        Assert.True(map.IsKnown(1));
        Assert.False(map.IsKnown(2));
        map.Enter(1);
        Assert.True(map.IsKnown(2));
        Assert.Equal("gate", map.CurrentRoom);
    }

    [Fact]
    public void RoomsRememberWhatHappened()
    {
        var map = Map();
        map.Enter(1);
        map.CurrentState.Cleared = true;
        map.CurrentState.BrokenCrates.Add(0);
        map.Enter(0);
        map.Enter(1);
        Assert.True(map.CurrentState.Cleared);
        Assert.Contains(0, map.CurrentState.BrokenCrates);
    }
}
