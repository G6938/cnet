using System.Diagnostics;
using Cnet.Pipeline;
using Cnet.Routing;
using Cnet.Throttling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Xunit;

namespace Cnet.Tests;

public sealed class ThrottleAndRateLimitTests
{
    [Fact]
    public async Task Throttle_DelaysSecondSend_ToSameChat()
    {
        using var throttle = new OutboundThrottle(Options.Create(new CnetOptions
        {
            BotToken = "x",
            EnableOutboundThrottle = true,
            OutboundPerChatIntervalMilliseconds = 120,
            OutboundGlobalPerSecond = 1000,
        }));

        var stopwatch = Stopwatch.StartNew();
        await throttle.WaitAsync(1);
        await throttle.WaitAsync(1);
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds >= 90, "elapsed=" + stopwatch.ElapsedMilliseconds);
    }

    [Fact]
    public async Task Throttle_DoesNotDelay_DifferentChats()
    {
        using var throttle = new OutboundThrottle(Options.Create(new CnetOptions
        {
            BotToken = "x",
            EnableOutboundThrottle = true,
            OutboundPerChatIntervalMilliseconds = 500,
            OutboundGlobalPerSecond = 1000,
        }));

        var stopwatch = Stopwatch.StartNew();
        await throttle.WaitAsync(1);
        await throttle.WaitAsync(2);
        await throttle.WaitAsync(3);
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 200, "elapsed=" + stopwatch.ElapsedMilliseconds);
    }

    [Fact]
    public async Task Throttle_Disabled_IsPassThrough()
    {
        using var throttle = new OutboundThrottle(Options.Create(new CnetOptions
        {
            BotToken = "x",
            EnableOutboundThrottle = false,
            OutboundPerChatIntervalMilliseconds = 1000,
        }));

        var stopwatch = Stopwatch.StartNew();
        await throttle.WaitAsync(1);
        await throttle.WaitAsync(1);
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 100);
    }

    [Fact]
    public void InboundRateLimit_BlocksAboveLimit_PerUser()
    {
        var state = new InboundRateLimitState(2);

        Assert.True(state.TryRegister(1));
        Assert.True(state.TryRegister(1));
        Assert.False(state.TryRegister(1));
        Assert.True(state.TryRegister(2));
    }

    [Fact]
    public async Task RateLimitMiddleware_DropsExcessUpdates()
    {
        var state = new InboundRateLimitState(1);
        var middleware = new InboundRateLimitMiddleware(state);
        var passed = 0;
        await using var provider = new ServiceCollection().BuildServiceProvider();

        UpdateContext Context() => new(
            new Update
            {
                Id = 1,
                Message = new Message
                {
                    Id = 1,
                    Date = DateTime.UtcNow,
                    Chat = new Chat { Id = 5, Type = Telegram.Bot.Types.Enums.ChatType.Private },
                    From = new User { Id = 5, FirstName = "T" },
                    Text = "x",
                },
            },
            TestKit.Client(),
            provider,
            CancellationToken.None);

        Task Next(UpdateContext _)
        {
            passed++;
            return Task.CompletedTask;
        }

        await middleware.InvokeAsync(Context(), Next);
        await middleware.InvokeAsync(Context(), Next);

        Assert.Equal(1, passed);
    }
}
