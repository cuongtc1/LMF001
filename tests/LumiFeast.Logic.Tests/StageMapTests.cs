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

    private static Data.RunDef Run => TestData.Game.Run("honey_labyrinth_stage_1");

    [Fact]
    public void EveryLayoutFillsForManySeeds()
    {
        for (ulong seed = 0; seed < 300; seed++)
        {
            var map = StageMap.Generate(Run, seed);
            Assert.Equal(map.Rooms.Count, map.Rooms.Distinct().Count());
            Assert.Equal(RoomKinds.Entrance, Kind(map.Rooms[0]));
            Assert.Single(map.Rooms, id => Kind(id) == RoomKinds.Boss);
        }
    }

    [Fact]
    public void StackedRoomsHaveMatchingGaps()
    {
        for (ulong seed = 0; seed < 300; seed++)
        {
            var map = StageMap.Generate(Run, seed);
            for (var i = 0; i < map.Rooms.Count; i++)
            {
                var above = map.Neighbor(i, 0, -1);
                if (above >= 0)
                {
                    Assert.NotNull(Room(map.Rooms[i]).Up);
                    Assert.NotNull(Room(map.Rooms[above]).Down);
                }
            }
        }
    }

    [Fact]
    public void SameSeedSameMap() => Assert.Equal(StageMap.Generate(Run, 42).Rooms, StageMap.Generate(Run, 42).Rooms);

    [Fact]
    public void SeedsGiveDifferentMaps() =>
        Assert.True(Enumerable.Range(0, 30).Select(seed => string.Join(' ', StageMap.Generate(Run, (ulong)seed).Rooms)).Distinct().Count() > 5);

    [Fact]
    public void RoomsAboveAreKnownFromBelow()
    {
        var map = new StageMap(["entrance", "climb_a", "sky_nest"], [(0, 0), (1, 0), (1, -1)]);
        map.Enter(1);
        Assert.Equal(2, map.Neighbor(1, 0, -1));
        Assert.True(map.IsKnown(2));
    }

    private static Data.RoomDef Room(string id) => Run.Rooms.First(room => room.Id == id);

    private static string Kind(string id) => Room(id).Kind;
}
