using Cnet.Keyboards;
using Xunit;

namespace Cnet.Tests;

public sealed class KeyboardBuilderTests
{
    [Fact]
    public void Inline_BuildsRowsAndButtons()
    {
        var markup = Keyboard.Inline()
            .Row().Callback("A", "a").Callback("B", "b")
            .Row().Url("Site", "https://example.com")
            .Build();

        var rows = markup.InlineKeyboard.Select(row => row.ToArray()).ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Equal(2, rows[0].Length);
        Assert.Equal("a", rows[0][0].CallbackData);
        Assert.Equal("https://example.com", rows[1][0].Url);
    }

    [Fact]
    public void Inline_StartsImplicitRow_AndSkipsEmptyRows()
    {
        var markup = Keyboard.Inline()
            .Callback("A", "a")
            .Row()
            .Build();

        Assert.Single(markup.InlineKeyboard);
    }

    [Fact]
    public void Reply_BuildsWithFlags()
    {
        var markup = Keyboard.Reply()
            .Row().Button("One").Button("Two")
            .OneTime()
            .Build();

        Assert.True(markup.ResizeKeyboard);
        Assert.True(markup.OneTimeKeyboard);
        Assert.Equal(2, markup.Keyboard.First().Count());
    }
}
