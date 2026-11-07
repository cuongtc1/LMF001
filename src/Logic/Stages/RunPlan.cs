using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Stages;

/// <summary>Picks the rooms of one run. The same seed gives the same run. At most one challenge room per run.</summary>
public static class RunPlan
{
    public static IReadOnlyList<string> Build(RunDef run, ulong seed)
    {
        var pool = run.Rooms.Select(room => room.Id).Where(id => id != run.FirstRoom && id != run.BossRoom).ToList();
        var random = new SeededRandom(seed);
        var plan = new List<string> { run.FirstRoom };
        while (plan.Count < run.RoomsPerRun && pool.Count > 0)
        {
            var pick = random.NextInt(0, pool.Count);
            plan.Add(pool[pick]);
            pool.RemoveAt(pick);
            if (run.Rooms.First(room => room.Id == plan[^1]).Challenge is not null)
            {
                pool.RemoveAll(id => run.Rooms.First(room => room.Id == id).Challenge is not null);
            }
        }

        plan.Add(run.BossRoom);
        return plan;
    }
}
