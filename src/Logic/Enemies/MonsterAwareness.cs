using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Enemies;

public enum AwarenessState
{
    /// <summary>Has not seen Lumi: patrols near its spawn point.</summary>
    Unaware,

    /// <summary>Just saw Lumi: stands still with a "!" before it starts the fight.</summary>
    Noticing,

    /// <summary>Fights: the brain chases and attacks.</summary>
    Aware,
}

/// <summary>
/// Monsters fight only after they notice Lumi (Little Noah shows a "!" the moment a foe notices Noah).
/// They see ahead of them within a range and a height band, sense her when she is very close behind,
/// and wake up at once when hit or when a nearby monster calls. Out of range for a while, they give up.
/// </summary>
public sealed class MonsterAwareness(MonsterAwarenessDef def, int facing)
{
    private int _timer;
    private int _outOfRangeTicks;

    public AwarenessState State { get; private set; } = AwarenessState.Unaware;

    public int Facing { get; private set; } = facing < 0 ? -1 : 1;

    /// <summary>Patrol walk direction times the patrol speed share; 0 while pausing or fighting.</summary>
    public float PatrolMove { get; private set; }

    /// <summary>True on the tick the monster notices Lumi (show the "!" and call the others).</summary>
    public bool JustNoticed { get; private set; }

    public bool IsFighting => State == AwarenessState.Aware;

    public bool Sees(float deltaX, float deltaY)
    {
        var distance = Math.Abs(deltaX);
        var inFront = Math.Sign(deltaX) == Facing || distance <= def.BackSense;
        return inFront && distance <= def.SightRange && Math.Abs(deltaY) <= def.SightHeight;
    }

    /// <summary>A hit or a nearby monster's call: notice now, even without seeing Lumi.</summary>
    public void Alert(float deltaX)
    {
        if (State != AwarenessState.Unaware)
        {
            return;
        }

        Facing = deltaX < 0 ? -1 : 1;
        Notice();
    }

    /// <param name="deltaX">Lumi's x minus the monster's x.</param>
    /// <param name="deltaY">Lumi's feet minus the monster's feet.</param>
    /// <param name="fromHome">The monster's x minus its spawn x.</param>
    public void Step(float deltaX, float deltaY, float fromHome)
    {
        JustNoticed = false;
        PatrolMove = 0;
        switch (State)
        {
            case AwarenessState.Unaware:
                if (Sees(deltaX, deltaY))
                {
                    Facing = deltaX < 0 ? -1 : 1;
                    Notice();
                    return;
                }

                Patrol(fromHome);
                break;
            case AwarenessState.Noticing:
                if (--_timer <= 0)
                {
                    State = AwarenessState.Aware;
                    _outOfRangeTicks = 0;
                }

                break;
            case AwarenessState.Aware:
                _outOfRangeTicks = Math.Abs(deltaX) > def.LoseRange ? _outOfRangeTicks + 1 : 0;
                if (_outOfRangeTicks >= def.ForgetTicks)
                {
                    State = AwarenessState.Unaware;
                    _timer = 0;
                }

                break;
        }
    }

    private void Notice()
    {
        State = AwarenessState.Noticing;
        _timer = def.NoticeTicks;
        JustNoticed = true;
    }

    /// <summary>Walks to the end of its beat, pauses, turns around; after a lost fight it walks home first.</summary>
    private void Patrol(float fromHome)
    {
        if (_timer > 0)
        {
            _timer--;
            return;
        }

        if (Math.Abs(fromHome) >= def.PatrolDistance && Math.Sign(fromHome) == Facing)
        {
            Facing = -Facing;
            _timer = def.PatrolPauseTicks;
            return;
        }

        PatrolMove = Facing * def.PatrolSpeedScale;
    }
}
