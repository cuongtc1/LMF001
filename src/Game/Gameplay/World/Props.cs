using System;
using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Game.Gameplay.Combat;
using LumiFeast.Game.Gameplay.Player;
using LumiFeast.Logic.Data;
using LumiFeast.Logic.Run;

namespace LumiFeast.Game.Gameplay.World;

/// <summary>Something Lumi breaks or opens by hitting it.</summary>
public partial class HittableProp : Node2D, IDamageable
{
    private const int FlashTicks = 6;
    private const float FlashStrength = 1.8f;

    private Sprite2D _sprite = null!;
    private Hurtbox _hurtbox = null!;
    private int _hitsLeft;
    private int _flash;

    public event Action<HittableProp>? Broken;

    public void Setup(string sprite, float height, Vector2 feet, int hits)
    {
        var texture = GameServices.Texture(sprite);
        _sprite = new Sprite2D { Texture = texture, Scale = Vector2.One * height / texture.GetHeight(), Position = new Vector2(0, -height / 2) };
        AddChild(_sprite);
        _hurtbox = new Hurtbox();
        AddChild(_hurtbox);
        _hurtbox.Setup(this, new Vector2(texture.GetWidth() * _sprite.Scale.X, height), PhysicsLayers.EnemyHurtbox);
        GlobalPosition = feet;
        _hitsLeft = hits;
        ZIndex = -3;
    }

    /// <summary>Swaps the drawing at the same scale and keeps it standing on the floor.</summary>
    public void ChangeSprite(string sprite)
    {
        _sprite.Texture = GameServices.Texture(sprite);
        _sprite.Position = new Vector2(0, -_sprite.Texture.GetHeight() * _sprite.Scale.Y / 2);
    }

    public void FadeAway(float seconds)
    {
        var tween = CreateTween();
        tween.TweenInterval(seconds / 2);
        tween.TweenProperty(this, CanvasItem.PropertyName.Modulate.ToString(), Colors.Transparent, seconds / 2);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    public bool TakeHit(in HitInfo hit)
    {
        if (_hitsLeft <= 0)
        {
            return false;
        }

        _hitsLeft--;
        _flash = FlashTicks;
        if (_hitsLeft == 0)
        {
            _hurtbox.SetDeferred(Area2D.PropertyName.Monitorable, false);
            Broken?.Invoke(this);
        }

        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        _flash = Mathf.Max(0, _flash - 1);
        _sprite.Modulate = _flash > 0 ? Colors.White * FlashStrength : Colors.White;
    }
}

/// <summary>An HP or MP token. Lumi takes it by touch, but only when it would help.</summary>
public partial class Pickup : Node2D
{
    private const float TakeDistance = 90;
    private const float BobPixels = 8;
    private const float BobSpeed = 4;
    private const float Height = 64;

    private PlayerController _player = null!;
    private PickupDef _def = null!;
    private bool _hp;
    private float _age;

    public void Setup(PlayerController player, PickupDef def, bool hp, Vector2 position)
    {
        _player = player;
        _def = def;
        _hp = hp;
        var texture = GameServices.Texture(hp ? def.HpTokenSprite : def.MpTokenSprite);
        AddChild(new Sprite2D { Texture = texture, Scale = Vector2.One * Height / texture.GetHeight() });
        GlobalPosition = position;
        ZIndex = 5;
    }

    public override void _PhysicsProcess(double delta)
    {
        _age += (float)delta;
        GetChild<Sprite2D>(0).Position = new Vector2(0, Mathf.Sin(_age * BobSpeed) * BobPixels);
        if (_player.GlobalPosition.DistanceTo(GlobalPosition + new Vector2(0, Height)) > TakeDistance + Height)
        {
            return;
        }

        var health = _player.Health;
        if (_hp && PickupRules.Helps(health.Current, health.Max))
        {
            health.Heal(Mathf.RoundToInt(health.Max * _def.HpHeal * _player.Modifiers.Scale(AmuletEffects.TokenHeal)));
            QueueFree();
        }
        else if (!_hp && PickupRules.Helps(_player.Mp, _player.MaxMp))
        {
            _player.GainMp(_def.MpGain);
            QueueFree();
        }
    }
}
