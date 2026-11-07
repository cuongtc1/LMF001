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
    private bool _pierce;
    private bool _spin;
    private int _lifetime;

    public System.Action<IDamageable>? Landed { get; set; }

    /// <param name="lifetimeTicks">0 means it lives until it lands.</param>
    public void Launch(string sprite, float height, Vector2 from, Vector2 velocity, float gravity, float groundY, in HitInfo hit, uint targetLayer, bool pierce = false, int lifetimeTicks = 0)
    {
        var texture = GameServices.Texture(sprite);
        AddChild(new Sprite2D { Texture = texture, Scale = new Vector2(Mathf.Sign(velocity.X == 0 ? 1 : velocity.X), 1) * height / texture.GetHeight() });
        _hitbox = new AttackHitbox();
        AddChild(_hitbox);
        _hitbox.Setup(targetLayer);
        _pierce = pierce;
        _lifetime = lifetimeTicks;
        _spin = gravity > 0;
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
        if (_spin)
        {
            Rotation += SpinPerSecond * seconds;
        }

        var box = new Logic.Data.RectDef(-HitboxSize / 2, -HitboxSize / 2, HitboxSize, HitboxSize);
        var landed = _hitbox.Sweep(Swing, box, 1, _hit);
        foreach (var target in landed)
        {
            Landed?.Invoke(target);
        }

        _lifetime--;
        if ((landed.Count > 0 && !_pierce) || GlobalPosition.Y >= _groundY || _lifetime == 0)
        {
            QueueFree();
        }
    }
}
