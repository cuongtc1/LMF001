using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Combat;

public enum GuardResult
{
    None,
    Blocked,
    Perfect,
}

/// <summary>Holding guard blocks frontal hits; a hit right after raising it opens a short counter window.</summary>
public sealed class GuardState(GuardDef guard)
{
    private int _heldTicks;
    private int _counterTimer;

    public bool IsGuarding => _heldTicks > 0;

    public bool CounterReady => _counterTimer > 0;

    public void Step(bool wantsGuard)
    {
        _heldTicks = wantsGuard ? _heldTicks + 1 : 0;
        _counterTimer = Math.Max(0, _counterTimer - 1);
    }

    public void ClearCounter() => _counterTimer = 0;

    /// <param name="hitDirection">The way the hit travels: 1 is to the right.</param>
    /// <param name="facing">The way Lumi looks.</param>
    public GuardResult Resolve(int hitDirection, int facing)
    {
        if (!IsGuarding || hitDirection == facing)
        {
            return GuardResult.None;
        }

        if (_heldTicks > guard.PerfectTicks)
        {
            return GuardResult.Blocked;
        }

        _counterTimer = guard.CounterTicks;
        return GuardResult.Perfect;
    }
}
