using LumiFeast.Logic.Data;
using LumiFeast.Logic.Stats;

namespace LumiFeast.Logic.Enemies;

public enum MonsterState
{
    Idle,
    Chase,
    Windup,
    Attack,
    Recover,
    Stunned,
}

public static class MonsterBehaviors
{
    public const string Passive = "passive";
    public const string Chaser = "chaser";
    public const string Flyer = "flyer";
}

/// <summary>Shared AI: walk, hop or fly to the target, wind up, strike, recover. Numbers come from data.</summary>
public sealed class MonsterBrain(MonsterAiDef ai, MonsterStats stats)
{
    private const float TicksPerSecond = 60;

    private int _timer;
    private int _cooldown;
    private int _hopTimer;

    public MonsterState State { get; private set; } = MonsterState.Idle;

    public int Facing { get; private set; } = -1;

    public float MoveVelocityX { get; private set; }

    public float MoveVelocityY { get; private set; }

    public bool WantsHop { get; private set; }

    public bool StrikeActive => State == MonsterState.Attack;

    /// <summary>Starts a new swing id each time it enters the attack state.</summary>
    public int SwingId { get; private set; }

    /// <summary>Hits land but do not stop this attack.</summary>
    public bool IsArmored => ai.SuperArmor && State is MonsterState.Windup or MonsterState.Attack;

    public void Stun()
    {
        if (!IsArmored)
        {
            Set(MonsterState.Stunned, ai.StunTicks);
        }
    }

    /// <param name="deltaX">Target x minus monster x.</param>
    /// <param name="hoverError">Flyers only: pixels below the hover height.</param>
    public void Step(float deltaX, bool onFloor, float hoverError = 0)
    {
        WantsHop = false;
        MoveVelocityX = 0;
        MoveVelocityY = 0;
        _cooldown = Math.Max(0, _cooldown - 1);
        _hopTimer = Math.Max(0, _hopTimer - 1);
        _timer = Math.Max(0, _timer - 1);
        if (ai.Behavior == MonsterBehaviors.Passive)
        {
            if (State == MonsterState.Stunned && _timer == 0)
            {
                State = MonsterState.Idle;
            }

            return;
        }

        switch (State)
        {
            case MonsterState.Idle:
            case MonsterState.Chase:
                Think(deltaX, onFloor);
                break;
            case MonsterState.Windup when _timer == 0:
                SwingId++;
                Set(MonsterState.Attack, ai.ActiveTicks);
                break;
            case MonsterState.Attack:
                MoveVelocityX = Facing * ai.LungeSpeed;
                if (_timer == 0)
                {
                    Set(MonsterState.Recover, Scaled(ai.RecoveryTicks));
                }

                break;
            case MonsterState.Recover when _timer == 0:
                _cooldown = Scaled(ai.CooldownTicks);
                State = MonsterState.Idle;
                break;
            case MonsterState.Stunned when _timer == 0:
                State = MonsterState.Idle;
                break;
        }

        if (ai.Behavior == MonsterBehaviors.Flyer)
        {
            Fly(hoverError);
        }
    }

    /// <summary>Dives during the strike; otherwise climbs back to its hover height.</summary>
    private void Fly(float hoverError)
    {
        if (State == MonsterState.Attack)
        {
            MoveVelocityY = ai.LungeSpeed;
        }
        else if (State != MonsterState.Stunned)
        {
            MoveVelocityY = Math.Clamp(-hoverError * TicksPerSecond, -stats.Speed, stats.Speed);
        }
    }

    private void Think(float deltaX, bool onFloor)
    {
        var distance = Math.Abs(deltaX);
        if (distance > ai.AggroRange)
        {
            State = MonsterState.Idle;
            return;
        }

        Facing = deltaX < 0 ? -1 : 1;
        if (distance <= ai.AttackRange)
        {
            State = MonsterState.Chase;
            if (_cooldown == 0 && onFloor)
            {
                Set(MonsterState.Windup, Scaled(ai.WindupTicks));
            }

            return;
        }

        State = MonsterState.Chase;
        if (ai.HopVelocity <= 0)
        {
            MoveVelocityX = Facing * stats.Speed;
        }
        else if (!onFloor)
        {
            MoveVelocityX = Facing * stats.Speed;
        }
        else if (_hopTimer == 0)
        {
            WantsHop = true;
            MoveVelocityX = Facing * stats.Speed;
            _hopTimer = Scaled(ai.HopIntervalTicks);
        }
    }

    private int Scaled(int ticks) => (int)MathF.Round(ticks * stats.CooldownScale);

    private void Set(MonsterState state, int ticks)
    {
        State = state;
        _timer = ticks;
    }
}
