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
    float LungeVelocityX,
    bool FastFall = false,
    float PlungeSpeed = 0);

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
    private int _airJumpsLeft = move.AirJumps;
    private int _runDirection;
    private bool _jumpRising;

    public float VelocityX { get; private set; }

    public float VelocityY { get; private set; }

    public int Facing { get; private set; } = 1;

    public bool IsDashing => _dashTicks > 0;

    /// <summary>The fast run after a dash; it lasts while the same direction is held.</summary>
    public bool IsSprinting { get; private set; }

    public bool IsInvulnerable => _dashInvulnerable > 0;

    /// <summary>The double jump is a story reward, so it starts locked.</summary>
    public bool AirJumpUnlocked { get; set; }

    /// <summary>Plays run and jump faster. Jump height and jump distance stay the same, so levels never change.</summary>
    public float Tempo { get; set; } = 1;

    /// <summary>A jump or dash is waiting for the current lock (attack, stun) to end.</summary>
    public bool HasQueuedAction => _jumpBuffer > 0 || _dashQueued;

    public void Face(int direction) => Facing = direction;

    public void Knockback(float velocityX, float velocityY)
    {
        VelocityX = velocityX;
        VelocityY = velocityY;
        _dashTicks = 0;
    }

    /// <summary>An attack during a dash ends it early and keeps the run.</summary>
    public void StopDash()
    {
        _dashTicks = 0;
        StartRun();
    }

    public void Step(in MotorInput input)
    {
        var wasDashing = IsDashing;
        Tickdown();
        if (wasDashing && !IsDashing)
        {
            StartRun();
        }

        if (input.JumpPressed)
        {
            _jumpBuffer = move.JumpBufferTicks;
        }

        if (input.DashPressed && _dashCooldown == 0)
        {
            _dashQueued = true;
        }

        if (Math.Sign(input.MoveX) != _runDirection)
        {
            IsSprinting = false;
        }

        if (input.OnFloor)
        {
            _coyote = move.CoyoteTicks;
            _airDashesLeft = move.AirDashes;
            _airJumpsLeft = move.AirJumps;
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
            TryJump(input);
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

    private void StartRun()
    {
        IsSprinting = true;
        _runDirection = Facing;
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

    private void TryJump(in MotorInput input)
    {
        if (_jumpBuffer == 0)
        {
            return;
        }

        if (_coyote > 0)
        {
            StartJump(move.JumpVelocity);
            _coyote = 0;
        }
        else if (!input.OnFloor && AirJumpUnlocked && _airJumpsLeft > 0)
        {
            StartJump(move.AirJumpVelocity);
            _airJumpsLeft--;
        }
    }

    private void StartJump(float velocity)
    {
        VelocityY = -velocity * Tempo;
        _jumpBuffer = 0;
        _jumpRising = true;
    }

    private void StepHorizontal(in MotorInput input)
    {
        if (input.LungeVelocityX != 0)
        {
            VelocityX = input.LungeVelocityX;
            return;
        }

        var speed = (IsSprinting ? move.SprintSpeed : move.RunSpeed) * Tempo;
        var target = input.Locked ? 0 : input.MoveX * speed;
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

        if (input.PlungeSpeed > 0)
        {
            VelocityY = input.PlungeSpeed;
            return;
        }

        var fastFall = input.FastFall && !input.OnFloor;
        var falling = VelocityY > 0;
        var scale = fastFall ? move.FastFallGravityScale : falling ? move.FallGravityScale * input.GravityScale : 1;
        VelocityY += move.Gravity * Tempo * Tempo * scale * TickSeconds;
        VelocityY = Math.Min(VelocityY, (fastFall ? move.FastFallMaxSpeed : move.MaxFallSpeed) * Tempo);
    }
}
