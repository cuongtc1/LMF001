using LumiFeast.Logic.Player;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class PlayerMotorTests
{
    private static PlayerMotor NewMotor() => new(TestData.Game.Character("lumi").Movement);

    private static MotorInput Ground(float move = 0, bool jump = false, bool dash = false) =>
        new(move, jump, jump, dash, true, false, 1, 0);

    private static MotorInput Air(bool held = false) => new(0, false, held, false, false, false, 1, 0);

    [Fact]
    public void RunReachesTopSpeed()
    {
        var motor = NewMotor();
        for (var i = 0; i < 60; i++)
        {
            motor.Step(Ground(1));
        }

        Assert.Equal(TestData.Game.Character("lumi").Movement.RunSpeed, motor.VelocityX);
    }

    [Fact]
    public void CoyoteTimeAllowsJumpJustAfterLeavingTheFloor()
    {
        var motor = NewMotor();
        motor.Step(Ground());
        motor.Step(new MotorInput(0, true, true, false, false, false, 1, 0));
        Assert.True(motor.VelocityY < 0);
    }

    [Fact]
    public void AirJumpsRunOut()
    {
        var motor = NewMotor();
        motor.Step(Ground());
        for (var i = 0; i < 30; i++)
        {
            motor.Step(Air());
        }

        motor.Step(new MotorInput(0, true, true, false, false, false, 1, 0));
        var before = motor.VelocityY;
        motor.Step(Air(true));
        motor.Step(new MotorInput(0, true, true, false, false, false, 1, 0));
        Assert.True(motor.VelocityY >= before);
    }

    [Fact]
    public void ReleasingJumpCutsTheRise()
    {
        var held = NewMotor();
        var tapped = NewMotor();
        held.Step(Ground(jump: true));
        tapped.Step(Ground(jump: true));
        held.Step(Air(true));
        tapped.Step(Air(false));
        Assert.True(tapped.VelocityY > held.VelocityY);
    }

    [Fact]
    public void DashIsInvulnerableAndHasCooldown()
    {
        var motor = NewMotor();
        motor.Step(Ground(dash: true));
        Assert.True(motor.IsDashing);
        Assert.True(motor.IsInvulnerable);
        Assert.False(new PlayerMotorProbe(motor).CanDashAgain());
    }

    [Fact]
    public void JumpQueuedDuringAttackFiresWhenUnlocked()
    {
        var motor = NewMotor();
        motor.Step(new MotorInput(0, true, true, false, true, true, 1, 0));
        Assert.True(motor.HasQueuedAction);
        Assert.True(motor.VelocityY >= 0);
        motor.Step(new MotorInput(0, false, true, false, true, false, 1, 0));
        Assert.True(motor.VelocityY < 0);
    }

    [Fact]
    public void AirJumpLiftsAgainWhileFalling()
    {
        var motor = NewMotor();
        for (var i = 0; i < 30; i++)
        {
            motor.Step(Air());
        }

        Assert.True(motor.VelocityY > 0);
        motor.Step(new MotorInput(0, true, true, false, false, false, 1, 0));
        Assert.True(motor.VelocityY < 0);
    }

    [Fact]
    public void AirJumpsRefillOnLanding()
    {
        var motor = NewMotor();
        motor.Step(new MotorInput(0, true, true, false, false, false, 1, 0));
        motor.Step(Ground());
        for (var i = 0; i < 30; i++)
        {
            motor.Step(Air());
        }

        motor.Step(new MotorInput(0, true, true, false, false, false, 1, 0));
        Assert.True(motor.VelocityY < 0);
    }

    [Fact]
    public void DownFallsFaster()
    {
        var plain = NewMotor();
        var fast = NewMotor();
        for (var i = 0; i < 20; i++)
        {
            plain.Step(Air());
            fast.Step(Air() with { FastFall = true });
        }

        Assert.True(fast.VelocityY > plain.VelocityY);
    }

    [Fact]
    public void DoubleTapSprintsUntilReleased()
    {
        var motor = NewMotor();
        motor.Step(Ground(1));
        motor.Step(Ground(0));
        motor.Step(Ground(1));
        Assert.True(motor.IsSprinting);
        motor.Step(Ground(0));
        Assert.False(motor.IsSprinting);
    }

    [Fact]
    public void SlowTapsDoNotSprint()
    {
        var motor = NewMotor();
        motor.Step(Ground(1));
        for (var i = 0; i < 40; i++)
        {
            motor.Step(Ground(0));
        }

        motor.Step(Ground(1));
        Assert.False(motor.IsSprinting);
    }

    [Fact]
    public void SprintIsFasterThanRun()
    {
        var motor = NewMotor();
        motor.Step(Ground(1));
        motor.Step(Ground(0));
        for (var i = 0; i < 60; i++)
        {
            motor.Step(Ground(1));
        }

        Assert.Equal(TestData.Game.Character("lumi").Movement.SprintSpeed, motor.VelocityX);
    }

    [Fact]
    public void DoubleTapDuringAttackQueuesForCancel()
    {
        var motor = NewMotor();
        var locked = Ground(1) with { Locked = true };
        motor.Step(locked);
        motor.Step(Ground(0) with { Locked = true });
        motor.Step(locked);
        Assert.True(motor.HasQueuedAction);
        motor.Step(Ground(1));
        Assert.True(motor.IsSprinting);
    }

    private readonly struct PlayerMotorProbe(PlayerMotor motor)
    {
        public bool CanDashAgain()
        {
            motor.Step(Ground(dash: true));
            return motor.HasQueuedAction;
        }
    }
}
