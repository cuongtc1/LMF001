using LumiFeast.Logic.Common;
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
    Down,
    Dizzy,
}

public static class MonsterBehaviors
{
    public const string Passive = "passive";
    public const string Chaser = "chaser";
    public const string Flyer = "flyer";
}

public static class MonsterMoveKinds
{
    public const string Strike = "strike";
    public const string Throw = "throw";
    public const string Ambush = "ambush";
}

/// <summary>Shared AI: walk, hop or fly to the target, wind up, strike, recover. Numbers come from data.</summary>
public sealed class MonsterBrain(MonsterAiDef ai, MonsterStats stats, ulong seed = 1)
{
    /// <summary>Close enough under an unreachable Lumi to stop and wait (or jump, for climbers).</summary>
    private const float UnderTargetDistance = 20;

    private readonly SeededRandom _random = new(seed);
    private const float TicksPerSecond = 60;

    private int _timer;
    private int _cooldown;
    private int _hopTimer;
    private int _stoicTimer;
    private int _hitsTaken;
    private int _hitResetTimer;
    private int _nextHpMark;
    private float _stagger;
    private string _lastMove = string.Empty;
    private int _moveRepeats;

    public MonsterState State { get; private set; } = MonsterState.Idle;

    public int Facing { get; private set; } = -1;

    public float MoveVelocityX { get; private set; }

    public float MoveVelocityY { get; private set; }

    /// <summary>The boss move in play, or null.</summary>
    public MonsterMoveDef? CurrentMove { get; private set; }

    public bool InPhaseTwo { get; private set; }

    /// <summary>Ticks left in the current state.</summary>
    public int StateTicksLeft => _timer;

    public bool WantsHop { get; private set; }

    public bool StrikeActive => State == MonsterState.Attack;

    /// <summary>Starts a new swing id each time it enters the attack state.</summary>
    public int SwingId { get; private set; }

    /// <summary>Hits land but do not stop this attack.</summary>
    public bool IsArmored => _stoicTimer > 0 || (ai.SuperArmor && State is MonsterState.Windup or MonsterState.Attack);

    /// <summary>Hits can push, launch or stun it right now.</summary>
    public bool CanBeMoved => !IsArmored && State is not (MonsterState.Down or MonsterState.Dizzy);

    public float StaggerRatio => ai.StaggerMax > 0 ? _stagger / ai.StaggerMax : 0;

    public float DamageTakenScale => State == MonsterState.Dizzy ? ai.StaggerDamageScale : 1;

    /// <summary>Fills the stagger bar, even through armor. A full bar breaks armor and makes it dizzy.</summary>
    public bool AddStagger(float amount)
    {
        if (ai.StaggerMax <= 0 || State == MonsterState.Dizzy)
        {
            return false;
        }

        _stagger += amount;
        if (_stagger < ai.StaggerMax)
        {
            return false;
        }

        _stagger = 0;
        _stoicTimer = 0;
        Set(MonsterState.Dizzy, ai.StaggerBreakTicks);
        return true;
    }

    /// <summary>Armor from the hit count, an HP mark, or standing up. Counts only hits outside armor.</summary>
    public bool RegisterHit(float hpFraction)
    {
        if (_stoicTimer > 0)
        {
            return false;
        }

        _hitsTaken++;
        _hitResetTimer = ai.StoicResetTicks;
        if (!InPhaseTwo && ai.PhaseTwoHp > 0 && hpFraction <= ai.PhaseTwoHp)
        {
            InPhaseTwo = true;
            EnterStoic(ai.StoicTicks);
            return true;
        }

        var crossedMark = false;
        while (_nextHpMark < ai.StoicHpMarks.Count && hpFraction <= ai.StoicHpMarks[_nextHpMark])
        {
            crossedMark = true;
            _nextHpMark++;
        }

        if (!crossedMark && (ai.StoicThreshold == 0 || _hitsTaken < ai.StoicThreshold))
        {
            return false;
        }

        EnterStoic(ai.StoicTicks);
        return true;
    }

    /// <summary>A rolling monster turns around when it hits a wall.</summary>
    public void BounceOffWall()
    {
        if (ai.BouncesOffWalls && State == MonsterState.Attack)
        {
            Facing = -Facing;
        }
    }

    /// <summary>A hit from the front is held back unless it breaks guards or the monster is already reeling.</summary>
    public bool GuardsAgainst(int hitDirection, bool guardBreak) =>
        ai.FrontGuardScale < 1 && !guardBreak && hitDirection != Facing && CanBeMoved && State != MonsterState.Stunned;

    public void Stun(int ticks, bool knockdown = false)
    {
        if (!CanBeMoved)
        {
            return;
        }

        if (knockdown && ai.Behavior != MonsterBehaviors.Flyer)
        {
            Set(MonsterState.Down, ai.DownTicks);
            return;
        }

        Set(MonsterState.Stunned, (int)MathF.Round(ticks * ai.StunScale));
    }

