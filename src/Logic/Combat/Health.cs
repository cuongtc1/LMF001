namespace LumiFeast.Logic.Combat;

public sealed class Health(int max)
{
    public int Max { get; } = max;

    public int Current { get; private set; } = max;

    public bool IsDead => Current <= 0;

    public void Damage(int amount) => Current = Math.Max(0, Current - amount);

    public void Refill() => Current = Max;
}
