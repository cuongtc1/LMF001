using Godot;
using LumiFeast.Game.Gameplay.Player;
using LumiFeast.Logic.Data;

namespace LumiFeast.Game.Gameplay.World;

/// <summary>
/// A honey puddle a slime leaves where its hop lands (round 83): Lumi walking in it is slowed for a moment. It shrinks
/// away after its time runs out. Drawn as a flat ellipse, so it needs no art yet.
/// </summary>
public partial class StickyPuddle : Node2D
{
    private const float FeetSlack = 40;
    private const float EllipseHeight = 18;
    private const int FadeTicks = 30;

    private PlayerController _player = null!;
    private HazardDef _def = null!;
    private int _ticksLeft;

    public void Setup(PlayerController player, HazardDef def, Vector2 feet)
    {
        _player = player;
        _def = def;
        _ticksLeft = def.Ticks;
        GlobalPosition = feet;
        ZIndex = -2;
    }

    public override void _PhysicsProcess(double delta)
    {
        _ticksLeft--;
        if (_ticksLeft <= 0)
        {
            QueueFree();
            return;
        }

        Modulate = new Color(1, 1, 1, Mathf.Min(1, _ticksLeft / (float)FadeTicks));
        var feet = _player.GlobalPosition;
        if (Mathf.Abs(feet.X - GlobalPosition.X) < _def.Width / 2 && Mathf.Abs(feet.Y - GlobalPosition.Y) < FeetSlack)
        {
            _player.Slow(_def.Slow, _def.SlowTicks);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var color = new Color(_def.Color);
        DrawSetTransform(Vector2.Zero, 0, new Vector2(1, EllipseHeight / _def.Width));
        DrawCircle(Vector2.Zero, _def.Width / 2, color);
        DrawCircle(new Vector2(-_def.Width * 0.12f, -_def.Width * 0.08f), _def.Width * 0.18f, color.Lightened(0.35f));
    }
}
