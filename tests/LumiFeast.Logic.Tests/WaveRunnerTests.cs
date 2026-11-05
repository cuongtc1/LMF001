using LumiFeast.Logic.Combat;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class WaveRunnerTests
{
    [Fact]
    public void WaitsForTheStart()
    {
        Assert.Equal(WaveEvent.None, new WaveRunner(2, 0).Step(0));
    }

    [Fact]
    public void NextWaveWaitsForTheLastMonsterAndTheDelay()
    {
        var waves = new WaveRunner(2, 1);
        waves.Start();
        Assert.Equal(WaveEvent.SpawnWave, waves.Step(0));
        Assert.Equal(WaveEvent.None, waves.Step(3));
        Assert.Equal(WaveEvent.None, waves.Step(0));
        Assert.Equal(WaveEvent.SpawnWave, waves.Step(0));
        Assert.Equal(1, waves.Wave);
    }

    [Fact]
    public void ClearsAfterTheLastWave()
    {
        var waves = new WaveRunner(1, 0);
        waves.Start();
        waves.Step(0);
        Assert.Equal(WaveEvent.Cleared, waves.Step(0));
        Assert.True(waves.IsCleared);
        Assert.False(waves.IsRunning);
    }
}
