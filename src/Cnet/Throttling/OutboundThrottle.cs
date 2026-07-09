using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Cnet.Throttling;

public interface IOutboundThrottle
{
    Task WaitAsync(long chatId, CancellationToken cancellationToken = default);
}

public sealed class OutboundThrottle(IOptions<CnetOptions> options) : IOutboundThrottle, IDisposable
{
    private const int MaxTrackedChats = 100000;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Queue<long> _globalSendTimes = new();
    private readonly ConcurrentDictionary<long, long> _lastSendPerChat = new();

    public async Task WaitAsync(long chatId, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.EnableOutboundThrottle)
        {
            return;
        }

        while (true)
        {
            long waitMilliseconds;
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var now = Environment.TickCount64;

                while (_globalSendTimes.Count > 0 && now - _globalSendTimes.Peek() >= 1000)
                {
                    _globalSendTimes.Dequeue();
                }

                long chatWait = 0;
                if (_lastSendPerChat.TryGetValue(chatId, out var lastSend))
                {
                    chatWait = settings.OutboundPerChatIntervalMilliseconds - (now - lastSend);
                }

                var globalReady = _globalSendTimes.Count < settings.OutboundGlobalPerSecond;

                if (chatWait <= 0 && globalReady)
                {
                    _globalSendTimes.Enqueue(now);
                    _lastSendPerChat[chatId] = now;

                    if (_lastSendPerChat.Count > MaxTrackedChats)
                    {
                        PurgeStaleChats(now, settings.OutboundPerChatIntervalMilliseconds);
                    }

                    return;
                }

                waitMilliseconds = Math.Max(chatWait, 10);
                if (!globalReady && _globalSendTimes.Count > 0)
                {
                    waitMilliseconds = Math.Max(waitMilliseconds, 1000 - (now - _globalSendTimes.Peek()));
                }
            }
            finally
            {
                _gate.Release();
            }

            await Task.Delay((int)Math.Min(waitMilliseconds, 1000), cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose() => _gate.Dispose();

    private void PurgeStaleChats(long now, int perChatInterval)
    {
        foreach (var pair in _lastSendPerChat)
        {
            if (now - pair.Value > perChatInterval * 10L)
            {
                _lastSendPerChat.TryRemove(pair.Key, out _);
            }
        }
    }
}
