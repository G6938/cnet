using System.Collections.Concurrent;
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
    private readonly ConcurrentQueue<(RedisValue Id, Update Update)> _buffer = new();
    private readonly SemaphoreSlim _fetchGate = new(1, 1);
    private volatile bool _groupReady;
    private long _sinceLastClaim;

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

    public async ValueTask EnqueueAsync(Update update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        cancellationToken.ThrowIfCancellationRequested();

        var payload = JsonSerializer.Serialize(update, JsonBotAPI.Options);

        await _connection.GetDatabase().StreamAddAsync(
            _options.StreamName,
            [new NameValueEntry(PayloadField, payload)],
            maxLength: _options.StreamMaxLength,
            useApproximateMaxLength: true).ConfigureAwait(false);
    }

    public async ValueTask<UpdateLease?> DequeueAsync(CancellationToken cancellationToken)
    {
        await EnsureGroupAsync().ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_buffer.TryDequeue(out var entry))
            {
                return new UpdateLease(entry.Update, _ => AcknowledgeAsync(entry.Id));
            }

            await FillBufferAsync(cancellationToken).ConfigureAwait(false);

            if (_buffer.IsEmpty)
            {
                try
                {
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }
            }
        }

        return null;
    }

    public void Complete()
    {
    }

    public void Dispose() => _fetchGate.Dispose();

    private async Task FillBufferAsync(CancellationToken cancellationToken)
    {
        await _fetchGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_buffer.IsEmpty)
            {
                return;
            }

            await ClaimStalePendingAsync().ConfigureAwait(false);

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
                return;
            }

            BufferEntries(entries);
        }
        finally
        {
            _fetchGate.Release();
        }
    }

    private async Task ClaimStalePendingAsync()
    {
        var now = Environment.TickCount64;
        if (now - Interlocked.Read(ref _sinceLastClaim) < _options.PendingClaimIdle.TotalMilliseconds)
        {
            return;
        }

        Interlocked.Exchange(ref _sinceLastClaim, now);

        try
        {
            var database = _connection.GetDatabase();
            var idleMilliseconds = (long)_options.PendingClaimIdle.TotalMilliseconds;
            var pending = await database.StreamPendingMessagesAsync(
                _options.StreamName,
                _options.ConsumerGroup,
                count: 32,
                consumerName: RedisValue.Null).ConfigureAwait(false);

            var stale = pending
                .Where(message => message.IdleTimeInMilliseconds >= idleMilliseconds)
                .Select(message => message.MessageId)
                .ToArray();

            if (stale.Length == 0)
            {
                return;
            }

            var claimed = await database.StreamClaimAsync(
                _options.StreamName,
                _options.ConsumerGroup,
                _consumerName,
                idleMilliseconds,
                stale).ConfigureAwait(false);

            BufferEntries(claimed);
        }
        catch (RedisException exception)
        {
            _logger.StreamClaimFailed(exception);
        }
    }

    private void BufferEntries(StreamEntry[] entries)
    {
        foreach (var entry in entries)
        {
            var payload = entry[PayloadField];
            if (payload.IsNullOrEmpty)
            {
                FireAndForgetAck(entry.Id);
                continue;
            }

            var update = JsonSerializer.Deserialize<Update>(payload.ToString(), JsonBotAPI.Options);
            if (update is null)
            {
                FireAndForgetAck(entry.Id);
                continue;
            }

            _buffer.Enqueue((entry.Id, update));
        }
    }

    private void FireAndForgetAck(RedisValue entryId) => _ = AcknowledgeAsync(entryId).AsTask();

    private async ValueTask AcknowledgeAsync(RedisValue entryId)
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

        try
        {
            await _connection.GetDatabase().StreamCreateConsumerGroupAsync(
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

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Claiming stale pending entries failed")]
    internal static partial void StreamClaimFailed(this ILogger logger, Exception exception);
}
