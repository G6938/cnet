using System.Globalization;
using Cnet.Pipeline;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Cnet.Redis;

public sealed class RedisReplayGuard(IConnectionMultiplexer connection, IOptions<CnetRedisOptions> options)
    : IReplayGuard
{
    public async ValueTask<bool> TryRegisterAsync(int updateId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var settings = options.Value;
        var key = settings.KeyPrefix + ":replay:" + updateId.ToString(CultureInfo.InvariantCulture);

        return await connection.GetDatabase()
            .StringSetAsync(key, "1", settings.ReplayWindow, When.NotExists)
            .ConfigureAwait(false);
    }
}
