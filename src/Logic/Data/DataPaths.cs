namespace LumiFeast.Logic.Data;

/// <summary>Data file locations, relative to the project root.</summary>
public static class DataPaths
{
    public const string Messages = "data/messages/messages.properties";
    public const string Difficulty = "data/balance/difficulty.json";
    public const string Scaling = "data/balance/scaling.json";
    public const string GameFeel = "data/balance/game_feel.json";
    public const string Input = "data/input/default_bindings.json";
    public const string CommonVfx = "data/vfx/common.json";
    public const string Characters = "data/characters";
    public const string Attacks = "data/attacks";
    public const string Monsters = "data/monsters";
    public const string Stages = "data/stages";
    public const string Runs = "data/runs";
    public const string JsonExtension = ".json";

    /// <summary>Files in the characters folder that are not character definitions.</summary>
    public const string SkinFileSuffix = "_skins.json";
}
