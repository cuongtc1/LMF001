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
    bool AirHold = false,
    bool SprintHeld = false);

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
    private int _tapDirection;
    private int _tapTimer;
    private int _lastMoveDirection;
    private bool _sprintQueued;
    private bool _jumpRising;
    private bool _sprintButtonHeld;

    public float VelocityX { get; private set; }

    public float VelocityY { get; private set; }

    public int Facing { get; private set; } = 1;

    public bool IsDashing => _dashTicks > 0;

    public bool IsSprinting { get; private set; }

    /// <summary>This jump left the ground at a sprint; it stays true until landing.</summary>
    public bool IsSprintJump { get; private set; }

    public bool IsInvulnerable => _dashInvulnerable > 0;

    /// <summary>The double jump is a story reward, so it starts locked.</summary>
    public bool AirJumpUnlocked { get; set; }

    /// <summary>Plays run and jump faster. Jump height and jump distance stay the same, so levels never change.</summary>
    public float Tempo { get; set; } = 1;

    /// <summary>A jump or dash is waiting for the current lock (attack, stun) to end.</summary>
    public bool HasQueuedAction => _jumpBuffer > 0 || _dashQueued || _sprintQueued;

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

        TrackDoubleTap(input);
        TrackSprintButton(input);
        if (input.OnFloor)
        {
            _coyote = move.CoyoteTicks;
            _airDashesLeft = move.AirDashes;
            _airJumpsLeft = move.AirJumps;
            VelocityY = Math.Min(VelocityY, 0);
            _jumpRising = false;
            IsSprintJump = false;
        }

        if (!input.Locked)
        {
            if (input.MoveX != 0 && !IsDashing)
            {
                Facing = Math.Sign(input.MoveX);
            }

            TryStartDash(input);
            TryJump(input);
            if (_sprintQueued)
            {
                _sprintQueued = false;
                IsSprinting = true;
            }
        }
        else
        {
            IsSprinting = false;
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

    /// <summary>Two quick presses of one direction start a sprint; it lasts while the direction is held.</summary>
    private void TrackDoubleTap(in MotorInput input)
    {
        var direction = Math.Sign(input.MoveX);
        _tapTimer = Math.Max(0, _tapTimer - 1);
        if (direction != 0 && _lastMoveDirection == 0)
        {
            _sprintQueued = direction == _tapDirection && _tapTimer > 0;
            _tapDirection = direction;
            _tapTimer = move.SprintTapTicks;
        }

        if (direction != _tapDirection || direction == 0)
        {
            IsSprinting = false;
            _sprintQueued = false;
        }

        _lastMoveDirection = direction;
    }

    /// <summary>The sprint button needs no double tap: hold it with a direction. Made for gamepads.</summary>
    private void TrackSprintButton(in MotorInput input)
    {
        if (input.SprintHeld && input.MoveX != 0 && !input.Locked)
        {
            IsSprinting = true;
        }
        else if (_sprintButtonHeld && !input.SprintHeld)
        {
            IsSprinting = false;
        }

        _sprintButtonHeld = input.SprintHeld;
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
            IsSprintJump = IsSprinting;
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

        var fastFall = input.FastFall && !input.OnFloor && !input.AirHold;
        var falling = VelocityY > 0;
        var scale = fastFall ? move.FastFallGravityScale : falling ? move.FallGravityScale * input.GravityScale : 1;
        VelocityY += move.Gravity * Tempo * Tempo * scale * TickSeconds;
        VelocityY = Math.Min(VelocityY, (fastFall ? move.FastFallMaxSpeed : move.MaxFallSpeed) * Tempo);
        if (input.AirHold)
        {
            VelocityY = Math.Min(VelocityY, 0);
        }
    }
}
