using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Stages;

public static class RoomKinds
{
    public const string Entrance = "entrance";
    public const string Fight = "fight";
    public const string Treasure = "treasure";
    public const string Trial = "trial";
    public const string Boss = "boss";

    /// <summary>A mini boss with a few helpers: a calmer, single big fight between the crowded rooms.</summary>
    public const string Elite = "elite";

    /// <summary>The Raccoon's stall. Only once the Raccoon has been healed; before that the cell holds a treasure room.</summary>
    public const string Shop = "shop";

    /// <summary>Ledges over open sky, no monsters; reached by climbing out of the room below.</summary>
    public const string Sky = "sky";

    public static readonly string[] All = [Entrance, Fight, Treasure, Trial, Boss, Sky, Elite, Shop];
}

/// <summary>What a room looks like when Lumi comes back to it.</summary>
public sealed class RoomState
{
    public bool Visited { get; set; }

    /// <summary>Healed, or never had monsters. Its edges are open.</summary>
    public bool Cleared { get; set; }

    public bool RewardTaken { get; set; }

    public HashSet<int> BrokenCrates { get; } = [];

    /// <summary>Keys already taken, by their index in the room data; <see cref="ClearKey"/> is the key dropped on clear.</summary>
    public HashSet<int> TakenKeys { get; } = [];

    public const int ClearKey = -1;

    /// <summary>The stall's items, drawn on the first visit.</summary>
    public List<Run.ShopItem> Stock { get; } = [];

    public HashSet<int> Sold { get; } = [];

    /// <summary>Chests by name ("room", "lair", "bonus"); true once opened.</summary>
    public Dictionary<string, bool> Chests { get; } = [];
}

/// <summary>
/// The rooms of one run on a grid, with what happened in each. Cells side by side connect west and east;
/// cells stacked connect up and down (the lower room has a top gap, the upper one an open bottom).
/// </summary>
public sealed class StageMap
{
    private readonly RoomState[] _states;
    private readonly (int X, int Y)[] _cells;

    public StageMap(IReadOnlyList<string> rooms, IReadOnlyList<(int X, int Y)> cells)
    {
        Rooms = rooms;
        _cells = [.. cells];
        _states = rooms.Select(_ => new RoomState()).ToArray();
    }

    /// <summary>A plain row, west to east.</summary>
    public StageMap(IReadOnlyList<string> rooms)
        : this(rooms, rooms.Select((_, i) => (i, 0)).ToList())
    {
    }

    public IReadOnlyList<string> Rooms { get; }

    public int Current { get; private set; }

    public RoomState State(int index) => _states[index];

    public RoomState CurrentState => _states[Current];

    public string CurrentRoom => Rooms[Current];

    public (int X, int Y) Cell(int index) => _cells[index];

    /// <returns>The room in that direction, or -1.</returns>
    public int Neighbor(int index, int dx, int dy)
    {
        var target = (_cells[index].X + dx, _cells[index].Y + dy);
        return Array.IndexOf(_cells, target);
    }

    /// <summary>West (-1) or east (1).</summary>
    public int Neighbor(int index, int side) => Neighbor(index, Math.Sign(side), 0);

    public void Enter(int index)
    {
        Current = index;
        _states[index].Visited = true;
    }

    /// <summary>Shown on the minimap: visited, or next to a visited room.</summary>
    public bool IsKnown(int index) =>
        _states[index].Visited || Directions.Any(d => Neighbor(index, d.X, d.Y) is var near && near >= 0 && _states[near].Visited);

    public static readonly (int X, int Y)[] Directions = [(-1, 0), (1, 0), (0, -1), (0, 1)];

    /// <summary>
    /// Picks a layout and fills each cell with an unused room of its kind. A cell with a cell above needs a room with a
    /// top gap; a cell with a cell below needs an open bottom. The same seed gives the same map.
    /// </summary>
    /// <param name="withShop">False before the Raccoon has been healed: shop cells take a treasure room.</param>
    public static StageMap Generate(RunDef run, ulong seed, bool withShop = true)
    {
        var random = new SeededRandom(seed);
        var layout = run.Layouts[random.NextInt(0, run.Layouts.Count)];
        var cells = layout.Cells.Select(cell => (cell.X, cell.Y)).ToList();
        var used = new HashSet<string>();
        var rooms = new List<string>();
        foreach (var cell in layout.Cells)
        {
            var needsUp = cells.Contains((cell.X, cell.Y - 1));
            var needsDown = cells.Contains((cell.X, cell.Y + 1));
            var kind = cell.Kind == RoomKinds.Shop && !withShop ? RoomKinds.Treasure : cell.Kind;
            var pool = run.Rooms.Where(room => room.Kind == kind && !used.Contains(room.Id)
                && (!needsUp || room.Up is not null) && (!needsDown || room.Down is not null)).ToList();
            if (pool.Count == 0)
            {
                throw new InvalidOperationException($"{layout.Id}: no {cell.Kind} room for ({cell.X}, {cell.Y})");
            }

            // rooms with a gap the cell does not use are kept for cells that need them
            var plain = pool.Where(room => (needsUp || room.Up is null) && (needsDown || room.Down is null)).ToList();
            var choices = plain.Count > 0 ? plain : pool;
            var pick = choices[random.NextInt(0, choices.Count)];
            used.Add(pick.Id);
            rooms.Add(pick.Id);
        }

        return new StageMap(rooms, cells);
    }
}
