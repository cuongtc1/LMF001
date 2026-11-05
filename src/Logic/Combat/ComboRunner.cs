using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Combat;

/// <summary>How the current attack press starts a combo.</summary>
public enum ComboEntry
{
    Ground,
    Air,
    SprintAir,
    Sprint,
}

public enum AttackPhase
{
    None,
    Startup,
    Active,
    Recovery,
}

/// <summary>Runs an attack chain tick by tick: startup, active, recovery, with a buffered next press.</summary>
public sealed class ComboRunner(AttackSetDef set, int bufferTicks)
{
    private int _buffer;
    private float _tickRemainder;
    private bool _airStringSpent;
    private bool _heavyBuffered;

    /// <summary>Attack ticks that pass per game tick.</summary>
    public float AttackSpeed { get; set; } = 1;

    public AttackDef? Current { get; private set; }

    public int Tick { get; private set; }

    public bool JustStarted { get; private set; }

    /// <summary>Changes on every swing, so a hitbox hits each target once per swing.</summary>
    public int SwingId { get; private set; }

    public bool IsAttacking => Current is not null;

    public bool CanCancel => Current is { } attack && Tick >= attack.CancelFromTick;

    /// <summary>A press is waiting that continues this string.</summary>
    public bool HasBufferedFollow => _buffer > 0 && Current is { } attack && FollowOf(attack) is not null;

    public AttackPhase Phase
    {
        get
        {
            if (Current is not { } attack)
            {
                return AttackPhase.None;
            }

            if (Tick < attack.StartupTicks)
            {
                return AttackPhase.Startup;
            }

            return Tick < attack.StartupTicks + attack.ActiveTicks ? AttackPhase.Active : AttackPhase.Recovery;
        }
    }

    /// <summary>0 to 1 across the whole attack.</summary>
    public float Progress => Current is { } attack ? (float)Tick / attack.TotalTicks : 0;

    public void PressAttack(bool heavy = false)
    {
        _buffer = bufferTicks;
        _heavyBuffered = heavy;
    }

    public void Cancel()
    {
        Current = null;
        Tick = 0;
    }

    /// <summary>Only one air string per jump: if it ends or breaks, the next one waits for the ground.</summary>
    public void Advance(ComboEntry entry = ComboEntry.Ground)
    {
        JustStarted = false;
        var inAir = entry is ComboEntry.Air or ComboEntry.SprintAir;
        if (!inAir)
        {
            _airStringSpent = false;
        }

        if (Current is { } attack)
        {
            _tickRemainder += AttackSpeed;
            Tick += (int)_tickRemainder;
            _tickRemainder -= (int)_tickRemainder;
            if (Tick >= attack.ChainFromTick && _buffer > 0 && FollowOf(attack) is { } next)
            {
                Start(next);
            }
            else if (Tick >= attack.TotalTicks)
            {
                Cancel();
            }
        }

        if (Current is null && _buffer > 0 && !(inAir && _airStringSpent) && FirstAttack(entry) is { } first)
        {
            _airStringSpent = inAir;
            Start(first);
        }

        _buffer = Math.Max(0, _buffer - 1);
    }

    /// <summary>X has no air version yet, so it does nothing in the air.</summary>
    private string? FirstAttack(ComboEntry entry) => (entry, _heavyBuffered) switch
    {
        (ComboEntry.Air, false) => set.FirstAir,
        (ComboEntry.SprintAir, false) => set.FirstSprintAir,
        (ComboEntry.Air or ComboEntry.SprintAir, true) => null,
        (ComboEntry.Sprint, false) => set.FirstSprint,
        (ComboEntry.Sprint, true) => set.FirstSprintHeavy,
        (_, true) => set.FirstHeavy,
        _ => set.First,
    };

    private string? FollowOf(AttackDef attack) => _heavyBuffered ? attack.NextHeavy : attack.Next;

    private void Start(string id)
    {
        Current = set.Attacks.First(attack => attack.Id == id);
        Tick = 0;
        _tickRemainder = 0;
        SwingId++;
        JustStarted = true;
        _buffer = 0;
    }
}
