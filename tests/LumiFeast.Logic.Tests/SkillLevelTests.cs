using LumiFeast.Logic.Data;
using LumiFeast.Logic.Run;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class SkillLevelTests
{
    private static readonly SkillDef Wave = new("wave", "a", 20, 100, 0, [new SkillLevelDef(Repeats: 1), new SkillLevelDef(CooldownScale: 0.5f)]);

    [Fact]
    public void ASkillStartsAtLevelOneAndStopsAtItsTop()
    {
        var loadout = new SkillLoadout(4);
        loadout.Add(Wave);
        Assert.Equal(1, loadout.LevelOf("wave"));
        Assert.True(loadout.LevelUp("wave"));
        Assert.True(loadout.LevelUp("wave"));
        Assert.False(loadout.LevelUp("wave"));
        Assert.Equal(3, loadout.LevelOf("wave"));
        Assert.Empty(loadout.Upgradable);
    }

    [Fact]
    public void OnlyEquippedSkillsLevelUp()
    {
        var loadout = new SkillLoadout(4);
        Assert.False(loadout.LevelUp("wave"));
    }

    [Fact]
    public void LevelsChangeWhatTheSkillDoes()
    {
        var loadout = new SkillLoadout(4);
        loadout.Add(Wave);
        Assert.Null(loadout.Bonus(0));
        loadout.LevelUp("wave");
        Assert.Equal(1, loadout.Bonus(0)!.Repeats);
        loadout.LevelUp("wave");
        loadout.TryUse(0, 100, 1);
        Assert.Equal(50, loadout.CooldownLeft(0));
    }

    [Fact]
    public void EveryLumiSkillHasTwoVisibleUpgrades()
    {
        var set = TestData.Game.SkillSet("lumi_skills");
        Assert.All(set.Skills, skill =>
        {
            Assert.Equal(3, skill.MaxLevel);
            Assert.All(skill.Levels!, level => Assert.True(level.Repeats > 0 || level.DamageScale > 1 || level.CooldownScale < 1, skill.Id));
        });
        Assert.Equal(set.Slots, set.DefaultLoadout!.Count);
        Assert.All(set.DefaultLoadout, id => Assert.Contains(set.Skills, skill => skill.Id == id));
    }

    [Fact]
    public void ClearingTheRunResetsLevels()
    {
        var loadout = new SkillLoadout(4);
        loadout.Add(Wave);
        loadout.LevelUp("wave");
        loadout.Clear();
        loadout.Add(Wave);
        Assert.Equal(1, loadout.LevelOf("wave"));
    }
}
