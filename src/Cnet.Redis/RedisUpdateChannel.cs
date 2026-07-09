using System.Text.Json;
using Cnet.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Cnet.Redis;

public sealed class RedisUpdateChannel : IUpdateChannel, IDisposable
{
    private const string PayloadField = "u";

    private readonly IConnectionMultiplexer _connection;
    private readonly CnetRedisOptions _options;
    private readonly ILogger<RedisUpdateChannel> _logger;
    private readonly string _consumerName;
    private readonly SemaphoreSlim _available = new(0);
    private readonly Queue<(RedisValue Id, Update Update)> _buffer = new();
    private readonly Lock _bufferGate = new();
    private volatile bool _groupReady;

    public RedisUpdateChannel(
        IConnectionMultiplexer connection,
        IOptions<CnetRedisOptions> options,
        ILogger<RedisUpdateChannel> logger)
    {
        _connection = connection;
        _options = options.Value;
        _logger = logger;
        _consumerName = "c-" + Guid.NewGuid().ToString("N")[..8];
    }

    public bool TryEnqueue(Update update)
    {
        _ = EnqueueAsync(update, CancellationToken.None).AsTask();
        return true;
    }

    public async ValueTask EnqueueAsync(Update update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        cancellationToken.ThrowIfCancellationRequested();

        var payload = JsonSerializer.Serialize(update, JsonBotAPI.Options);
        var database = _connection.GetDatabase();

        await database.StreamAddAsync(
            _options.StreamName,
            [new NameValueEntry(PayloadField, payload)],
            maxLength: _options.StreamMaxLength,
            useApproximateMaxLength: true).ConfigureAwait(false);
    }

    public async ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
    {
        await EnsureGroupAsync().ConfigureAwait(false);

        while (true)
        {
            lock (_bufferGate)
            {
                if (_buffer.Count > 0)
                {
                    return true;
                }
            }

            if (await FetchBatchAsync(cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            try
            {
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }
    }

    public bool TryDequeue(out Update update)
    {
        lock (_bufferGate)
        {
            if (_buffer.Count > 0)
            {
                var entry = _buffer.Dequeue();
                update = entry.Update;
                _ = AcknowledgeAsync(entry.Id);
                return true;
            }
        }

        update = null!;
        return false;
    }

    public void Complete()
    {
    }

    public void Dispose() => _available.Dispose();

    private async Task<bool> FetchBatchAsync(CancellationToken cancellationToken)
    {
        var database = _connection.GetDatabase();

        StreamEntry[] entries;
        try
        {
            entries = await database.StreamReadGroupAsync(
                _options.StreamName,
                _options.ConsumerGroup,
                _consumerName,
                StreamPosition.NewMessages,
                count: 32).ConfigureAwait(false);
        }
        catch (RedisException exception)
        {
            _logger.StreamReadFailed(exception);
            return false;
        }

        if (entries.Length == 0)
        {
            return false;
        }

        var added = false;
        foreach (var entry in entries)
        {
            var payload = entry[PayloadField];
            if (payload.IsNullOrEmpty)
            {
                await AcknowledgeAsync(entry.Id).ConfigureAwait(false);
                continue;
            }

            var update = JsonSerializer.Deserialize<Update>(payload.ToString(), JsonBotAPI.Options);
            if (update is null)
            {
                await AcknowledgeAsync(entry.Id).ConfigureAwait(false);
                continue;
            }

            lock (_bufferGate)
            {
                _buffer.Enqueue((entry.Id, update));
            }

            added = true;
        }

        return added;
    }

    private async Task AcknowledgeAsync(RedisValue entryId)
    {
        try
        {
            await _connection.GetDatabase()
                .StreamAcknowledgeAsync(_options.StreamName, _options.ConsumerGroup, entryId)
                .ConfigureAwait(false);
        }
        catch (RedisException exception)
        {
            _logger.StreamAckFailed(exception);
        }
    }

    private async Task EnsureGroupAsync()
    {
        if (_groupReady)
        {
            return;
        }

        var database = _connection.GetDatabase();
        try
        {
            await database.StreamCreateConsumerGroupAsync(
                _options.StreamName,
                _options.ConsumerGroup,
                StreamPosition.Beginning,
                createStream: true).ConfigureAwait(false);
        }
        catch (RedisException exception) when (exception.Message.Contains("BUSYGROUP", StringComparison.Ordinal))
        {
        }

        _groupReady = true;
    }
}

internal static partial class RedisUpdateChannelLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Reading the update stream failed")]
    internal static partial void StreamReadFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Acknowledging a stream entry failed")]
    internal static partial void StreamAckFailed(this ILogger logger, Exception exception);
}
