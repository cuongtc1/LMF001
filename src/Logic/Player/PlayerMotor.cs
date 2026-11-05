using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Player;

public readonly record struct MotorInput(
    float MoveX,
    bool JumpPressed,
    bool JumpHeld,
    bool DashPressed,
    bool OnFloor,
    bool Locked,
    float GravityScale,
    float LungeVelocityX);

/// <summary>Run, jump and dash rules. One Step call is one 1/60 s tick; velocities are pixels per second.</summary>
public sealed class PlayerMotor(MovementDef move)
{
    private const float TickSeconds = 1f / 60f;

    private int _coyote;
    private int _jumpBuffer;
    private bool _dashQueued;
    private int _dashTicks;
    private int _dashCooldown;
    private int _dashInvulnerable;
    private int _airDashesLeft = move.AirDashes;
    private bool _jumpRising;

    public float VelocityX { get; private set; }

    public float VelocityY { get; private set; }

    public int Facing { get; private set; } = 1;

    public bool IsDashing => _dashTicks > 0;

    public bool IsInvulnerable => _dashInvulnerable > 0;

    /// <summary>A jump or dash is waiting for the current lock (attack, stun) to end.</summary>
    public bool HasQueuedAction => _jumpBuffer > 0 || _dashQueued;

    public void Face(int direction) => Facing = direction;

    public void Knockback(float velocityX, float velocityY)
    {
        VelocityX = velocityX;
        VelocityY = velocityY;
        _dashTicks = 0;
    }

    public void Step(in MotorInput input)
    {
        Tickdown();
        if (input.JumpPressed)
        {
            _jumpBuffer = move.JumpBufferTicks;
        }

        if (input.DashPressed && _dashCooldown == 0)
        {
            _dashQueued = true;
        }

        if (input.OnFloor)
        {
            _coyote = move.CoyoteTicks;
            _airDashesLeft = move.AirDashes;
            VelocityY = Math.Min(VelocityY, 0);
            _jumpRising = false;
        }

        if (!input.Locked)
        {
            if (input.MoveX != 0 && !IsDashing)
            {
                Facing = Math.Sign(input.MoveX);
            }

            TryStartDash(input);
            TryJump();
        }

        if (IsDashing)
        {
            VelocityX = Facing * move.DashSpeed;
            VelocityY = 0;
            return;
        }

        StepHorizontal(input);
        StepVertical(input);
    }

    private void Tickdown()
    {
        _coyote = Math.Max(0, _coyote - 1);
        _jumpBuffer = Math.Max(0, _jumpBuffer - 1);
        _dashTicks = Math.Max(0, _dashTicks - 1);
        _dashCooldown = Math.Max(0, _dashCooldown - 1);
        _dashInvulnerable = Math.Max(0, _dashInvulnerable - 1);
    }

    private void TryStartDash(in MotorInput input)
    {
        if (!_dashQueued)
        {
            return;
        }

        _dashQueued = false;
        if (!input.OnFloor && _airDashesLeft == 0)
        {
            return;
        }

        if (!input.OnFloor)
        {
            _airDashesLeft--;
        }

        _dashTicks = move.DashTicks;
        _dashCooldown = move.DashCooldownTicks;
        _dashInvulnerable = move.DashInvulnerableTicks;
    }

    private void TryJump()
    {
        if (_jumpBuffer > 0 && _coyote > 0)
        {
            VelocityY = -move.JumpVelocity;
            _jumpBuffer = 0;
            _coyote = 0;
            _jumpRising = true;
        }
    }

    private void StepHorizontal(in MotorInput input)
    {
        if (input.LungeVelocityX != 0)
        {
            VelocityX = input.LungeVelocityX;
            return;
        }

        var target = input.Locked ? 0 : input.MoveX * move.RunSpeed;
        var acceleration = input.OnFloor ? move.GroundAcceleration : move.AirAcceleration;
        VelocityX = MathUtils.Approach(VelocityX, target, acceleration * TickSeconds);
    }

    private void StepVertical(in MotorInput input)
    {
        if (_jumpRising && !input.JumpHeld && VelocityY < 0)
        {
            VelocityY *= move.JumpCutScale;
            _jumpRising = false;
        }

        var scale = VelocityY > 0 ? move.FallGravityScale : 1;
        VelocityY += move.Gravity * scale * input.GravityScale * TickSeconds;
        VelocityY = Math.Min(VelocityY, move.MaxFallSpeed);
    }
}
