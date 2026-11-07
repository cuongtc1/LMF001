using Godot;
using LumiFeast.Game.Common;

namespace LumiFeast.Game.Gameplay.Combat;

/// <summary>A thrown object on an arc. It breaks on the ground or on its first hit.</summary>
public partial class Projectile : Node2D
{
    private const float HitboxSize = 90;
    private const float SpinPerSecond = 9;
    private const int Swing = 1;

    private AttackHitbox _hitbox = null!;
    private HitInfo _hit;
    private Vector2 _velocity;
    private float _gravity;
    private float _groundY;

    public void Launch(string sprite, float height, Vector2 from, Vector2 velocity, float gravity, float groundY, in HitInfo hit)
    {
        var texture = GameServices.Texture(sprite);
        AddChild(new Sprite2D { Texture = texture, Scale = Vector2.One * height / texture.GetHeight() });
        _hitbox = new AttackHitbox();
        AddChild(_hitbox);
        _hitbox.Setup(PhysicsLayers.PlayerHurtbox);
        GlobalPosition = from;
        _velocity = velocity;
        _gravity = gravity;
        _groundY = groundY;
        _hit = hit;
        ZIndex = 10;
    }

    public override void _PhysicsProcess(double delta)
    {
        var seconds = (float)delta;
        _velocity.Y += _gravity * seconds;
        GlobalPosition += _velocity * seconds;
        Rotation += SpinPerSecond * seconds;
        var box = new Logic.Data.RectDef(-HitboxSize / 2, -HitboxSize / 2, HitboxSize, HitboxSize);
        if (_hitbox.Sweep(Swing, box, 1, _hit).Count > 0 || GlobalPosition.Y >= _groundY)
        {
            QueueFree();
        }
    }
}
