using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;
using LumiFeast.Logic.Run;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class RunLoadoutTests
{
    private static readonly SkillDef Skill = new("a", "atk", 30, 100, 10);

    [Fact]
    public void SkillNeedsMpAndThenCoolsDown()
    {
        var loadout = new SkillLoadout(2);
        loadout.Add(Skill);
        Assert.Null(loadout.TryUse(0, 10, 1));
        Assert.Equal(Skill, loadout.TryUse(0, 50, 1));
        Assert.Null(loadout.TryUse(0, 50, 1));
        for (var i = 0; i < 100; i++)
        {
            loadout.Step();
        }

        Assert.NotNull(loadout.TryUse(0, 50, 1));
    }

    [Fact]
    public void CooldownScaleShortensTheWait()
    {
        var loadout = new SkillLoadout(1);
        loadout.Add(Skill);
        loadout.TryUse(0, 50, 0.75f);
        Assert.Equal(75, loadout.CooldownLeft(0));
    }

    [Fact]
    public void SlotsFillInOrderWithoutDuplicates()
    {
        var loadout = new SkillLoadout(2);
        Assert.True(loadout.Add(Skill));
        Assert.False(loadout.Add(Skill));
        Assert.True(loadout.Add(Skill with { Id = "b" }));
        Assert.True(loadout.IsFull);
        Assert.False(loadout.Add(Skill with { Id = "c" }));
    }

    [Fact]
    public void AmuletsAddUp()
    {
        var mods = new RunModifiers();
        mods.Add(new AmuletDef("x", [new AmuletEffectDef(AmuletEffects.DamageTaken, -0.15f)]));
        mods.Add(new AmuletDef("y", [new AmuletEffectDef(AmuletEffects.DamageTaken, -0.1f), new AmuletEffectDef(AmuletEffects.AttackProjectile, 0, "z3", "wave")]));
        Assert.Equal(0.75f, mods.Scale(AmuletEffects.DamageTaken), 3);
        Assert.Equal(1f, mods.Scale(AmuletEffects.Speed));
        Assert.Equal(["wave"], mods.ProjectilesFor("z3"));
        Assert.Empty(mods.ProjectilesFor("z1"));
    }

    [Fact]
    public void RewardDrawGivesDistinctChoices()
    {
        var picks = RewardDraw.Pick(["a", "b", "c", "d"], 3, new SeededRandom(5));
        Assert.Equal(3, picks.Distinct().Count());
        Assert.Equal(2, RewardDraw.Pick(["a", "b"], 3, new SeededRandom(5)).Count);
    }
}
