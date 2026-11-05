using LumiFeast.Logic.Combat;
using LumiFeast.Logic.Data;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class ComboRunnerTests
{
    private static readonly RectDef Box = new(0, 0, 1, 1);
    private static readonly AttackSetDef Set = new("test", "a", "b", "b", [Attack("a", "b"), Attack("b", null)]);

    private static AttackDef Attack(string id, string? next) =>
        new(id, 1, 2, 2, 4, 6, next, 0, Box, 0, 0, 0, "v", "v", 0, 0, 0, 0, 0, false);

    private static void RunOut(ComboRunner combo, ComboEntry entry)
    {
        for (var tick = 0; tick < 40; tick++)
        {
            combo.Advance(entry);
        }
    }

    [Fact]
    public void OnlyOneAirStringPerJump()
    {
        var combo = new ComboRunner(Set, 8);
        combo.PressAttack();
        combo.Advance(ComboEntry.Air);
        Assert.True(combo.IsAttacking);
        RunOut(combo, ComboEntry.Air);
        combo.PressAttack();
        combo.Advance(ComboEntry.Air);
        Assert.False(combo.IsAttacking);
    }

    [Fact]
    public void LandingGivesTheAirStringBack()
    {
        var combo = new ComboRunner(Set, 8);
        combo.PressAttack();
        combo.Advance(ComboEntry.Air);
        RunOut(combo, ComboEntry.Air);
        combo.Advance(ComboEntry.Ground);
        combo.PressAttack();
        combo.Advance(ComboEntry.Air);
        Assert.True(combo.IsAttacking);
    }

    private static ComboRunner Run(params int[] pressTicks)
    {
        var combo = new ComboRunner(Set, 8);
        for (var tick = 0; tick < 40; tick++)
        {
            if (pressTicks.Contains(tick))
            {
                combo.PressAttack();
            }

            combo.Advance();
        }

        return combo;
    }

    [Fact]
    public void PhasesFollowTheAttackTiming()
    {
        var combo = new ComboRunner(Set, 8);
        combo.PressAttack();
        combo.Advance();
        Assert.True(combo.JustStarted);
        Assert.Equal(AttackPhase.Startup, combo.Phase);
        combo.Advance();
        combo.Advance();
        Assert.Equal(AttackPhase.Active, combo.Phase);
        combo.Advance();
        combo.Advance();
        Assert.Equal(AttackPhase.Recovery, combo.Phase);
    }

    [Fact]
    public void PressDuringAttackChainsToNext()
    {
        var combo = new ComboRunner(Set, 8);
        combo.PressAttack();
        combo.Advance();
        combo.PressAttack();
        for (var i = 0; i < 4; i++)
        {
            combo.Advance();
        }

        Assert.Equal("b", combo.Current!.Id);
        Assert.Equal(2, combo.SwingId);
    }

    [Theory]
    [InlineData(ComboEntry.Ground, "a")]
    [InlineData(ComboEntry.Air, "b")]
    [InlineData(ComboEntry.Sprint, "b")]
    public void EntryPicksTheFirstAttack(ComboEntry entry, string expected)
    {
        var combo = new ComboRunner(Set, 8);
        combo.PressAttack();
        combo.Advance(entry);
        Assert.Equal(expected, combo.Current!.Id);
    }

    [Theory]
    [InlineData(1f, 8)]
    [InlineData(2f, 4)]
    public void AttackSpeedShortensTheAttack(float speed, int expectedTicks)
    {
        var combo = new ComboRunner(Set, 8) { AttackSpeed = speed };
        combo.PressAttack();
        combo.Advance();
        var ticks = 0;
        while (combo.IsAttacking)
        {
            combo.Advance();
            ticks++;
        }

        Assert.Equal(expectedTicks, ticks);
    }

    [Fact]
    public void NoPressEndsTheCombo() => Assert.False(Run(0).IsAttacking);

    [Fact]
    public void LastAttackDoesNotLoop()
    {
        var combo = new ComboRunner(Set, 8);
        combo.PressAttack();
        combo.Advance();
        combo.PressAttack();
        for (var i = 0; i < 12; i++)
        {
            combo.Advance();
        }

        Assert.Equal(2, combo.SwingId);
        Assert.False(combo.IsAttacking);
    }

    [Fact]
    public void CancelOnlyOpensAtTheCancelTick()
    {
        var combo = new ComboRunner(Set, 8);
        combo.PressAttack();
        combo.Advance();
        Assert.False(combo.CanCancel);
        for (var i = 0; i < 6; i++)
        {
            combo.Advance();
        }

        Assert.True(combo.CanCancel);
    }
}
