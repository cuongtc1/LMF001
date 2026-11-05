using LumiFeast.Logic.Combat;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class BodyBlockTests
{
    [Theory]
    [InlineData(0f, 10f, 100f, 30f, 0f)]
    [InlineData(80f, 10f, 100f, 30f, -20f)]
    [InlineData(120f, 10f, 100f, 30f, 20f)]
    public void PushesOutOnTheNearSide(float x, float half, float otherX, float otherHalf, float expected) =>
        Assert.Equal(expected, BodyBlock.PushOut(x, half, otherX, otherHalf));
}
