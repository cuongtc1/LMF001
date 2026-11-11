using LumiFeast.Logic.Combat;
using LumiFeast.Logic.Data;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class ComboRunnerTests
{
    private static readonly RectDef Box = new(0, 0, 1, 1);
    private static readonly AttackSetDef Set = new("test", "a", "b", "a", "h", "b", "h", "h", [Attack("a", "b", "h"), Attack("b", null), Attack("h", null)]);

    private static AttackDef Attack(string id, string? next, string? heavy = null) =>
        new(id, 1, 2, 2, 4, 4, next, 0, Box, 0, 0, 0, "v", "v", 0, 0, 0, false, heavy, 4, 0, false, 0, 0, 0, 0, false);

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
    public void HeavyStartsAndBranchesFromTheCombo()
    {
        var combo = new ComboRunner(Set, 8);
        combo.PressAttack(heavy: true);
        combo.Advance();
        Assert.Equal("h", combo.Current?.Id);
        combo.Cancel();
        combo.PressAttack();
        combo.Advance();
        combo.PressAttack(heavy: true);
        for (var i = 0; i < 4; i++)
        {
            combo.Advance();
        }

        Assert.Equal("h", combo.Current?.Id);
    }

    [Fact]
    public void CancelCanComeBeforeTheChain()
    {
        var late = Attack("a", "b") with { CancelFromTick = 1, ChainFromTick = 6 };
        var combo = new ComboRunner(new AttackSetDef("t", "a", "a", "a", "a", "a", "a", "a", [late, Attack("b", null)]), 8);
        combo.PressAttack();
        combo.Advance();
        combo.PressAttack();
        combo.Advance();
        combo.Advance();
        Assert.True(combo.CanCancel);
        Assert.Equal("a", combo.Current?.Id);
    }

    [Fact]
    public void HeavyInTheAirUsesItsOwnAttackAndSpendsTheJump()
    {
        var combo = new ComboRunner(Set, 8);
        combo.PressAttack(heavy: true);
        combo.Advance(ComboEntry.Air);
        Assert.Equal("h", combo.Current?.Id);
        RunOut(combo, ComboEntry.Air);
        combo.PressAttack();
        combo.Advance(ComboEntry.Air);
        Assert.False(combo.IsAttacking);
    }

    [Fact]
    public void CounterEntryStartsTheCounter()
    {
        var combo = new ComboRunner(Set, 8);
        combo.PressAttack();
        combo.Advance(ComboEntry.Counter);
        Assert.Equal("h", combo.Current?.Id);
    }

    [Fact]
    public void DashZIsItsOwnOpenerAndZZXStillNeedsTwoSlashes()
    {
        // round 79 (producer): after the lunge stab the Z chain starts again at Z1, so Dash Z, Z Z X passes, never Dash Z Z X
        var set = TestData.Game.AttackSet("lumi_sword");
        var stab = set.Attacks.First(attack => attack.Id == set.FirstSprint);
        Assert.Equal(set.First, stab.Next);
        Assert.Null(stab.NextHeavy);
    }

    [Fact]
    public void XRisingAndXInADashPickTheirOwnPasses()
    {
        var set = Set with { FirstAirUpHeavy = "b", FirstSprintHeavy = "a" };
        var combo = new ComboRunner(set, 8);
        combo.PressAttack(heavy: true);
        combo.Advance(ComboEntry.AirUp);
        Assert.Equal("b", combo.Current?.Id);
        combo = new ComboRunner(set, 8);
        combo.PressAttack(heavy: true);
        combo.Advance(ComboEntry.Sprint);
        Assert.Equal("a", combo.Current?.Id);
    }

    [Fact]
    public void AnEmptyFirstHeavyMeansXAloneDoesNothing()
    {
        var combo = new ComboRunner(Set with { FirstHeavy = string.Empty }, 8);
        combo.PressAttack(heavy: true);
        combo.Advance();
        Assert.False(combo.IsAttacking);
    }

    [Fact]
    public void UpSlashSharesTheOneAirAttack()
    {
        var combo = new ComboRunner(Set, 8);
        combo.PressAttack();
        combo.Advance(ComboEntry.AirUp);
        Assert.Equal("a", combo.Current?.Id);
        RunOut(combo, ComboEntry.AirUp);
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
