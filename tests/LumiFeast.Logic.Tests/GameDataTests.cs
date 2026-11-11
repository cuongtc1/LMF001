using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class GameDataTests
{
    [Fact]
    public void ComboKitMatchesTheDesign()
    {
        // 02_combat.md "Combo kit v4": X only inside combos (Z Z X pass, Z Z Z X rising slash), no up-slash in the air
        var set = TestData.Game.AttackSet("lumi_sword");
        AttackDef Get(string id) => set.Attacks.First(attack => attack.Id == id);
        var z1 = Get(set.First);
        var z2 = Get(z1.Next!);
        var z3 = Get(z2.Next!);
        Assert.Equal(set.First, z3.Next);
        Assert.Null(z1.NextHeavy);
        Assert.True(Get(z2.NextHeavy!) is { PassThrough: true, TurnAtEnd: true });
        Assert.True(Get(z3.NextHeavy!).LaunchSpeed > 0);
        Assert.Equal(string.Empty, set.FirstHeavy);
        Assert.Equal(set.FirstAir, set.FirstAirUp);
        Assert.False(Get(set.FirstAirHeavy).PassThrough);
        Assert.Single(set.Attacks, attack => attack.PassThrough);
        Assert.Equal("a3", Get(Get(set.FirstAir).Next!).Next);
        Assert.True(set.AirHopSpeed > 0);
    }

    [Fact]
    public void OnlyHeavyAttacksMoveMonstersUpOrDown()
    {
        var attacks = TestData.Game.AttackSet("lumi_sword").Attacks;
        Assert.All(attacks, attack =>
        {
            var heavy = attack.GuardBreak;
            Assert.True(heavy || attack is { LaunchSpeed: 0, Knockdown: false }, attack.Id);
        });
        Assert.Contains(attacks, attack => attack.LaunchSpeed > 0);
        Assert.Contains(attacks, attack => attack.Knockdown);
    }

    [Fact]
    public void EverySkillAndAmuletHasANameAndADescription()
    {
        var messages = MessageCatalog.Parse(new DiskDataSource().ReadText(DataPaths.Messages));
        var keys = TestData.Game.SkillSets.Values.SelectMany(set => set.Skills).Select(skill => MessageKeys.SkillPrefix + skill.Id)
            .Concat(TestData.Game.AmuletSets.Values.SelectMany(set => set.Amulets).Select(amulet => MessageKeys.AmuletPrefix + amulet.Id));
        Assert.All(keys, key =>
        {
            Assert.True(messages.Contains(key), key);
            Assert.True(messages.Contains(key + MessageKeys.DescriptionSuffix), key);
        });
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
