using Cnet.Routing;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Xunit;

namespace Cnet.Tests;

public sealed class ContextApiTests
{
    private static readonly IServiceProvider Services = new ServiceCollection().BuildServiceProvider();

    private static Message TestMessage() => new()
    {
        Id = 55,
        Date = DateTime.UtcNow,
        Chat = new Chat { Id = 7, Type = Telegram.Bot.Types.Enums.ChatType.Private },
        From = new User { Id = 7, FirstName = "T" },
        Text = "hello",
    };

    private static MessageContext Context(FakeBotClient bot)
    {
        var message = TestMessage();
        var update = new Update { Id = 1, Message = message };
        var inner = new UpdateContext(update, TestKit.Client(bot), Services, CancellationToken.None);
        return new MessageContext(inner, message);
    }

    private static CallbackContext Callback(FakeBotClient bot, bool withMessage = true)
    {
        var callbackQuery = new CallbackQuery
        {
            Id = "cb",
            From = new User { Id = 7, FirstName = "T" },
            Data = "x:42",
            ChatInstance = "ci",
            Message = withMessage ? TestMessage() : null,
        };
        var update = new Update { Id = 2, CallbackQuery = callbackQuery };
        var inner = new UpdateContext(update, TestKit.Client(bot), Services, CancellationToken.None);
        return new CallbackContext(inner, callbackQuery, "42");
    }

    [Fact]
    public async Task ReplyQuoted_SendsMessage_WithReplyParameters()
    {
        var bot = new FakeBotClient();
        await Context(bot).ReplyQuotedAsync("quoted");

        var request = Assert.IsType<SendMessageRequest>(Assert.Single(bot.Requests));
        Assert.Equal(55, request.ReplyParameters!.MessageId);
        Assert.Equal("quoted", request.Text);
    }

    [Fact]
    public async Task ForwardTo_SendsForwardRequest()
    {
        var bot = new FakeBotClient();
        await Context(bot).ForwardToAsync(99);

        var request = Assert.IsType<ForwardMessageRequest>(Assert.Single(bot.Requests));
        Assert.Equal(99, request.ChatId.Identifier);
        Assert.Equal(55, request.MessageId);
    }

    [Fact]
    public async Task CopyTo_SendsCopyRequest()
    {
        var bot = new FakeBotClient();
        await Context(bot).CopyToAsync(99);

        var request = Assert.IsType<CopyMessageRequest>(Assert.Single(bot.Requests));
        Assert.Equal(99, request.ChatId.Identifier);
    }

    [Fact]
    public async Task Delete_And_Typing_SendExpectedRequests()
    {
        var bot = new FakeBotClient();
        var context = Context(bot);
        await context.DeleteAsync();
        await context.TypingAsync();

        Assert.Contains(bot.Requests, request => request is DeleteMessageRequest);
        Assert.Contains(bot.Requests, request => request is SendChatActionRequest);
    }

    [Fact]
    public async Task CallbackEditText_EditsTheBoundMessage()
    {
        var bot = new FakeBotClient();
        await Callback(bot).EditTextAsync("edited");

        var request = Assert.IsType<EditMessageTextRequest>(Assert.Single(bot.Requests));
        Assert.Equal(55, request.MessageId);
        Assert.Equal("edited", request.Text);
    }

    [Fact]
    public async Task CallbackEditText_Throws_WithoutBoundMessage()
    {
        var bot = new FakeBotClient();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Callback(bot, withMessage: false).EditTextAsync("x"));
    }

    [Fact]
    public async Task CallbackAlert_AnswersWithAlert()
    {
        var bot = new FakeBotClient();
        await Callback(bot).AlertAsync("warning");

        var request = Assert.IsType<AnswerCallbackQueryRequest>(Assert.Single(bot.Requests));
        Assert.True(request.ShowAlert);
        Assert.Equal("warning", request.Text);
    }

    [Fact]
    public void TextShortcut_FallsBackToCaption()
    {
        var message = TestMessage();
        message.Text = null;
        message.Caption = "caption text";
        var update = new Update { Id = 3, Message = message };
        var context = new MessageContext(
            new UpdateContext(update, TestKit.Client(), Services, CancellationToken.None),
            message);

        Assert.Equal("caption text", context.Text);
    }
}
