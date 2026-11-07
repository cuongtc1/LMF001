namespace LumiFeast.Game.Common;

/// <summary>Facts about the code and scenes. Balance numbers live in data/, never here.</summary>
public static class GameConstants
{
    public const string ResourceRoot = "res://";
    public const string PrototypeScene = "res://scenes/levels/prototype/prototype_stage.tscn";
    public const string PrototypeStageId = "prototype_honey_forest";
    public const string RunScene = "res://scenes/levels/run/run_stage.tscn";
    public const string FirstRunId = "honey_labyrinth_stage_1";
    public const string RunScriptPrefix = "data/debug/run_";
    public const ulong ScriptedRunSeed = 1;
    public const string PlayerCharacterId = "lumi";
    public const string AutoplayArgument = "--autoplay=";
    public const float TicksPerSecond = 60f;
    public const int PlayerZIndex = 2;
}

/// <summary>Collision layer bits.</summary>
public static class PhysicsLayers
{
    public const uint World = 1;
    public const uint PlayerHurtbox = 2;
    public const uint EnemyHurtbox = 4;
    public const uint Platform = 8;
}

public static class InputActions
{
    public const string MoveLeft = "move_left";
    public const string MoveRight = "move_right";
    public const string MoveDown = "move_down";
    public const string Jump = "jump";
    public const string AttackLight = "attack_light";
    public const string AttackHeavy = "attack_heavy";
    public const string Dash = "dash";
    public const string Guard = "guard";
    public const string DebugPanel = "debug_panel";
    public const string DebugReload = "debug_reload";
}
