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
    public void EffectsWithAnHpMinimumSwitchOffBelowIt()
    {
        var mods = new RunModifiers();
        mods.Add(new AmuletDef("s", [new AmuletEffectDef(AmuletEffects.Speed, 0.12f, MinHpRatio: 0.7f)]));
        Assert.Equal(0.12f, mods.Total(AmuletEffects.Speed, 0.9f), 3);
        Assert.Equal(0f, mods.Total(AmuletEffects.Speed, 0.5f));
    }

    [Fact]
    public void TokensStayWhenTheyWouldNotHelp()
    {
        Assert.False(PickupRules.Helps(300, 300));
        Assert.True(PickupRules.Helps(299, 300));
    }

    [Fact]
    public void RewardDrawGivesDistinctChoices()
    {
        var picks = RewardDraw.Pick(["a", "b", "c", "d"], 3, new SeededRandom(5));
        Assert.Equal(3, picks.Distinct().Count());
        Assert.Equal(2, RewardDraw.Pick(["a", "b"], 3, new SeededRandom(5)).Count);
    }

    [Fact]
    public void TargetedEffectsOnlyCountForTheirAttack()
    {
        var mods = new RunModifiers();
        mods.Add(new AmuletDef("sticky", [
            new AmuletEffectDef(AmuletEffects.AttackSlow, 0.3f, Target: "z3", Ticks: 180),
            new AmuletEffectDef(AmuletEffects.AttackSlow, 0.3f, Target: "xx", Ticks: 120)]));
        Assert.Equal(0.3f, mods.TotalFor(AmuletEffects.AttackSlow, "z3"), 3);
        Assert.Equal(0f, mods.TotalFor(AmuletEffects.AttackSlow, "z1"));
        Assert.Equal(180, mods.Ticks(AmuletEffects.AttackSlow, "z3"));
        Assert.Equal(180, mods.Ticks(AmuletEffects.AttackSlow));
    }

    [Fact]
    public void RoomUsesRunOutAndResetPerRoom()
    {
        var uses = new RoomUses();
        Assert.True(uses.TryUse(2));
        Assert.True(uses.TryUse(2));
        Assert.False(uses.TryUse(2));
        Assert.Equal(1, uses.Left(3));
        uses.Reset();
        Assert.Equal(2, uses.Left(2));
    }
}
