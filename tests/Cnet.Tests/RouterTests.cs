using Cnet.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Xunit;

namespace Cnet.Tests;

public sealed class RouterTests
{
    private static readonly CnetClient Client = new(
        new TelegramBotClient("1000000000:test-token-for-router-tests"),
        Options.Create(new CnetOptions { BotToken = "x" }));

    private static readonly IServiceProvider Services = new ServiceCollection().BuildServiceProvider();

    private static UpdateContext Context(Update update) => new(update, Client, Services, CancellationToken.None);

    private static Update MessageUpdate(string text, long chatId = 1) => new()
    {
        Id = 1,
        Message = new Message
        {
            Id = 10,
            Date = DateTime.UtcNow,
            Chat = new Chat { Id = chatId, Type = Telegram.Bot.Types.Enums.ChatType.Private },
            From = new User { Id = chatId, FirstName = "T" },
            Text = text,
        },
    };

    private static Update CallbackUpdate(string data) => new()
    {
        Id = 2,
        CallbackQuery = new CallbackQuery
        {
            Id = "cb1",
            From = new User { Id = 1, FirstName = "T" },
            Data = data,
            ChatInstance = "ci",
        },
    };

    [Fact]
    public async Task RoutesCommand_ToRegisteredHandler()
    {
        var router = new CnetRouter();
        string? received = null;
        router.AddCommand("start", ctx =>
        {
            received = ctx.Arguments;
            return Task.CompletedTask;
        });

        await router.RouteAsync(Context(MessageUpdate("/start payload")));

        Assert.Equal("payload", received);
    }

    [Fact]
    public async Task UnknownCommand_IsSilentlyIgnored()
    {
        var router = new CnetRouter();
        var handled = false;
        router.AddMessageHandler(_ =>
        {
            handled = true;
            return Task.CompletedTask;
        });

        await router.RouteAsync(Context(MessageUpdate("/unknown")));

        Assert.False(handled);
    }

    [Fact]
    public async Task PlainMessage_GoesToMessageHandler()
    {
        var router = new CnetRouter();
        string? received = null;
        router.AddMessageHandler(ctx =>
        {
            received = ctx.Message.Text;
            return Task.CompletedTask;
        });

        await router.RouteAsync(Context(MessageUpdate("hello")));

        Assert.Equal("hello", received);
    }

    [Fact]
    public async Task Callback_MatchesLongestPrefix()
    {
        var router = new CnetRouter();
        string? matched = null;
        router.AddCallback("a:", _ =>
        {
            matched = "short";
            return Task.CompletedTask;
        });
        router.AddCallback("a:b:", ctx =>
        {
            matched = "long:" + ctx.Payload;
            return Task.CompletedTask;
        });

        await router.RouteAsync(Context(CallbackUpdate("a:b:42")));

        Assert.Equal("long:42", matched);
    }

    [Fact]
    public async Task Callback_PayloadStripsPrefix()
    {
        var router = new CnetRouter();
        string? payload = null;
        router.AddCallback("blk:", ctx =>
        {
            payload = ctx.Payload;
            return Task.CompletedTask;
        });

        await router.RouteAsync(Context(CallbackUpdate("blk:abc")));

        Assert.Equal("abc", payload);
    }
}
