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

    private static TResponse DefaultResponse<TResponse>(object request)
    {
        object? value = request switch
        {
            GetUpdatesRequest => Array.Empty<Update>(),
            DeleteWebhookRequest => true,
            AnswerCallbackQueryRequest => true,
            GetMeRequest => new User { Id = 1000000000, FirstName = "fake", Username = "fake_bot" },
            SendMessageRequest send => new Message
            {
                Id = 999,
                Date = DateTime.UtcNow,
                Chat = new Chat { Id = send.ChatId.Identifier ?? 0, Type = Telegram.Bot.Types.Enums.ChatType.Private },
            },
            _ => null,
        };

        return value is TResponse typed
            ? typed
            : throw new InvalidOperationException("No fake response for " + request.GetType().Name);
    }
}
