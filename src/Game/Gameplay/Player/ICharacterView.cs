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
    string AttackId,
    int SwingId,
    float AttackProgress,
    bool Guarding,
    bool CounterReady);

/// <summary>What the controller tells the visuals. The Spine view will implement this too.</summary>
public interface ICharacterView
{
    void Show(in ViewState state);
}
