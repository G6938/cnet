using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Xunit;

namespace Cnet.Tests;

public sealed class RetryExecutorTests
{
    [Fact]
    public async Task ReturnsResult_OnFirstSuccess()
    {
        var calls = 0;
        var result = await RetryExecutor.ExecuteAsync(
            _ =>
            {
                calls++;
                return Task.FromResult(42);
            },
            maxAttempts: 3);

        Assert.Equal(42, result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Retries_On429_WithRetryAfter()
    {
        var calls = 0;
        var result = await RetryExecutor.ExecuteAsync(
            _ =>
            {
                calls++;
                if (calls < 3)
                {
                    throw new ApiRequestException("Too Many Requests", 429, new ResponseParameters { RetryAfter = 0 });
                }

                return Task.FromResult("ok");
            },
            maxAttempts: 3);

        Assert.Equal("ok", result);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Throws_WhenAttemptsExhausted()
    {
        await Assert.ThrowsAsync<ApiRequestException>(() => RetryExecutor.ExecuteAsync<int>(
            _ => throw new ApiRequestException("Too Many Requests", 429, new ResponseParameters { RetryAfter = 0 }),
            maxAttempts: 2));
    }

    [Fact]
    public async Task Retries_OnTransientNetworkErrors()
    {
        var calls = 0;
        await RetryExecutor.ExecuteAsync(
            _ =>
            {
                calls++;
                return calls < 2 ? throw new HttpRequestException("boom") : Task.CompletedTask;
            },
            maxAttempts: 3);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task DoesNotRetry_NonRetryableApiErrors()
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
}
