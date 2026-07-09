using Cnet.Sessions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Cnet.Redis;

public sealed class RedisSessionStorage(IConnectionMultiplexer connection, IOptions<CnetRedisOptions> options)
    : ISessionStorage
{
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await connection.GetDatabase().StringGetAsync(Prefixed(key)).ConfigureAwait(false);
        return value.IsNullOrEmpty ? null : value.ToString();
    }

    public async Task SetAsync(string key, string value, TimeSpan? lifetime = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await connection.GetDatabase()
            .StringSetAsync(Prefixed(key), value, lifetime ?? options.Value.SessionLifetime)
            .ConfigureAwait(false);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await connection.GetDatabase().KeyDeleteAsync(Prefixed(key)).ConfigureAwait(false);
    }

    private string Prefixed(string key) => options.Value.KeyPrefix + ":" + key;
}
