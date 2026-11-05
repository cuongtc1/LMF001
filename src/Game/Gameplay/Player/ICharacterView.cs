namespace LumiFeast.Game.Gameplay.Player;

public readonly record struct ViewState(
    int Facing,
    float VelocityX,
    float VelocityY,
    bool OnFloor,
    bool Dashing,
    bool Blinking,
    bool Hurt,
    bool Attacking,
    float SwingDegrees);

/// <summary>What the controller tells the visuals. The Spine view will implement this too.</summary>
public interface ICharacterView
{
    void Show(in ViewState state);
}
