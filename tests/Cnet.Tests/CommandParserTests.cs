using Cnet.Routing;
using Xunit;

namespace Cnet.Tests;

public sealed class CommandParserTests
{
    [Fact]
    public void Parses_CommandWithArguments()
    {
        Assert.True(CommandParser.TryParse("/start abc 123", out var command));
        Assert.Equal("start", command.Name);
        Assert.Equal("abc 123", command.Arguments);
    }

    [Fact]
    public void StripsBotMention_AndNormalizesCase()
    {
        Assert.True(CommandParser.TryParse("/LINK@MyBot x", out var command));
        Assert.Equal("link", command.Name);
        Assert.Equal("x", command.Arguments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("/")]
    [InlineData("/@bot")]
    public void Rejects_NonCommands(string? text)
    {
        Assert.False(CommandParser.TryParse(text, out _));
    }
}
