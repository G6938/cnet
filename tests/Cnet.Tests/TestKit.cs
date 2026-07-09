using System.Collections.Concurrent;
using Cnet.Throttling;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace Cnet.Tests;

public static class TestKit
{
    public static CnetOptions Options(Action<CnetOptions>? configure = null)
    {
        var options = new CnetOptions { BotToken = "1000000000:test", EnableOutboundThrottle = false };
        configure?.Invoke(options);
        return options;
    }

    public static CnetClient Client(FakeBotClient? bot = null, Action<CnetOptions>? configure = null)
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options(configure));
        return new CnetClient(bot ?? new FakeBotClient(), new OutboundThrottle(options), options);
    }
}

public sealed class FakeBotClient : ITelegramBotClient
{
    public ConcurrentQueue<object> Requests { get; } = new();

    public Func<object, object?>? Responder { get; set; }

    public bool LocalBotServer => false;

    public long BotId => 1000000000;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    public IExceptionParser ExceptionsParser { get; set; } = new DefaultExceptionParser();

    public event AsyncEventHandler<Telegram.Bot.Args.ApiRequestEventArgs>? OnMakingApiRequest;

    public event AsyncEventHandler<Telegram.Bot.Args.ApiResponseEventArgs>? OnApiResponseReceived;

    public Task<TResponse> SendRequest<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Requests.Enqueue(request);
        _ = OnMakingApiRequest;
        _ = OnApiResponseReceived;

        var custom = Responder?.Invoke(request);
        if (custom is TResponse typed)
        {
            return Task.FromResult(typed);
        }

        return Task.FromResult(DefaultResponse<TResponse>(request));
    }

    public Task<bool> TestApi(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task DownloadFile(string filePath, Stream destination, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DownloadFile(TGFile file, Stream destination, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private static int _nextFakeMessageId = 20000;

    private static TResponse DefaultResponse<TResponse>(object request)
    {
        if (request is GetUpdatesRequest && Array.Empty<Update>() is TResponse updates)
        {
            return updates;
        }

        if (request is GetMeRequest
            && new User { Id = 1000000000, FirstName = "fake", Username = "fake_bot" } is TResponse me)
        {
            return me;
        }

        object? value = typeof(TResponse) switch
        {
            var t when t == typeof(bool) => true,
            var t when t == typeof(Message) => new Message
            {
                Id = Interlocked.Increment(ref _nextFakeMessageId),
                Date = DateTime.UtcNow,
                Chat = new Chat { Id = 1, Type = Telegram.Bot.Types.Enums.ChatType.Private },
            },
            var t when t == typeof(MessageId) => new MessageId { Id = Interlocked.Increment(ref _nextFakeMessageId) },
            var t when t == typeof(MessageId[]) => new[] { new MessageId { Id = Interlocked.Increment(ref _nextFakeMessageId) } },
            _ => null,
        };

        return value is TResponse typed
            ? typed
            : throw new InvalidOperationException("No fake response for " + request.GetType().Name);
    }
}
