using Godot;

namespace LumiFeast.Game.Gameplay.Enemies;

/// <summary>
/// A column of purple light where a monster is about to appear (Little Noah's lair foes rise out of one, 21:30).
/// It shows the player where the next monster comes from before it can act, then fades.
/// </summary>
public partial class SpawnPortal : Node2D
{
    private const float Width = 150;
    private const float Height = 330;
    private const int Bands = 6;
    private static readonly Color Glow = new(0.75f, 0.35f, 1f);

    private int _ticks;
    private int _total;

    public void Setup(int ticks)
    {
        _ticks = ticks;
        _total = ticks;
        ZIndex = 4;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (--_ticks <= 0)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        // grows quickly, then fades as the monster takes shape
        var progress = 1 - (float)_ticks / _total;
        var strength = Mathf.Min(1, progress * 4) * (1 - progress * 0.7f);
        for (var i = 0; i < Bands; i++)
        {
            var share = 1 - (float)i / Bands;
            var color = new Color(Glow, 0.14f * strength);
            DrawRect(new Rect2(-Width * share / 2, -Height, Width * share, Height), color);
        }

        DrawSetTransform(new Vector2(0, -6), 0, new Vector2(1, 0.25f));
        DrawCircle(Vector2.Zero, Width * 0.6f, new Color(Glow, 0.5f * strength));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}
