using System.Collections.Generic;
using Godot;
using LumiFeast.Logic.Data;

namespace LumiFeast.Game.Gameplay.Combat;

/// <summary>What one hit does to its target. Damage is before the target's defense.</summary>
public readonly record struct HitInfo(int Attack, float Multiplier, int Direction, float KnockbackSpeed, float LaunchSpeed, string SourceId, int StunTicks = 0, bool Knockdown = false, float Stagger = 0);

public interface IDamageable
{
    /// <returns>True when the hit landed.</returns>
    bool TakeHit(in HitInfo hit);
}

/// <summary>Something Lumi cannot walk through.</summary>
public interface IBodyBlocker
{
    bool BlocksBody { get; }

    Vector2 FeetPosition { get; }

    Vector2 BodySize { get; }
}

/// <summary>The body area that receives hits.</summary>
public partial class Hurtbox : Area2D
{
    public IDamageable Owner_ { get; private set; } = null!;

    public void Setup(IDamageable owner, Vector2 size, uint layer)
    {
        Owner_ = owner;
        CollisionLayer = layer;
        CollisionMask = 0;
        Monitoring = false;
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = size }, Position = new Vector2(0, -size.Y / 2) });
    }
}

/// <summary>Attack area shared by Lumi and monsters. Each swing hits a target once.</summary>
public partial class AttackHitbox : Area2D
{
    private readonly HashSet<ulong> _alreadyHit = [];
    private CollisionShape2D _shape = null!;
    private int _swingId = -1;

    public void Setup(uint targetLayer)
    {
        CollisionLayer = 0;
        CollisionMask = targetLayer;
        _shape = new CollisionShape2D { Shape = new RectangleShape2D(), Disabled = true };
        AddChild(_shape);
    }

    public void Close() => _shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);

    /// <summary>Opens the box for this swing and returns the targets hit this tick.</summary>
    public List<IDamageable> Sweep(int swingId, RectDef box, int facing, in HitInfo hit)
    {
        if (swingId != _swingId)
        {
            _swingId = swingId;
            _alreadyHit.Clear();
        }

        ((RectangleShape2D)_shape.Shape).Size = new Vector2(box.Width, box.Height);
        _shape.Position = new Vector2((box.X + box.Width / 2) * facing, box.Y + box.Height / 2);
        _shape.Disabled = false;

        var landed = new List<IDamageable>();
        foreach (var area in GetOverlappingAreas())
        {
            if (area is Hurtbox target && _alreadyHit.Add(area.GetInstanceId()) && target.Owner_.TakeHit(hit))
            {
                landed.Add(target.Owner_);
            }
        }

        return landed;
    }
}
