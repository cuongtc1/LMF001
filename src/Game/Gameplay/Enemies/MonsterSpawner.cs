using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Game.Gameplay.Combat;
using LumiFeast.Game.Gameplay.Player;
using LumiFeast.Logic.Stats;

namespace LumiFeast.Game.Gameplay.Enemies;

/// <summary>Creates a monster from its id, scaled by tier and difficulty.</summary>
public static class MonsterSpawner
{
    private const float FloorClearance = 4;

    public static MonsterController Spawn(Node parent, string id, float x, float groundY, int tier, GameFeel gameFeel, PlayerController player)
    {
        var data = GameServices.Data;
        var def = data.Monster(id);
        var stats = StatScaler.ForMonster(def, tier, data.Difficulty(GameServices.DifficultyId), data.Scaling);
        var monster = new MonsterController();
        parent.AddChild(monster);
        monster.Setup(def, stats, data.Feel, gameFeel, player, new Vector2(x, groundY - FloorClearance - def.Ai.HoverHeight));
        return monster;
    }
}
