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
    /// <summary>
    /// Replaces unsold items Lumi can no longer use (she found that skill or amulet after the stock was drawn, or her
    /// skill slots filled up) with another item of the same kind, or marks them sold when none is left (round 70 bug).
    /// </summary>
    /// <param name="skills">Skills Lumi could still take now.</param>
    /// <param name="amulets">Amulets Lumi does not have now.</param>
    public static void Refresh(List<ShopItem> stock, HashSet<int> sold, IReadOnlyList<string> skills, IReadOnlyList<string> amulets, SeededRandom random)
    {
        for (var i = 0; i < stock.Count; i++)
        {
            var item = stock[i];
            var options = item.Kind switch
            {
                ShopItemKinds.Skill => skills,
                ShopItemKinds.Amulet => amulets,
                _ => null,
            };
            if (sold.Contains(i) || options is null || options.Contains(item.Id))
            {
                continue;
            }

            var free = options.Where(id => !stock.Any(other => other.Kind == item.Kind && other.Id == id)).ToList();
            if (free.Count == 0)
            {
                sold.Add(i);
                continue;
            }

            stock[i] = item with { Id = RewardDraw.Pick(free, 1, random)[0] };
        }
    }

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
