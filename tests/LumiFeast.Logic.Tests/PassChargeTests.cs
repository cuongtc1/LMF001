using LumiFeast.Logic.Combat;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class PassChargeTests
{
    [Fact]
    public void TwoLandedZSwingsEarnOnePass()
    {
        var charge = new PassCharge(2);
        Assert.False(charge.TryUse());
        charge.Landed();
        Assert.False(charge.Ready);
        charge.Landed();
        Assert.True(charge.TryUse());
        Assert.False(charge.TryUse());
    }

    [Fact]
    public void MashingXNeverPassesTwiceInARow()
    {
        var charge = new PassCharge(2);
        charge.Landed();
        charge.Landed();
        charge.Landed();
        Assert.Equal(2, charge.Charges);
        Assert.True(charge.TryUse());
        Assert.False(charge.TryUse());
    }
}
