using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Combat;

/// <summary>How the current attack press starts a combo.</summary>
public enum ComboEntry
{
    Ground,
    Air,
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

    /// <summary>Attack ticks that pass per game tick.</summary>
    public float AttackSpeed { get; set; } = 1;

    public AttackDef? Current { get; private set; }

    public int Tick { get; private set; }

    public bool JustStarted { get; private set; }

    /// <summary>Changes on every swing, so a hitbox hits each target once per swing.</summary>
    public int SwingId { get; private set; }

    public bool IsAttacking => Current is not null;

    public bool CanCancel => Current is { } attack && Tick >= attack.CancelFromTick;

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

    public void PressAttack() => _buffer = bufferTicks;

    public void Cancel()
    {
        Current = null;
        Tick = 0;
    }

    public void Advance(ComboEntry entry = ComboEntry.Ground)
    {
        JustStarted = false;
        if (Current is { } attack)
        {
            _tickRemainder += AttackSpeed;
            Tick += (int)_tickRemainder;
            _tickRemainder -= (int)_tickRemainder;
            if (Phase == AttackPhase.Recovery && _buffer > 0 && attack.Next is { } next)
            {
                Start(next);
            }
            else if (Tick >= attack.TotalTicks)
            {
                Cancel();
            }
        }

        if (Current is null && _buffer > 0)
        {
            Start(entry switch
            {
                ComboEntry.Air => set.FirstAir,
                ComboEntry.Sprint => set.FirstSprint,
                _ => set.First,
            });
        }

        _buffer = Math.Max(0, _buffer - 1);
    }

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
