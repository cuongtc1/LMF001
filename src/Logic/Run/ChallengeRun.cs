using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Run;

public static class ChallengeKinds
{
    /// <summary>Optional: the lever summons stronger monsters and locks the door until all are healed. Wins a big chest.</summary>
    public const string Lair = "lair";

    /// <summary>A normal fight room with a bonus: heal everyone without an unguarded hit for an extra chest.</summary>
    public const string NoHit = "no_hit";

    public static readonly string[] All = [Lair, NoHit];
}

public enum ChallengeState
{
    Waiting,
    Running,
    Won,
    Failed,
}

/// <summary>A room trial. A lair waits for its lever; a no-hit trial runs from the start.</summary>
public sealed class ChallengeRun
{
    public ChallengeRun(ChallengeDef def)
    {
        Def = def;
        if (!NeedsLever)
        {
            State = ChallengeState.Running;
        }
    }

    public ChallengeDef Def { get; }

    public ChallengeState State { get; private set; } = ChallengeState.Waiting;

    public bool NeedsLever => Def.Kind == ChallengeKinds.Lair;

    /// <summary>A lair keeps its door shut while running: once accepted, it must be finished.</summary>
    public bool LocksDoor => NeedsLever && State == ChallengeState.Running;

    public bool Start()
    {
        if (State != ChallengeState.Waiting)
        {
            return false;
        }

        State = ChallengeState.Running;
        return true;
    }

    /// <param name="roomHealed">Every wave is spawned and healed.</param>
    public ChallengeState Step(bool roomHealed)
    {
        if (State == ChallengeState.Running && roomHealed)
        {
            State = ChallengeState.Won;
        }

        return State;
    }

    /// <summary>Only the no-hit bonus can be lost; a lair is lost only by losing the run.</summary>
    public void PlayerHit()
    {
        if (State == ChallengeState.Running && Def.Kind == ChallengeKinds.NoHit)
        {
            State = ChallengeState.Failed;
        }
    }
}
