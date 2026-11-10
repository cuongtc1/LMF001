using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Game.Gameplay.Player;
using LumiFeast.Logic.Common;

namespace LumiFeast.Game.Gameplay.World;

/// <summary>
/// A gold coin that pops out of a healed monster or a broken crate, spins, then flies to Lumi by itself
/// (Little Noah's gold is collected without walking over it). The gold is already in the wallet; the coin is the show.
/// </summary>
public partial class GoldCoin : Node2D
{
    private const float Gravity = 2600;
    private const float PopSeconds = 0.45f;
    private const float FlyAcceleration = 5200;
    private const float ArriveDistance = 40;
    private const float FrameSeconds = 0.07f;
    private const float TargetLift = 110;
    private static readonly int[] SpinOrder = [0, 1, 2, 1];

    private PlayerController _player = null!;
    private Sprite2D _sprite = null!;
    private List<Texture2D> _frames = [];
    private Vector2 _velocity;
    private float _age;
    private float _speed;
    private int _amount;

    /// <summary>Called with the coin's gold when it reaches Lumi.</summary>
    public Action<int>? Arrived { get; set; }

    /// <param name="frames">Spin frames: front, half turned, edge-on (one frame: no spin, a coin pile).</param>
    public void Setup(PlayerController player, IReadOnlyList<string> frames, float height, int amount, Vector2 position, Vector2 velocity)
    {
        _player = player;
        _frames = frames.Select(GameServices.Texture).ToList();
        _sprite = new Sprite2D { Texture = _frames[0], Scale = Vector2.One * height / _frames[0].GetHeight() };
        AddChild(_sprite);
        _amount = amount;
        _velocity = velocity;
        GlobalPosition = position;
        ZIndex = 6;
    }

    public override void _PhysicsProcess(double delta)
    {
        var step = (float)delta;
        _age += step;
        if (_frames.Count >= 3)
        {
            var tick = (int)(_age / FrameSeconds);
            _sprite.Texture = _frames[SpinOrder[tick % SpinOrder.Length]];
            _sprite.FlipH = tick / SpinOrder.Length % 2 == 1;
        }

        if (_age < PopSeconds)
        {
            _velocity.Y += Gravity * step;
            GlobalPosition += _velocity * step;
            return;
        }

        _speed += FlyAcceleration * step;
        var toLumi = _player.GlobalPosition + new Vector2(0, -TargetLift) - GlobalPosition;
        if (toLumi.Length() <= Mathf.Max(ArriveDistance, _speed * step))
        {
            AudioService.Play(SoundIds.Coin);
            Arrived?.Invoke(_amount);
            QueueFree();
            return;
        }

        GlobalPosition += toLumi.Normalized() * _speed * step;
    }
}
