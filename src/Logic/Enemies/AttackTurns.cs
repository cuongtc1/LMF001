namespace LumiFeast.Logic.Enemies;

/// <summary>
/// Shared by the monsters of one room: at most a few of them attack at the same moment, the others wait their turn
/// close by. A crowd then feels busy and dangerous but stays readable (15_core_gameplay.md, behavior rule 3).
/// </summary>
public sealed class AttackTurns(int maxAttackers)
{
    private readonly HashSet<object> _attackers = [];

    public int Attacking => _attackers.Count;

    /// <summary>The monster holds a turn, or a turn is free for it.</summary>
    public bool MayAttack(object monster) => _attackers.Contains(monster) || _attackers.Count < maxAttackers;

    public void Take(object monster)
    {
        if (MayAttack(monster))
        {
            _attackers.Add(monster);
        }
    }

    public void Release(object monster) => _attackers.Remove(monster);
}
