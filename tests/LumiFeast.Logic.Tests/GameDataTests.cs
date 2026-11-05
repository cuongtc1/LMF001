using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class GameDataTests
{
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
