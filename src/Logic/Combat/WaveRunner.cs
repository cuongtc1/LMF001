namespace LumiFeast.Logic.Combat;

public enum WaveEvent
{
    None,
    SpawnWave,
    Cleared,
}

/// <summary>Runs a closed fight: the next wave comes a short delay after the last monster is gone.</summary>
public sealed class WaveRunner(int waveCount, int delayTicks)
{
    private int _delay;

    public int Wave { get; private set; } = -1;

    public bool IsRunning { get; private set; }

    public bool IsCleared { get; private set; }

    public void Start()
    {
        Wave = -1;
        _delay = 0;
        IsRunning = true;
        IsCleared = false;
    }

    public void Reset()
    {
        Wave = -1;
        IsRunning = false;
        IsCleared = false;
    }

    public WaveEvent Step(int monstersLeft)
    {
        if (!IsRunning || monstersLeft > 0)
        {
            return WaveEvent.None;
        }

        if (_delay > 0)
        {
            _delay--;
            return WaveEvent.None;
        }

        if (Wave + 1 >= waveCount)
        {
            IsRunning = false;
            IsCleared = true;
            return WaveEvent.Cleared;
        }

        Wave++;
        _delay = delayTicks;
        return WaveEvent.SpawnWave;
    }
}
