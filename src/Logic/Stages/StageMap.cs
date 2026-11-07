namespace LumiFeast.Logic.Stages;

/// <summary>What a room looks like when Lumi comes back to it.</summary>
public sealed class RoomState
{
    public bool Visited { get; set; }

    /// <summary>Healed, or never had monsters. Its edges are open.</summary>
    public bool Cleared { get; set; }

    public bool RewardTaken { get; set; }

    public HashSet<int> BrokenCrates { get; } = [];

    /// <summary>Chests by name ("room", "lair", "bonus"); true once opened.</summary>
    public Dictionary<string, bool> Chests { get; } = [];
}

/// <summary>The rooms of one run, laid out west to east, with what happened in each.</summary>
public sealed class StageMap
{
    private readonly RoomState[] _states;

    public StageMap(IReadOnlyList<string> rooms)
    {
        Rooms = rooms;
        _states = rooms.Select(_ => new RoomState()).ToArray();
    }

    public IReadOnlyList<string> Rooms { get; }

    public int Current { get; private set; }

    public RoomState State(int index) => _states[index];

    public RoomState CurrentState => _states[Current];

    public string CurrentRoom => Rooms[Current];

    /// <summary>Grid cell of a room; one row for now, so vertical rooms can come later without changing callers.</summary>
    public (int X, int Y) Cell(int index) => (index, 0);

    /// <returns>The room on that side, or -1 at the end of the row.</returns>
    public int Neighbor(int index, int side)
    {
        var next = index + Math.Sign(side);
        return next >= 0 && next < Rooms.Count ? next : -1;
    }

    public void Enter(int index)
    {
        Current = index;
        _states[index].Visited = true;
    }

    /// <summary>Shown on the minimap: visited, or next to a visited room.</summary>
    public bool IsKnown(int index) =>
        _states[index].Visited || (Neighbor(index, -1) is var west && west >= 0 && _states[west].Visited) || (Neighbor(index, 1) is var east && east >= 0 && _states[east].Visited);
}
