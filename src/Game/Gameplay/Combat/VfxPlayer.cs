using System.Linq;
using Godot;
using LumiFeast.Game.Common;

namespace LumiFeast.Game.Gameplay.Combat;

/// <summary>Plays a frame-by-frame effect from assets/vfx once, then removes itself.</summary>
public partial class VfxPlayer : Sprite2D
{
    private Texture2D[] _frames = [];
    private int _ticksPerFrame;
    private int _tick;

    public void Play(string id, Vector2 position, int facing)
    {
        var vfx = GameServices.Data.Vfx;
        var effect = vfx.Effects.First(item => item.Id == id);
        _frames = Enumerable.Range(0, effect.Frames)
            .Select(frame => GameServices.Texture($"{vfx.Folder}/{id}/frame_{frame}.png"))
            .ToArray();
        _ticksPerFrame = effect.TicksPerFrame;
        Texture = _frames[0];
        GlobalPosition = position;
        Scale = new Vector2(effect.Scale * facing, effect.Scale);
        ZIndex = 15;
    }

    public override void _PhysicsProcess(double delta)
    {
        _tick++;
        var frame = _tick / _ticksPerFrame;
        if (frame >= _frames.Length)
        {
            QueueFree();
            return;
        }

        Texture = _frames[frame];
    }
}
