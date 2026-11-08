using Godot;
using LumiFeast.Game.Common;

namespace LumiFeast.Game.Gameplay.Combat;

/// <summary>
/// A thrown or flying object. It breaks on the ground, on its first hit (unless it pierces) or when its time runs out.
/// Options: hit again every few ticks, pull monsters toward itself, and burst in a wider area when it ends.
/// </summary>
public partial class Projectile : Node2D
{
    private const float HitboxSize = 90;
    private const float SpinPerSecond = 9;
    private const int BurstTicks = 3;

    private AttackHitbox _hitbox = null!;
    private HitInfo _hit;
    private Vector2 _velocity;
    private float _gravity;
    private float _groundY;
    private bool _pierce;
    private bool _spin;
    private int _lifetime;
    private int _age;
    private int _burstLeft;

    public System.Action<IDamageable>? Landed { get; set; }

    /// <summary>Called once where the projectile bursts (for effects and sound).</summary>
    public System.Action<Vector2>? Burst { get; set; }

    /// <summary>Above 0: the same targets can be hit again every this many ticks.</summary>
    public int RehitTicks { get; set; }

    /// <summary>Hits pull monsters toward the projectile instead of pushing them away.</summary>
    public bool Pull { get; set; }

    /// <summary>Square hit area; 0 keeps the default.</summary>
    public float HitSize { get; set; }

    /// <summary>Above 0: when the projectile ends it hits everything in a square this wide.</summary>
    public float BurstSize { get; set; }

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
        if (_burstLeft > 0)
        {
            // the burst box stays a few ticks so the physics overlap catches every target
            var area = new Logic.Data.RectDef(-BurstSize / 2, -BurstSize / 2, BurstSize, BurstSize);
            foreach (var target in _hitbox.Sweep(int.MaxValue, area, 1, _hit))
            {
                Landed?.Invoke(target);
            }

            if (--_burstLeft == 0)
            {
                QueueFree();
            }

            return;
        }

        var seconds = (float)delta;
        _velocity.Y += _gravity * seconds;
        GlobalPosition += _velocity * seconds;
        if (_spin)
        {
            Rotation += SpinPerSecond * seconds;
        }

        var size = HitSize > 0 ? HitSize : HitboxSize;
        var box = new Logic.Data.RectDef(-size / 2, -size / 2, size, size);
        var swing = RehitTicks > 0 ? _age / RehitTicks : 0;
        var hit = Pull ? _hit with { PullToX = GlobalPosition.X } : _hit;
        var landed = _hitbox.Sweep(swing, box, 1, hit);
        _age++;
        foreach (var target in landed)
        {
            Landed?.Invoke(target);
        }

        _lifetime--;
        if ((landed.Count > 0 && !_pierce) || GlobalPosition.Y >= _groundY || _lifetime == 0)
        {
            End();
        }
    }

    private void End()
    {
        if (BurstSize <= 0)
        {
            QueueFree();
            return;
        }

        _burstLeft = BurstTicks;
        Burst?.Invoke(GlobalPosition);
        foreach (var child in GetChildren())
        {
            if (child is Sprite2D sprite)
            {
                sprite.Visible = false;
            }
        }
    }
}
