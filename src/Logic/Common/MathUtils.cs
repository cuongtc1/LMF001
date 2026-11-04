namespace LumiFeast.Logic.Common;

public static class MathUtils
{
    /// <summary>Moves current towards target by at most maxDelta, without overshooting.</summary>
    public static float Approach(float current, float target, float maxDelta)
    {
        if (Math.Abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + Math.Sign(target - current) * maxDelta;
    }
}
