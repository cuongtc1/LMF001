namespace LumiFeast.Logic.Combat;

/// <summary>
/// Combo kit v3 (round 77): a pass is earned by Z. Every Z swing that lands adds one charge; a pass (Z Z X, or the
/// rising slash Z Z Z X) needs <c>needed</c> charges and spends them all, so mashing X does nothing.
/// </summary>
public sealed class PassCharge(int needed)
{
    public int Needed { get; } = needed;

    public int Charges { get; private set; }

    public bool Ready => Charges >= Needed;

    /// <summary>A Z swing landed on at least one target (call once per swing).</summary>
    public void Landed() => Charges = Math.Min(Needed, Charges + 1);

    /// <returns>False when the pass is not earned yet.</returns>
    public bool TryUse()
    {
        if (!Ready)
        {
            return false;
        }

        Charges = 0;
        return true;
    }

    public void Reset() => Charges = 0;
}
