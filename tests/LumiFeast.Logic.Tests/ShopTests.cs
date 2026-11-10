using LumiFeast.Logic.Common;
using LumiFeast.Logic.Run;
using LumiFeast.Logic.Stages;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class ShopTests
{
    [Fact]
    public void WalletSpendsOnlyWhatItHas()
    {
        var wallet = new RunWallet();
        wallet.Earn(100);
        Assert.False(wallet.TrySpend(160));
        Assert.True(wallet.TrySpend(90));
        Assert.Equal(10, wallet.Gold);
    }

    [Fact]
    public void StockHasSkillAmuletsHealAndKey()
    {
        var shop = TestData.Game.Run("honey_labyrinth_stage_1").Shop;
        var stock = ShopStock.Draw(shop, ["a", "b"], ["x", "y", "z"], new SeededRandom(3));
        Assert.Equal(shop.Skills + shop.Amulets + 2, stock.Count);
        Assert.Contains(stock, item => item.Kind == ShopItemKinds.Heal);
        Assert.Contains(stock, item => item.Kind == ShopItemKinds.Key);
        Assert.Equal(shop.Amulets, stock.Count(item => item.Kind == ShopItemKinds.Amulet));
    }

    [Fact]
    public void FullSkillSlotsMeanNoSkillOnSale()
    {
        var shop = TestData.Game.Run("honey_labyrinth_stage_1").Shop;
        var stock = ShopStock.Draw(shop, [], ["x", "y"], new SeededRandom(1));
        Assert.DoesNotContain(stock, item => item.Kind == ShopItemKinds.Skill);
    }

    [Fact]
    public void TheStallAppearsOnlyAfterTheRaccoonIsHealed()
    {
        var run = TestData.Game.Run("honey_labyrinth_stage_1");
        var shops = run.Rooms.Where(room => room.Kind == RoomKinds.Shop).Select(room => room.Id).ToHashSet();
        for (ulong seed = 0; seed < 30; seed++)
        {
            Assert.DoesNotContain(StageMap.Generate(run, seed, withShop: false).Rooms, shops.Contains);
            Assert.Contains(StageMap.Generate(run, seed, withShop: true).Rooms, shops.Contains);
        }
    }
}