    /// <param name="deltaX">Target x minus monster x.</param>
    /// <param name="hoverError">Flyers only: pixels below the hover height.</param>
    /// <param name="canReach">False when Lumi is far above or below: ground monsters walk under her instead of swinging.</param>
    public void Step(float deltaX, bool onFloor, float hoverError = 0, bool canReach = true)
    {
        WantsHop = false;
        MoveVelocityX = 0;
        MoveVelocityY = 0;
        _cooldown = Math.Max(0, _cooldown - 1);
        _hopTimer = Math.Max(0, _hopTimer - 1);
        _timer = Math.Max(0, _timer - 1);
        _stoicTimer = Math.Max(0, _stoicTimer - 1);
        StepHitMemory();
        if (ai.Behavior == MonsterBehaviors.Passive)
        {
            if (State is MonsterState.Stunned or MonsterState.Down && _timer == 0)
            {
                State = MonsterState.Idle;
            }

            return;
        }

        switch (State)
        {
            case MonsterState.Idle:
            case MonsterState.Chase:
                Think(deltaX, onFloor, canReach);
                break;
            case MonsterState.Windup when _timer == 0:
                SwingId++;
                Set(MonsterState.Attack, CurrentMove?.ActiveTicks ?? ai.ActiveTicks);
                break;
            case MonsterState.Attack:
                MoveVelocityX = Facing * (CurrentMove?.LungeSpeed ?? ai.LungeSpeed);
                if (_timer == 0)
                {
                    Set(MonsterState.Recover, Scaled(CurrentMove?.RecoveryTicks ?? ai.RecoveryTicks));
                }

                break;
            case MonsterState.Recover when _timer == 0:
                _cooldown = Scaled(ai.CooldownTicks);
                CurrentMove = null;
                State = MonsterState.Idle;
                break;
            case MonsterState.Stunned when _timer == 0:
                State = MonsterState.Idle;
                break;
            case MonsterState.Dizzy when _timer == 0:
                State = MonsterState.Idle;
                break;
            case MonsterState.Down when _timer == 0:
                State = MonsterState.Idle;
                _stoicTimer = ai.GetupArmorTicks;
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

    private void Think(float deltaX, bool onFloor, bool canReach)
    {
        var distance = Math.Abs(deltaX);
        if (distance > ai.AggroRange)
        {
            State = MonsterState.Idle;
            return;
        }

        Facing = deltaX < 0 ? -1 : 1;
        if (ai.Moves is { Count: > 0 } moves)
        {
            ThinkBoss(moves, distance);
            return;
        }

        if (!canReach)
        {
            State = MonsterState.Chase;
            MoveVelocityX = distance > UnderTargetDistance ? Facing * stats.Speed : 0;
            return;
        }

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

    private void EnterStoic(int ticks)
    {
        _stoicTimer = ticks;
        _hitsTaken = 0;
        _cooldown = 0;
        if (State is MonsterState.Stunned or MonsterState.Down)
        {
            State = MonsterState.Idle;
        }
    }

    private void StepHitMemory()
    {
        _hitResetTimer = Math.Max(0, _hitResetTimer - 1);
        if (_hitResetTimer == 0)
        {
            _hitsTaken = 0;
        }
    }

    /// <summary>Weighted pick among moves that fit the distance; never the same move three times in a row.</summary>
    private void ThinkBoss(IReadOnlyList<MonsterMoveDef> moves, float distance)
    {
        State = MonsterState.Chase;
        var choices = moves.Where(move => distance >= move.MinRange && distance <= move.MaxRange && !(move.Id == _lastMove && _moveRepeats >= 2)).ToList();
        if (_cooldown == 0 && choices.Count > 0)
        {
            CurrentMove = PickWeighted(choices);
            _moveRepeats = CurrentMove.Id == _lastMove ? _moveRepeats + 1 : 1;
            _lastMove = CurrentMove.Id;
            Set(MonsterState.Windup, Scaled(CurrentMove.WindupTicks));
            return;
        }

        if (distance > ai.AttackRange)
        {
            MoveVelocityX = Facing * stats.Speed * (InPhaseTwo ? ai.PhaseTwoSpeed : 1);
        }
    }

    private MonsterMoveDef PickWeighted(List<MonsterMoveDef> choices)
    {
        var roll = _random.NextDouble() * choices.Sum(move => move.Weight);
        foreach (var move in choices)
        {
            roll -= move.Weight;
            if (roll < 0)
            {
                return move;
            }
        }

        return choices[^1];
    }

    private int Scaled(int ticks) => (int)MathF.Round(ticks * stats.CooldownScale / (InPhaseTwo ? ai.PhaseTwoSpeed : 1));

    private void Set(MonsterState state, int ticks)
    {
        State = state;
        _timer = ticks;
    }
}
