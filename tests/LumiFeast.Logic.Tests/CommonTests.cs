using LumiFeast.Logic.Common;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class CommonTests
{
    [Theory]
    [InlineData(0f, 10f, 3f, 3f)]
    [InlineData(9f, 10f, 3f, 10f)]
    [InlineData(10f, 0f, 3f, 7f)]
    public void ApproachNeverOvershoots(float current, float target, float maxDelta, float expected) =>
        Assert.Equal(expected, MathUtils.Approach(current, target, maxDelta));

    [Fact]
    public void SameSeedGivesSameSequence()
    {
        var a = new SeededRandom(42);
        var b = new SeededRandom(42);
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(a.NextULong(), b.NextULong());
        }
    }

    [Fact]
    public void NextIntStaysInRange()
    {
        var random = new SeededRandom(7);
        for (var i = 0; i < 1000; i++)
        {
            Assert.InRange(random.NextInt(-3, 4), -3, 3);
        }
    }

    [Fact]
    public void EventBusDeliversUntilUnsubscribed()
    {
        var bus = new EventBus();
        var received = 0;
        void Handler(int value) => received += value;

        bus.Subscribe<int>(Handler);
        bus.Publish(2);
        bus.Unsubscribe<int>(Handler);
        bus.Publish(5);

        Assert.Equal(2, received);
    }
}
