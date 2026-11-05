using Godot;
using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;

namespace LumiFeast.Game.Common;

/// <summary>The loaded game data and the session settings changed from the debug panel.</summary>
public static class GameServices
{
    private static readonly GodotDataSource Source = new();

    public static MessageCatalog Messages { get; private set; } = MessageCatalog.Parse(string.Empty);

    public static GameData Data { get; private set; } = null!;

    public static string DifficultyId { get; set; } = string.Empty;

    public static int? TierOverride { get; set; }

    public static bool GodMode { get; set; }

    public static void Load()
    {
        Messages = MessageCatalog.Parse(Source.ReadText(DataPaths.Messages));
        Data = new GameDataLoader(Source).Load();
        if (DifficultyId.Length == 0)
        {
            DifficultyId = Data.Difficulties.Default;
        }

        InputSetup.Apply(Data.Input);
    }

    public static Texture2D Texture(string path)
    {
        var fullPath = GameConstants.ResourceRoot + path;
        if (ResourceLoader.Exists(fullPath))
        {
            return GD.Load<Texture2D>(fullPath);
        }

        GameLog.Error(MessageKeys.AssetMissing, path);
        return new PlaceholderTexture2D { Size = Vector2.One * 64 };
    }
}
