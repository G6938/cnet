using Cnet.DependencyInjection;
using Cnet.Pipeline;
using Cnet.Sessions;
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

        return builder;
    }
}
