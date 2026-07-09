using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace Cnet.Testing;

public sealed class FakeBotClient : ITelegramBotClient
{
    private int _nextMessageId = 10000;

    public ConcurrentQueue<object> Requests { get; } = new();

    public Func<object, object?>? Responder { get; set; }

    public bool LocalBotServer => false;

    public long BotId => 1000000000;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    public IExceptionParser ExceptionsParser { get; set; } = new DefaultExceptionParser();

    public event AsyncEventHandler<ApiRequestEventArgs>? OnMakingApiRequest;

    public event AsyncEventHandler<ApiResponseEventArgs>? OnApiResponseReceived;

    public IReadOnlyList<object> SentRequests => [.. Requests];

    public IEnumerable<SendMessageRequest> SentMessages => Requests.OfType<SendMessageRequest>();

    public Task<TResponse> SendRequest<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Requests.Enqueue(request);
        _ = OnMakingApiRequest;
        _ = OnApiResponseReceived;

        if (Responder?.Invoke(request) is TResponse custom)
        {
            return Task.FromResult(custom);
        }

        return Task.FromResult(Default<TResponse>(request));
    }

    public Task DownloadFile(string filePath, Stream destination, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DownloadFile(TGFile file, Stream destination, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> TestApi(CancellationToken cancellationToken = default) => Task.FromResult(true);

    private TResponse Default<TResponse>(object request)
    {
        if (request is GetMeRequest
            && new User { Id = BotId, FirstName = "test", Username = "test_bot" } is TResponse me)
        {
            return me;
        }

        object? value = typeof(TResponse) switch
        {
            var t when t == typeof(bool) => true,
            var t when t == typeof(Message) => new Message
            {
                Id = Interlocked.Increment(ref _nextMessageId),
                Date = DateTime.UtcNow,
                Chat = new Chat { Id = 1, Type = Telegram.Bot.Types.Enums.ChatType.Private },
            },
            var t when t == typeof(MessageId) => new MessageId { Id = Interlocked.Increment(ref _nextMessageId) },
            var t when t == typeof(MessageId[]) => new[] { new MessageId { Id = Interlocked.Increment(ref _nextMessageId) } },
            var t when t == typeof(Update[]) => Array.Empty<Update>(),
            _ => null,
        };

        return value is TResponse typed
            ? typed
            : throw new InvalidOperationException("FakeBotClient has no default response for " + request.GetType().Name);
    }
}
