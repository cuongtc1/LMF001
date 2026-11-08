using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Combat;

public enum GuardResult
{
    None,
    Blocked,
    Perfect,
}

/// <summary>
/// Holding guard blocks frontal hits. A hit right after raising it is a perfect guard (no chip damage).
/// With <see cref="GuardDef.AutoCounter"/> every block makes Lumi counter by herself (round 55); otherwise only
/// a perfect guard opens a short window to press Z.
/// </summary>
public sealed class GuardState(GuardDef guard)
{
    private int _heldTicks;
    private int _counterTimer;
    private bool _autoCounter;

    public bool IsGuarding => _heldTicks > 0;

    public bool CounterReady => _counterTimer > 0;

    public void Step(bool wantsGuard)
    {
        _heldTicks = wantsGuard ? _heldTicks + 1 : 0;
        _counterTimer = Math.Max(0, _counterTimer - 1);
    }

    public void ClearCounter()
    {
        _counterTimer = 0;
        _autoCounter = false;
    }

    /// <summary>True once after a block when the counter should start without a button press.</summary>
    public bool TakeAutoCounter()
    {
        var pending = _autoCounter;
        _autoCounter = false;
        return pending;
    }

    /// <param name="hitDirection">The way the hit travels: 1 is to the right.</param>
    /// <param name="facing">The way Lumi looks.</param>
    public GuardResult Resolve(int hitDirection, int facing)
    {
        if (!IsGuarding || hitDirection == facing)
        {
            return GuardResult.None;
        }

        var result = _heldTicks > guard.PerfectTicks ? GuardResult.Blocked : GuardResult.Perfect;
        if (guard.AutoCounter || result == GuardResult.Perfect)
        {
            _counterTimer = guard.CounterTicks;
            _autoCounter = guard.AutoCounter;
        }

        return result;
    }
}
