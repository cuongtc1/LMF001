namespace LumiFeast.Logic.Combat;

public static class DamageCalculator
{
    /// <param name="defense">Share of damage removed, 0 to 1.</param>
    public static int Compute(int attack, float multiplier, float defense) =>
        Math.Max(1, (int)MathF.Round(attack * multiplier * (1 - defense)));
}
