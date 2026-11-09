using Godot;

namespace LumiFeast.Game.Common;

/// <summary>Icons of skills and amulets: <c>{icon_folder}/{id}.png</c>, folders set in the skill and amulet data.</summary>
public static class GameIcons
{
    public static Texture2D Skill(string skillSetId, string skillId) =>
        GameServices.Texture($"{GameServices.Data.SkillSet(skillSetId).IconFolder}/{skillId}.png");

    public static Texture2D Amulet(string amuletSetId, string amuletId) =>
        GameServices.Texture($"{GameServices.Data.AmuletSet(amuletSetId).IconFolder}/{amuletId}.png");
}
