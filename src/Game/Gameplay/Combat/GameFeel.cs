using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;

namespace LumiFeast.Game.Gameplay.Combat;

/// <summary>Hitstop, screen shake, effects and damage numbers for one stage.</summary>
public partial class GameFeel : Node2D
{
    private readonly SeededRandom _random = new((ulong)System.DateTime.Now.Ticks);
    private GameFeelDef _feel = null!;
    private Camera2D _camera = null!;
    private int _hitstop;
    private float _shake;

    public bool Frozen => _hitstop > 0;

    public void Setup(GameFeelDef feel, Camera2D camera)
    {
        _feel = feel;
        _camera = camera;
        ProcessPhysicsPriority = -100;
    }

    public void Hitstop(int ticks) => _hitstop = Mathf.Max(_hitstop, ticks);

    public void Shake(float pixels) => _shake = Mathf.Max(_shake, pixels);

    public void PlayEffect(string id, Vector2 position, int facing)
    {
        var effect = new VfxPlayer();
        AddChild(effect);
        effect.Play(id, position, facing);
    }

    public void ShowDamage(Vector2 position, int amount, Color color)
    {
        var label = new Label { Text = amount.ToString(), Position = position, ZIndex = 20 };
        label.AddThemeFontSizeOverride("font_size", 38);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 8);
        AddChild(label);
        var seconds = _feel.DamageNumberTicks / GameConstants.TicksPerSecond;
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(label, "position:y", position.Y - _feel.DamageNumberRise, seconds).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(label, "modulate:a", 0f, seconds).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(label.QueueFree));
    }

    public override void _PhysicsProcess(double delta)
    {
        _hitstop = Mathf.Max(0, _hitstop - 1);
        _shake = Mathf.MoveToward(_shake, 0, _feel.ShakeDecay * (float)delta);
        _camera.Offset = _shake > 0
            ? new Vector2((float)(_random.NextDouble() - 0.5) * 2 * _shake, (float)(_random.NextDouble() - 0.5) * 2 * _shake)
            : Vector2.Zero;
    }
}
