namespace LumiFeast.Logic.Combat;

/// <summary>
/// How far a pass slash carries Lumi (combo kit v2, "X goes through"): she lands just behind the nearest monster in
/// front of her, so a small slime is a short hop and a treant a long slash. With nobody in reach she passes a fixed
/// distance.
/// </summary>
public static class PassRule
{
    /// <param name="lumiX">Lumi's center.</param>
    /// <param name="facing">1 right, -1 left.</param>
    /// <param name="lumiHalfWidth">Half of Lumi's body width.</param>
    /// <param name="targets">Center and half width of each monster at Lumi's height.</param>
    /// <param name="reach">How far ahead of Lumi's front a monster may stand and still be the target.</param>
    /// <param name="margin">Gap left between the monster's back and Lumi.</param>
    /// <param name="fallback">Distance when no monster is in reach.</param>
    /// <param name="max">Longest pass.</param>
    public static float Distance(float lumiX, int facing, float lumiHalfWidth, IEnumerable<(float X, float HalfWidth)> targets,
        float reach, float margin, float fallback, float max)
    {
        var nearestAhead = float.MaxValue;
        float? land = null;
        foreach (var (x, halfWidth) in targets)
        {
            var ahead = (x - lumiX) * facing;
            if (ahead <= 0 || ahead - halfWidth - lumiHalfWidth > reach || ahead >= nearestAhead)
            {
                continue;
            }

            nearestAhead = ahead;
            land = ahead + halfWidth + lumiHalfWidth + margin;
        }

        return Math.Min(land ?? fallback, max);
    }
}
