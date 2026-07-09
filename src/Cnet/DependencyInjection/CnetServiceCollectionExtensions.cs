using Cnet.Pipeline;
using Cnet.Polling;
using Cnet.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Cnet.DependencyInjection;

public static class CnetServiceCollectionExtensions
{
    public static CnetBuilder AddCnet(this IServiceCollection services, Action<CnetOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<CnetOptions>()
            .Configure(configure)
            .Validate(options => !string.IsNullOrWhiteSpace(options.BotToken), "CnetOptions.BotToken is required.")
            .Validate(options => options.UpdateQueueCapacity > 0, "CnetOptions.UpdateQueueCapacity must be positive.")
            .Validate(options => options.WorkerConcurrency > 0, "CnetOptions.WorkerConcurrency must be positive.")
            .ValidateOnStart();

        services
            .AddHttpClient("cnet-telegram")
            .AddTypedClient<ITelegramBotClient>((httpClient, provider) =>
            {
                var options = provider.GetRequiredService<IOptions<CnetOptions>>().Value;
                return new TelegramBotClient(new TelegramBotClientOptions(options.BotToken, options.ApiBaseUrl), httpClient);
            });

        services.TryAddSingleton<CnetClient>();
        services.TryAddSingleton<CnetRouter>();
        services.TryAddSingleton<IUpdateChannel, BoundedUpdateChannel>();
        services.TryAddScoped<UpdatePipeline>();
        services.AddHostedService<UpdateProcessorService>();

        return new CnetBuilder(services);
    }
}

public sealed class CnetBuilder(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;

    public CnetBuilder OnCommand(string command, Func<CommandContext, Task> handler)
    {
        Services.AddOptions<RouterRegistrations>().Configure(registrations =>
            registrations.Actions.Add(router => router.AddCommand(command, handler)));
        EnsureRouterConfigured();
        return this;
    }

    public CnetBuilder OnCallback(string prefix, Func<CallbackContext, Task> handler)
    {
        Services.AddOptions<RouterRegistrations>().Configure(registrations =>
            registrations.Actions.Add(router => router.AddCallback(prefix, handler)));
        EnsureRouterConfigured();
        return this;
    }

    public CnetBuilder OnMessage(Func<MessageContext, Task> handler)
    {
        Services.AddOptions<RouterRegistrations>().Configure(registrations =>
            registrations.Actions.Add(router => router.AddMessageHandler(handler)));
        EnsureRouterConfigured();
        return this;
    }

    public CnetBuilder OnUpdate(UpdateType type, Func<UpdateContext, Task> handler)
    {
        Services.AddOptions<RouterRegistrations>().Configure(registrations =>
            registrations.Actions.Add(router => router.AddUpdateHandler(type, handler)));
        EnsureRouterConfigured();
        return this;
    }

    public CnetBuilder Use<TMiddleware>()
        where TMiddleware : class, IUpdateMiddleware
    {
        Services.AddScoped<IUpdateMiddleware, TMiddleware>();
        return this;
    }

    public CnetBuilder UseReplayGuard()
    {
        Services.AddSingleton<ReplayGuardMiddleware>();
        Services.AddScoped<IUpdateMiddleware>(provider => provider.GetRequiredService<ReplayGuardMiddleware>());
        return this;
    }

    public CnetBuilder UsePolling()
    {
        Services.AddHostedService<PollingService>();
        return this;
    }

    private void EnsureRouterConfigured()
    {
        Services.RemoveAll<CnetRouter>();
        Services.TryAddSingleton(provider =>
        {
            var router = new CnetRouter();
            var registrations = provider.GetRequiredService<IOptions<RouterRegistrations>>().Value;
            foreach (var action in registrations.Actions)
            {
                action(router);
            }

            return router;
        });
    }
}

public sealed class RouterRegistrations
{
    public IList<Action<CnetRouter>> Actions { get; } = [];
}
