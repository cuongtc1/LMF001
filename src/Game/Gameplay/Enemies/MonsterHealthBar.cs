using Godot;
using LumiFeast.Logic.Combat;

namespace LumiFeast.Game.Gameplay.Enemies;

/// <summary>
/// Small health bar under a monster's feet (Little Noah and Deus Exia both show one). Hidden while the monster is
/// unhurt, so a calm room stays clean; bosses use the big HUD bar instead.
/// </summary>
public partial class MonsterHealthBar : Node2D
{
    private const float Height = 9;
    private const float Border = 2;
    private static readonly Color Back = new(0.1f, 0.06f, 0.12f, 0.85f);
    private static readonly Color Fill = new("e2506a");
    private static readonly Color Low = new("ffb347");

    private Health _health = null!;
    private float _width;

    public void Setup(Health health, float width, float feetOffset)
    {
        _health = health;
        _width = width;
        Position = new Vector2(-width / 2, feetOffset);
        ZIndex = 5;
    }

    public void Refresh()
    {
        Visible = _health.Current > 0 && _health.Current < _health.Max;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var share = (float)_health.Current / _health.Max;
        DrawRect(new Rect2(-Border, -Border, _width + Border * 2, Height + Border * 2), Back);
        DrawRect(new Rect2(0, 0, _width * share, Height), share > 0.3f ? Fill : Low);
    }
}
