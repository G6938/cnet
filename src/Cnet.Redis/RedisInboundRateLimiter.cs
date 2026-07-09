using System.Globalization;
using Cnet.Pipeline;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Cnet.Redis;

public sealed class RedisInboundRateLimiter(
    int updatesPerMinutePerUser,
    IConnectionMultiplexer connection,
    IOptions<CnetRedisOptions> options) : IInboundRateLimiter
{
    private const string Script = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('PEXPIRE', KEYS[1], 60000)
        end
        return count
        """;

    public async ValueTask<bool> TryRegisterAsync(long userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = options.Value.KeyPrefix + ":ratelimit:" + userId.ToString(CultureInfo.InvariantCulture);
        var count = (long)await connection.GetDatabase()
            .ScriptEvaluateAsync(Script, [key])
            .ConfigureAwait(false);

        return count <= updatesPerMinutePerUser;
    }
}
