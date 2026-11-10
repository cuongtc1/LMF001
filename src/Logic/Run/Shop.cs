using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Run;

public static class ShopItemKinds
{
    public const string Skill = "skill";
    public const string Amulet = "amulet";
    public const string Heal = "heal";
    public const string Key = "key";
}

/// <param name="Id">Skill or amulet id; empty for heal and key.</param>
public sealed record ShopItem(string Kind, string Id, int Price);

/// <summary>Gold carried in one run (Little Noah: gold comes from foes, objects and chests, and is spent in shops).</summary>
public sealed class RunWallet
{
    public int Gold { get; private set; }

    public void Earn(int amount) => Gold += Math.Max(0, amount);

    public bool TrySpend(int price)
    {
        if (price > Gold)
        {
            return false;
        }

        Gold -= price;
        return true;
    }
}

/// <summary>What the Raccoon's stall sells in one room: a random selection, like Little Noah's shops.</summary>
public static class ShopStock
{
    /// <param name="skills">Skills Lumi could still take (empty when her slots are full).</param>
    /// <param name="amulets">Amulets Lumi does not have yet.</param>
    public static List<ShopItem> Draw(ShopDef shop, IReadOnlyList<string> skills, IReadOnlyList<string> amulets, SeededRandom random)
    {
        var items = new List<ShopItem>();
        foreach (var id in RewardDraw.Pick(skills, shop.Skills, random))
        {
            items.Add(new ShopItem(ShopItemKinds.Skill, id, shop.SkillPrice));
        }

        foreach (var id in RewardDraw.Pick(amulets, shop.Amulets, random))
        {
            items.Add(new ShopItem(ShopItemKinds.Amulet, id, shop.AmuletPrice));
        }

        items.Add(new ShopItem(ShopItemKinds.Heal, string.Empty, shop.HealPrice));
        items.Add(new ShopItem(ShopItemKinds.Key, string.Empty, shop.KeyPrice));
        return items;
    }
}
