using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Run;

/// <summary>
/// The skills Lumi takes into a run (H2, round 75: four permanent skills picked at the entrance shrine), their level in
/// this run and their cooldowns. Runs level them up; levels reset when the run ends.
/// </summary>
public sealed class SkillLoadout(int slotCount)
{
    private readonly SkillDef?[] _slots = new SkillDef?[slotCount];
    private readonly int[] _cooldowns = new int[slotCount];
    private readonly int[] _levels = new int[slotCount];

    public IReadOnlyList<SkillDef?> Slots => _slots;

    public bool IsFull => _slots.All(slot => slot is not null);

    public bool Has(string id) => _slots.Any(slot => slot?.Id == id);

    public int CooldownLeft(int slot) => _cooldowns[slot];

    /// <summary>1 for a fresh skill, 0 for an empty slot.</summary>
    public int Level(int slot) => _slots[slot] is null ? 0 : _levels[slot];

    public int LevelOf(string id) => Array.FindIndex(_slots, slot => slot?.Id == id) is var slot and >= 0 ? _levels[slot] : 0;

    /// <summary>Equipped skills that can still level up.</summary>
    public IEnumerable<SkillDef> Upgradable => _slots.OfType<SkillDef>().Where(skill => LevelOf(skill.Id) < skill.MaxLevel);

    /// <summary>Raises an equipped skill one level; false when it is not equipped or already at its top level.</summary>
    public bool LevelUp(string id)
    {
        var slot = Array.FindIndex(_slots, skill => skill?.Id == id);
        if (slot < 0 || _levels[slot] >= _slots[slot]!.MaxLevel)
        {
            return false;
        }

        _levels[slot]++;
        return true;
    }

    /// <summary>What the slot's current level adds (null at Lv1).</summary>
    public SkillLevelDef? Bonus(int slot) => _slots[slot] is { Levels: { } levels } && _levels[slot] > 1 ? levels[_levels[slot] - 2] : null;

    /// <summary>Puts the skill in the first free slot.</summary>
    public bool Add(SkillDef skill)
    {
        var free = Array.IndexOf(_slots, null);
        if (free < 0 || Has(skill.Id))
        {
            return false;
        }

        _slots[free] = skill;
        _levels[free] = 1;
        return true;
    }

    public void Clear()
    {
        Array.Clear(_slots);
        Array.Clear(_cooldowns);
        Array.Clear(_levels);
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

        _cooldowns[slot] = (int)MathF.Round(skill.CooldownTicks * cooldownScale * (Bonus(slot)?.CooldownScale ?? 1));
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
    public const string TokenHeal = "token_heal";
    public const string SkillCooldown = "skill_cooldown";
    public const string AttackProjectile = "attack_projectile";

    /// <summary>Guard blocks counter by themselves; value = uses per room (Steam Bath).</summary>
    public const string AutoCounter = "auto_counter";

    /// <summary>Hits on a monster's back deal this much more (Wolf Fang).</summary>
    public const string BackDamage = "back_damage";

    /// <summary>The target attack slows monsters by this share for Ticks (Sticky Finish).</summary>
    public const string AttackSlow = "attack_slow";

    /// <summary>Flat MP when the target attack lands, once per swing (Finisher Fuel).</summary>
    public const string AttackMana = "attack_mana";

    /// <summary>Flat MP for a perfect guard (Finisher Fuel).</summary>
    public const string PerfectGuardMana = "perfect_guard_mana";

    /// <summary>Above 0: the first skill in each room costs no MP (Second Helping).</summary>
    public const string FreeFirstSkill = "free_first_skill";

    /// <summary>Speed stat bonus for Ticks after a perfect guard (Sugar Rush); still under the speed cap.</summary>
    public const string GuardRush = "guard_rush";

    public static readonly string[] All =
        [LightDamage, HeavyDamage, HeavyStagger, Speed, ManaGain, DamageTaken, TokenHeal, SkillCooldown, AttackProjectile, AutoCounter, BackDamage, AttackSlow, AttackMana, PerfectGuardMana, FreeFirstSkill, GuardRush];
}

/// <summary>The amulets of one run, read as numbers by whoever needs them.</summary>
public sealed class RunModifiers
{
    private readonly List<AmuletDef> _amulets = [];

    public IReadOnlyList<AmuletDef> Amulets => _amulets;

    public bool Has(string id) => _amulets.Any(amulet => amulet.Id == id);

    public void Add(AmuletDef amulet) => _amulets.Add(amulet);

    public void Clear() => _amulets.Clear();

    /// <param name="hpRatio">Current HP share; effects with a higher minimum are off.</param>
    public float Total(string kind, float hpRatio = 1) => Effects(kind).Where(effect => hpRatio >= effect.MinHpRatio).Sum(effect => effect.Value);

    /// <summary>1 plus every bonus of this kind; a value of -0.15 gives 0.85.</summary>
    public float Scale(string kind, float hpRatio = 1) => Math.Max(0, 1 + Total(kind, hpRatio));

    /// <summary>Sum of the effects of this kind aimed at one attack.</summary>
    public float TotalFor(string kind, string attackId) => Effects(kind).Where(effect => effect.Target == attackId).Sum(effect => effect.Value);

    /// <summary>Longest duration among the effects of this kind (aimed at one attack when given).</summary>
    public int Ticks(string kind, string? attackId = null) =>
        Effects(kind).Where(effect => attackId is null || effect.Target == attackId).Select(effect => effect.Ticks).DefaultIfEmpty(0).Max();

    /// <summary>Projectiles that amulets add to this attack.</summary>
    public IEnumerable<string> ProjectilesFor(string attackId) =>
        Effects(AmuletEffects.AttackProjectile).Where(effect => effect.Target == attackId).Select(effect => effect.Projectile!);

    private IEnumerable<AmuletEffectDef> Effects(string kind) =>
        _amulets.SelectMany(amulet => amulet.Effects).Where(effect => effect.Kind == kind);
}

/// <summary>Counts uses of a per-room effect (Steam Bath counters, Second Helping); entering a room resets it.</summary>
public sealed class RoomUses
{
    private int _used;

    public int Left(int limit) => Math.Max(0, limit - _used);

    public bool TryUse(int limit)
    {
        if (Left(limit) == 0)
        {
            return false;
        }

        _used++;
        return true;
    }

    public void Reset() => _used = 0;
}

/// <summary>Tokens are taken only when they help, so a full HP bar leaves them on the floor.</summary>
public static class PickupRules
{
    public static bool Helps(float current, float max) => current < max;
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
