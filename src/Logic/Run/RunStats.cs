namespace LumiFeast.Logic.Run;

/// <summary>What one run achieved, shown on the result screen.</summary>
public sealed class RunStats
{
    private const int SecondsPerMinute = 60;

    public int Ticks { get; private set; }

    public int RoomsCleared { get; private set; }

    public int MonstersHealed { get; private set; }

    public int BestCombo { get; private set; }

    public void Reset()
    {
        Ticks = 0;
        RoomsCleared = 0;
        MonstersHealed = 0;
        BestCombo = 0;
    }

    public void Step() => Ticks++;

    public void RoomCleared() => RoomsCleared++;

    public void Healed() => MonstersHealed++;

    public void Combo(int hits) => BestCombo = Math.Max(BestCombo, hits);

    /// <summary>Play time as m:ss.</summary>
    public string Time(int ticksPerSecond)
    {
        var seconds = Ticks / ticksPerSecond;
        return $"{seconds / SecondsPerMinute}:{seconds % SecondsPerMinute:00}";
    }
}
