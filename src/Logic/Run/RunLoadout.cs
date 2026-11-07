using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Run;

/// <summary>Skill slots picked during a run, with their cooldowns.</summary>
public sealed class SkillLoadout(int slotCount)
{
    private readonly SkillDef?[] _slots = new SkillDef?[slotCount];
    private readonly int[] _cooldowns = new int[slotCount];

    public IReadOnlyList<SkillDef?> Slots => _slots;

    public bool IsFull => _slots.All(slot => slot is not null);

    public bool Has(string id) => _slots.Any(slot => slot?.Id == id);

    public int CooldownLeft(int slot) => _cooldowns[slot];

    /// <summary>Puts the skill in the first free slot.</summary>
    public bool Add(SkillDef skill)
    {
        var free = Array.IndexOf(_slots, null);
        if (free < 0 || Has(skill.Id))
        {
            return false;
        }

        _slots[free] = skill;
        return true;
    }

    public void Clear()
    {
        Array.Clear(_slots);
        Array.Clear(_cooldowns);
    }

    public void Step()
    {
        for (var i = 0; i < _cooldowns.Length; i++)
        {
            _cooldowns[i] = Math.Max(0, _cooldowns[i] - 1);
        }
    }

    /// <summary>Returns the skill when it is ready and affordable, and starts its cooldown.</summary>
    public SkillDef? TryUse(int slot, float mp, float cooldownScale)
    {
        if (slot < 0 || slot >= _slots.Length || _slots[slot] is not { } skill || _cooldowns[slot] > 0 || mp < skill.MpCost)
        {
            return null;
        }

        _cooldowns[slot] = (int)MathF.Round(skill.CooldownTicks * cooldownScale);
        return skill;
    }
}

public static class AmuletEffects
{
    public const string LightDamage = "light_damage";
    public const string HeavyDamage = "heavy_damage";
    public const string HeavyStagger = "heavy_stagger";
    public const string Speed = "speed";
    public const string ManaGain = "mana_gain";
    public const string DamageTaken = "damage_taken";
    public const string RoomHeal = "room_heal";
    public const string SkillCooldown = "skill_cooldown";
    public const string AttackProjectile = "attack_projectile";

    public static readonly string[] All =
        [LightDamage, HeavyDamage, HeavyStagger, Speed, ManaGain, DamageTaken, RoomHeal, SkillCooldown, AttackProjectile];
}

/// <summary>The amulets of one run, read as numbers by whoever needs them.</summary>
public sealed class RunModifiers
{
    private readonly List<AmuletDef> _amulets = [];

    public IReadOnlyList<AmuletDef> Amulets => _amulets;

    public bool Has(string id) => _amulets.Any(amulet => amulet.Id == id);

    public void Add(AmuletDef amulet) => _amulets.Add(amulet);

    public void Clear() => _amulets.Clear();

    public float Total(string kind) => Effects(kind).Sum(effect => effect.Value);

    /// <summary>1 plus every bonus of this kind; a value of -0.15 gives 0.85.</summary>
    public float Scale(string kind) => Math.Max(0, 1 + Total(kind));

    /// <summary>Projectiles that amulets add to this attack.</summary>
    public IEnumerable<string> ProjectilesFor(string attackId) =>
        Effects(AmuletEffects.AttackProjectile).Where(effect => effect.Target == attackId).Select(effect => effect.Projectile!);

    private IEnumerable<AmuletEffectDef> Effects(string kind) =>
        _amulets.SelectMany(amulet => amulet.Effects).Where(effect => effect.Kind == kind);
}

/// <summary>Draws the choices of a reward pedestal.</summary>
public static class RewardDraw
{
    public static IReadOnlyList<string> Pick(IReadOnlyList<string> pool, int count, SeededRandom random)
    {
        var left = pool.ToList();
        var picks = new List<string>();
        while (picks.Count < count && left.Count > 0)
        {
            var index = random.NextInt(0, left.Count);
            picks.Add(left[index]);
            left.RemoveAt(index);
        }

        return picks;
    }
}
