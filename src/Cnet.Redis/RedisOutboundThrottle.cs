using System.Globalization;
using Cnet.Throttling;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Cnet.Redis;

public sealed class RedisOutboundThrottle(
    IConnectionMultiplexer connection,
    IOptions<CnetRedisOptions> redisOptions,
    IOptions<Cnet.CnetOptions> cnetOptions) : IOutboundThrottle
{
    private const string ReserveScript = """
        local current = tonumber(redis.call('GET', KEYS[1]) or '0')
        if current >= tonumber(ARGV[1]) then
            local ttl = redis.call('PTTL', KEYS[1])
            if ttl < 0 then ttl = ARGV[2] end
            return ttl
        end
        redis.call('INCR', KEYS[1])
        if current == 0 then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
        end
        return -1
        """;

    private const string ChatGateScript = """
        local last = tonumber(redis.call('GET', KEYS[1]) or '0')
        local now = tonumber(ARGV[1])
        local interval = tonumber(ARGV[2])
        local wait = interval - (now - last)
        if wait > 0 then
            return wait
        end
        redis.call('SET', KEYS[1], now, 'PX', interval * 2)
        return -1
        """;

    public async Task WaitAsync(long chatId, CancellationToken cancellationToken = default)
    {
        var options = cnetOptions.Value;
        if (!options.EnableOutboundThrottle)
        {
            return;
        }

        var database = connection.GetDatabase();
        var prefix = redisOptions.Value.KeyPrefix;

        while (!cancellationToken.IsCancellationRequested)
        {
            var chatWait = (long)(double)await database.ScriptEvaluateAsync(
                ChatGateScript,
                [prefix + ":throttle:chat:" + chatId.ToString(CultureInfo.InvariantCulture)],
                [Now(), options.OutboundPerChatIntervalMilliseconds]).ConfigureAwait(false);

            if (chatWait > 0)
            {
                await DelayAsync(chatWait, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var second = Environment.TickCount64 / 1000;
            var globalWait = (long)(double)await database.ScriptEvaluateAsync(
                ReserveScript,
                [prefix + ":throttle:global:" + second.ToString(CultureInfo.InvariantCulture)],
                [options.OutboundGlobalPerSecond, 1000]).ConfigureAwait(false);

            if (globalWait < 0)
            {
                return;
            }

            await DelayAsync(globalWait, cancellationToken).ConfigureAwait(false);
        }
    }

    private static long Now() => Environment.TickCount64;

    private static async Task DelayAsync(long milliseconds, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay((int)Math.Clamp(milliseconds, 1, 1000), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
