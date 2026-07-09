using Cnet.DependencyInjection;
using Cnet.Pipeline;
using Cnet.Sessions;
using Cnet.Throttling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Cnet.Redis;

public static class CnetRedisExtensions
{
    public static CnetBuilder UseRedis(this CnetBuilder builder, Action<CnetRedisOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var services = builder.Services;

        services.AddOptions<CnetRedisOptions>()
            .Configure(configure)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "CnetRedisOptions.ConnectionString is required.")
            .ValidateOnStart();

        services.TryAddSingleton<IConnectionMultiplexer>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<CnetRedisOptions>>().Value;
            var configuration = ConfigurationOptions.Parse(options.ConnectionString);
            configuration.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(configuration);
        });

        services.RemoveAll<IUpdateChannel>();
        services.AddSingleton<IUpdateChannel, RedisUpdateChannel>();

        services.RemoveAll<IReplayGuard>();
        services.AddSingleton<IReplayGuard, RedisReplayGuard>();

        services.RemoveAll<ISessionStorage>();
        services.AddSingleton<ISessionStorage, RedisSessionStorage>();

        services.RemoveAll<IOutboundThrottle>();
        services.AddSingleton<IOutboundThrottle, RedisOutboundThrottle>();

        return builder;
    }

    public static CnetBuilder UseRedisRateLimit(this CnetBuilder builder, int updatesPerMinutePerUser)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentOutOfRangeException.ThrowIfLessThan(updatesPerMinutePerUser, 1);

        var services = builder.Services;
        services.RemoveAll<IInboundRateLimiter>();
        services.AddSingleton<IInboundRateLimiter>(provider => new RedisInboundRateLimiter(
            updatesPerMinutePerUser,
            provider.GetRequiredService<IConnectionMultiplexer>(),
            provider.GetRequiredService<IOptions<CnetRedisOptions>>()));
        services.AddScoped<IUpdateMiddleware, InboundRateLimitMiddleware>();
        return builder;
    }
}
