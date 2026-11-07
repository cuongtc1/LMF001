using Godot;
using LumiFeast.Game.Common;

namespace LumiFeast.Game.Gameplay.Player;

public readonly record struct PlayerFrameInput(float MoveX, bool JumpPressed, bool JumpHeld, bool DashPressed, bool AttackPressed, bool DownHeld, bool HeavyPressed, bool DownPressed, bool GuardHeld, int SkillPressed = -1)
{
    /// <summary>No button held. Use this, not <c>default</c>, which would read as skill slot 0.</summary>
    public static readonly PlayerFrameInput None = new(0, false, false, false, false, false, false, false, false);
}

/// <summary>Where the player's buttons come from: the device, or a script in smoke tests.</summary>
public interface IPlayerInput
{
    PlayerFrameInput Read();
}

public sealed class DeviceInput : IPlayerInput
{
    public PlayerFrameInput Read() => new(
        Input.GetAxis(InputActions.MoveLeft, InputActions.MoveRight),
        Input.IsActionJustPressed(InputActions.Jump),
        Input.IsActionPressed(InputActions.Jump),
        Input.IsActionJustPressed(InputActions.Dash),
        Input.IsActionJustPressed(InputActions.AttackLight),
        Input.IsActionPressed(InputActions.MoveDown),
        Input.IsActionJustPressed(InputActions.AttackHeavy),
        Input.IsActionJustPressed(InputActions.MoveDown),
        Input.IsActionPressed(InputActions.Guard),
        System.Array.FindIndex(InputActions.Skills, action => Input.IsActionJustPressed(action)));
}
