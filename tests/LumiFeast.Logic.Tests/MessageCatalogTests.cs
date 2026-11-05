using LumiFeast.Logic.Common;
using Xunit;

namespace LumiFeast.Logic.Tests;

public class MessageCatalogTests
{
    [Fact]
    public void ParsesAndFormats()
    {
        var catalog = MessageCatalog.Parse("# comment\n\nhello = Hi {0}, you have {1}\r\nplain=ok=fine\n");
        Assert.Equal("Hi Lumi, you have 3", catalog.Get("hello", "Lumi", 3));
        Assert.Equal("ok=fine", catalog.Get("plain"));
    }

    [Fact]
    public void UnknownKeyDoesNotCrash() => Assert.Equal("!nope!", MessageCatalog.Parse("").Get("nope"));
}
