using LumiFeast.Logic.Common;
using LumiFeast.Logic.Stages;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class AreaTests
{
    private static readonly Data.RunDef Run = TestData.Game.Run("honey_labyrinth_stage_1");

    [Fact]
    public void TheFirstAreaEndsInAGateWithTwoDoors()
    {
        var first = Run.Areas![0];
        Assert.Equal(2, first.Next.Count);
        Assert.All(first.Layouts, layout => Assert.Single(layout.Cells, cell => cell.Kind == RoomKinds.Gate));
        Assert.All(first.Next.Select(id => Run.Areas.First(area => area.Id == id)),
            area => Assert.All(area.Layouts, layout => Assert.Single(layout.Cells, cell => cell.Kind == RoomKinds.Boss)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void EveryRouteFillsWithoutRepeatingARoom(int door)
    {
        for (ulong seed = 0; seed < 40; seed++)
        {
            foreach (var withShop in new[] { false, true })
            {
                var random = new SeededRandom(seed);
                var used = new HashSet<string>();
                var first = StageMap.Generate(Run, Run.Areas![0].Layouts, random, withShop, used);
                var next = Run.Areas.First(area => area.Id == Run.Areas[0].Next[door]);
                var second = StageMap.Generate(Run, next.Layouts, random, withShop, used);
                Assert.Empty(first.Rooms.Intersect(second.Rooms));
                Assert.Equal(first.Rooms.Count + second.Rooms.Count, used.Count);
            }
        }
    }

    [Fact]
    public void AKeyWaitsOnTheWayUpToEveryLockedSkyChest()
    {
        // the key for a sky room's locked chest lies in the climb room below it, so Lumi never walks back for it
        foreach (var climb in Run.Rooms.Where(room => room.Up is not null))
        {
            Assert.NotEmpty(climb.Keys ?? []);
        }
    }
}
