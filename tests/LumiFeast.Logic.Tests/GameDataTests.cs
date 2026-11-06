using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class GameDataTests
{
    [Fact]
    public void LevelOneSetMatchesTheDesign()
    {
        var set = TestData.Game.AttackSet("lumi_sword");
        AttackDef Get(string id) => set.Attacks.First(attack => attack.Id == id);
        Assert.Equal(set.First, Get(Get(Get(set.First).Next!).Next!).Next);
        Assert.True(Get(Get(set.First).NextHeavy!).LaunchSpeed > 0);
        Assert.NotNull(Get(set.FirstHeavy).NextHeavy);
        Assert.Null(Get(set.FirstAir).Next);
        Assert.True(Get(set.FirstAirHeavy).PlungeSpeed > 0);
        Assert.True(Get(set.FirstAirHeavy).LandingLagTicks > 0);
        Assert.Equal((3f, 5f, 4f), (Get(set.First).Mana, Get(set.FirstHeavy).Mana, Get(set.FirstSprint).Mana));
    }

    [Fact]
    public void OnlyHeavyAttacksMoveMonstersUpOrDown()
    {
        var attacks = TestData.Game.AttackSet("lumi_sword").Attacks;
        Assert.All(attacks, attack =>
        {
            var heavy = attack.Id.Contains('x');
            Assert.True(heavy || attack is { LaunchSpeed: 0, Knockdown: false }, attack.Id);
        });
        Assert.Contains(attacks, attack => attack.LaunchSpeed > 0);
        Assert.Contains(attacks, attack => attack.Knockdown);
    }

    [Fact]
    public void RealDataLoads()
    {
        Assert.NotEmpty(TestData.Game.Monsters);
        Assert.Equal(TestData.Game.Difficulties.Default, TestData.Game.Difficulty(TestData.Game.Difficulties.Default).Id);
    }

    [Fact]
    public void EveryMessageKeyExists()
    {
        var catalog = MessageCatalog.Parse(new DiskDataSource().ReadText(DataPaths.Messages));
        Assert.All(MessageKeys.All, key => Assert.True(catalog.Contains(key), key));
    }

    [Fact]
    public void UnknownMonsterGivesDataError()
    {
        var error = Assert.Throws<GameDataException>(() => TestData.Game.Monster("nope"));
        Assert.Equal(MessageKeys.DataUnknownId, error.MessageKey);
    }

    [Fact]
    public void MissingFieldIsRejected()
    {
        var source = new OverrideSource(DataPaths.Scaling, "{ \"hp_growth_per_tier\": 1.3 }");
        var error = Assert.Throws<GameDataException>(() => new GameDataLoader(source).Load());
        Assert.Equal(MessageKeys.DataFileInvalid, error.MessageKey);
    }

    private sealed class OverrideSource(string path, string text) : IDataSource
    {
        private readonly DiskDataSource _disk = new();

        public string ReadText(string file) => file == path ? text : _disk.ReadText(file);

        public IEnumerable<string> ListFiles(string folder, string extension) => _disk.ListFiles(folder, extension);
    }
}
