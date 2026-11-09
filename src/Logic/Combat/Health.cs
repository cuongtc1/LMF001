namespace LumiFeast.Logic.Combat;

public sealed class Health(int max)
{
    public int Max { get; private set; } = max;

    public int Current { get; private set; } = max;

    public bool IsDead => Current <= 0;

    public void Damage(int amount) => Current = Math.Max(0, Current - amount);

    public void Heal(int amount) => Current = Math.Min(Max, Current + amount);

    public void Refill() => Current = Max;

    /// <summary>New max (a level change); the current share of health stays the same.</summary>
    public void Resize(int max)
    {
        Current = Math.Max(Current > 0 ? 1 : 0, (int)MathF.Round((float)Current / Max * max));
        Max = max;
    }
}
