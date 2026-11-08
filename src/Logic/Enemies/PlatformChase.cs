using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Enemies;

public enum ClimbAction
{
    None,

    /// <summary>Jump straight up with <see cref="PlatformChase.JumpVelocity"/>; one-way ledges catch the monster on the way down.</summary>
    Jump,

    /// <summary>Fall through the ledge it stands on.</summary>
    Drop,
}

/// <summary>
/// Lets ground monsters follow Lumi between the floor and ledges, so standing on a ledge is not a safe spot.
/// They wait a moment first, so a quick hop onto a ledge still works as a dodge.
/// </summary>
public sealed class PlatformChase(MonsterClimbDef def)
{
    private int _above;
    private int _below;
    private int _cooldown;

    public float JumpVelocity { get; private set; }

    /// <summary>Lumi is close enough in height to be hit; otherwise ground monsters do not swing at the air.</summary>
    public bool CanReach(float deltaY) => Math.Abs(deltaY) <= def.ReachHeight;

    /// <param name="deltaX">Lumi's feet minus the monster's feet, horizontally.</param>
    /// <param name="deltaY">Lumi's feet minus the monster's feet; negative when Lumi is higher.</param>
    /// <param name="onLedge">The monster stands on a ledge, not the floor.</param>
    /// <param name="lumiStanding">Lumi stands on the floor or a ledge (not in the air).</param>
    public ClimbAction Step(float deltaX, float deltaY, bool onFloor, bool onLedge, bool lumiStanding, float gravity)
    {
        _cooldown = Math.Max(0, _cooldown - 1);
        var standing = onFloor && lumiStanding;
        _above = standing && deltaY < -def.StepHeight ? _above + 1 : 0;
        _below = standing && onLedge && deltaY > def.StepHeight ? _below + 1 : 0;
        if (_cooldown > 0)
        {
            return ClimbAction.None;
        }

        if (_above >= def.WaitTicks && Math.Abs(deltaX) <= def.JumpReach && -deltaY <= def.MaxJumpHeight)
        {
            // rise a little above the ledge top, so the monster lands on it while falling
            JumpVelocity = MathF.Sqrt(2 * gravity * (-deltaY + def.Clearance));
            return Done(ClimbAction.Jump);
        }

        return _below >= def.WaitTicks ? Done(ClimbAction.Drop) : ClimbAction.None;
    }

    private ClimbAction Done(ClimbAction action)
    {
        _above = 0;
        _below = 0;
        _cooldown = def.CooldownTicks;
        return action;
    }
}
