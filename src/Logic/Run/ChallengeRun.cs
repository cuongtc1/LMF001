using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Run;

public static class ChallengeKinds
{
    /// <summary>Heal every monster before the clock runs out.</summary>
    public const string TimeLimit = "time_limit";

    /// <summary>Heal every monster without taking a hit. Guarded hits do not count.</summary>
    public const string NoHit = "no_hit";

    public static readonly string[] All = [TimeLimit, NoHit];
}

public enum ChallengeState
{
    Waiting,
    Running,
    Won,
    Failed,
}

/// <summary>An optional room trial. It waits for the lever, then is won by healing the room in time.</summary>
public sealed class ChallengeRun(ChallengeDef def, int ticksPerSecond)
{
    public ChallengeDef Def { get; } = def;

    public ChallengeState State { get; private set; } = ChallengeState.Waiting;

    public int TicksLeft { get; private set; }

    public bool HasClock => Def.Kind == ChallengeKinds.TimeLimit;

    /// <summary>Whole seconds left on the clock, rounded up so it never shows 0 while running.</summary>
    public int SecondsLeft => (TicksLeft + ticksPerSecond - 1) / ticksPerSecond;

    public bool Start()
    {
        if (State != ChallengeState.Waiting)
        {
            return false;
        }

        State = ChallengeState.Running;
        TicksLeft = Def.Seconds * ticksPerSecond;
        return true;
    }

    /// <param name="roomHealed">Every wave is spawned and healed.</param>
    public ChallengeState Step(bool roomHealed)
    {
        if (State != ChallengeState.Running)
        {
            return State;
        }

        if (roomHealed)
        {
            State = ChallengeState.Won;
        }
        else if (HasClock && --TicksLeft <= 0)
        {
            State = ChallengeState.Failed;
        }

        return State;
    }

    public void PlayerHit()
    {
        if (State == ChallengeState.Running && Def.Kind == ChallengeKinds.NoHit)
        {
            State = ChallengeState.Failed;
        }
    }
}
