using Cnet.Pipeline;
using Cnet.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Xunit;

namespace Cnet.Tests;

public sealed class BugFixTests
{
    [Fact]
    public async Task Retry_HandlesWrappedNetworkErrors_FromTelegramBot()
    {
        var calls = 0;
        var result = await RetryExecutor.ExecuteAsync(
            _ =>
            {
                calls++;
                if (calls < 3)
                {
                    throw new RequestException("Exception during making request", new HttpRequestException("boom"));
                }

                return Task.FromResult("ok");
            },
            maxAttempts: 3);

        Assert.Equal("ok", result);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Retry_DoesNotRetry_ApiErrorsWithoutRetryAfter()
    {
        var calls = 0;
        await Assert.ThrowsAsync<ApiRequestException>(() => RetryExecutor.ExecuteAsync<int>(
            _ =>
            {
                calls++;
                throw new ApiRequestException("Bad Request", 400);
            },
            maxAttempts: 3));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task EnqueueAsync_AppliesBackpressure_InsteadOfDropping()
    {
        var channel = new BoundedUpdateChannel(Options.Create(new CnetOptions { UpdateQueueCapacity = 1 }));

        await channel.EnqueueAsync(new Update { Id = 1 }, CancellationToken.None);
        var pending = channel.EnqueueAsync(new Update { Id = 2 }, CancellationToken.None);

        Assert.False(pending.IsCompleted);
        Assert.True(channel.TryDequeue(out var first));
        Assert.Equal(1, first.Id);

        await pending;
        Assert.True(channel.TryDequeue(out var second));
        Assert.Equal(2, second.Id);
    }

    [Fact]
    public async Task UnmatchedCallback_IsAutoAnswered()
    {
        var bot = new FakeBotClient();
        var client = TestKit.Client(bot);
        var router = new CnetRouter();
        await using var provider = new ServiceCollection().BuildServiceProvider();

        var update = new Update
        {
            Id = 1,
            CallbackQuery = new CallbackQuery
            {
                Id = "cb1",
                From = new User { Id = 1, FirstName = "T" },
                Data = "nobody-owns-this",
                ChatInstance = "ci",
            },
        };

        await router.RouteAsync(new UpdateContext(update, client, provider, CancellationToken.None));

        Assert.Contains(bot.Requests, request => request is AnswerCallbackQueryRequest);
    }

    [Fact]
    public async Task MatchedCallback_IsNotAutoAnswered()
    {
        var bot = new FakeBotClient();
        var client = TestKit.Client(bot);
        var router = new CnetRouter();
        router.AddCallback("x:", _ => Task.CompletedTask);
        await using var provider = new ServiceCollection().BuildServiceProvider();

        var update = new Update
        {
            Id = 1,
            CallbackQuery = new CallbackQuery
            {
                Id = "cb1",
                From = new User { Id = 1, FirstName = "T" },
                Data = "x:1",
                ChatInstance = "ci",
            },
        };

        await router.RouteAsync(new UpdateContext(update, client, provider, CancellationToken.None));

        Assert.DoesNotContain(bot.Requests, request => request is AnswerCallbackQueryRequest);
    }
}
