namespace LumiFeast.Logic.Combat;

/// <summary>Bodies do not overlap sideways. A push instead of solid collision, so nobody can stand on a monster's head.</summary>
public static class BodyBlock
{
    /// <returns>How far to move x so the two bodies only touch; 0 when they are apart.</returns>
    public static float PushOut(float x, float halfWidth, float otherX, float otherHalfWidth)
    {
        var overlap = halfWidth + otherHalfWidth - MathF.Abs(x - otherX);
        if (overlap <= 0)
        {
            return 0;
        }

        return x < otherX ? -overlap : overlap;
    }
}
