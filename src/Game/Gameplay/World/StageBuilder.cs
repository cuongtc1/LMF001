using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Logic.Data;

namespace LumiFeast.Game.Gameplay.World;

/// <summary>Builds level pieces from data for any stage or room.</summary>
public static class StageBuilder
{
    public const float WallThickness = 200;
    private const float GroundThickness = 400;
    private const float PlatformThickness = 16;
    private const int PlatformZIndex = -5;
    private const int ParallaxRepeats = 6;
    /// <summary>The prototype far layers end in a strip of plain sky that the floor art normally hides; in tall rooms
    /// they are drawn a bit larger so that strip falls below the screen. Final far layers should be painted to the bottom.</summary>
    private const float TallFarLayerScale = 1.14f;

    /// <param name="roomHeight">In rooms taller than one screen, far layers (scroll below 1) stay on screen as the
    /// camera climbs, and play layers (scroll 1) sit at the bottom of the room, around the floor.</param>
    /// <returns>The layer; for tall-room far layers the caller feeds <see cref="Parallax2D.ScreenOffset"/> each frame.</returns>
    public static Parallax2D AddLayer(Node parent, LayerDef layer, float roomHeight = RoomDef.ScreenHeight)
    {
        var texture = GameServices.Texture(layer.Sprite);
        var tall = roomHeight > RoomDef.ScreenHeight;
        var far = layer.ScrollScale < 1;
        var scale = tall && far ? TallFarLayerScale : 1;
        var parallax = new Parallax2D
        {
            ScrollScale = new Vector2(layer.ScrollScale, tall && far ? 0 : 1),
            RepeatSize = new Vector2(texture.GetWidth() * scale, 0),
            RepeatTimes = ParallaxRepeats,
            ZIndex = layer.ZIndex,
            IgnoreCameraScroll = tall && far,
        };
        var top = tall && !far ? roomHeight - RoomDef.ScreenHeight : 0;
        parallax.AddChild(new Sprite2D { Texture = texture, Centered = false, Position = new Vector2(0, top), Scale = Vector2.One * scale });
        parent.AddChild(parallax);
        return parallax;
    }

    /// <param name="floor">False for sky rooms, whose open bottom leads to the room below.</param>
    public static void AddBounds(Node parent, float width, float groundY, bool floor = true)
    {
        var body = new StaticBody2D { CollisionLayer = PhysicsLayers.World, CollisionMask = 0 };
        if (floor)
        {
            var groundSize = new Vector2(width + WallThickness * 2, GroundThickness);
            AddSolid(body, new Vector2(width / 2, groundY + GroundThickness / 2), groundSize);
        }

        var wallSize = new Vector2(WallThickness, groundY * 2);
        AddSolid(body, new Vector2(-WallThickness / 2, 0), wallSize);
        AddSolid(body, new Vector2(width + WallThickness / 2, 0), wallSize);
        parent.AddChild(body);
    }

    public static void AddSolid(StaticBody2D body, Vector2 position, Vector2 size) =>
        body.AddChild(new CollisionShape2D { Position = position, Shape = new RectangleShape2D { Size = size } });

    public static void AddPlatform(Node parent, PlatformDef platform)
    {
        var texture = GameServices.Texture(platform.Sprite);
        var scale = platform.Width / texture.GetWidth();
        var height = texture.GetHeight() * scale;
        parent.AddChild(new Sprite2D
        {
            Texture = texture,
            Scale = Vector2.One * scale,
            Position = new Vector2(platform.X, platform.Y + height / 2 - platform.TopInset),
            ZIndex = PlatformZIndex,
        });

        var body = new StaticBody2D { CollisionLayer = PhysicsLayers.Platform, CollisionMask = 0 };
        body.AddChild(new CollisionShape2D
        {
            Position = new Vector2(platform.X, platform.Y + PlatformThickness / 2),
            Shape = new RectangleShape2D { Size = new Vector2(platform.Width - 20, PlatformThickness) },
            OneWayCollision = true,
        });
        parent.AddChild(body);
    }
}
