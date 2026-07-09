using Cnet.Albums;
using Cnet.Pipeline;
using Cnet.Polling;
using Cnet.Routing;
using Cnet.Sessions;
using Cnet.Text;
using Cnet.Throttling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                AutomaticDecompression = System.Net.DecompressionMethods.All,
            })
            .AddTypedClient<ITelegramBotClient>((httpClient, provider) =>
            {
                var options = provider.GetRequiredService<IOptions<CnetOptions>>().Value;
                return new TelegramBotClient(new TelegramBotClientOptions(options.BotToken, options.ApiBaseUrl), httpClient);
            });

        services.TryAddSingleton<CnetClient>();
        services.TryAddSingleton<CnetRouter>();
        services.TryAddSingleton<OutboundThrottle>();
        services.TryAddSingleton<ISessionStorage, InMemorySessionStorage>();
        services.TryAddSingleton<IUpdateChannel, BoundedUpdateChannel>();
        services.TryAddScoped<UpdatePipeline>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, UpdateProcessorService>());

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

    public CnetBuilder OnCommand<THandler>()
        where THandler : class, ICommandHandler
    {
        Services.AddScoped<THandler>();
        return OnCommand(THandler.Command, context =>
            context.Services.GetRequiredService<THandler>().HandleAsync(context));
    }

    public CnetBuilder OnCallback<THandler>()
        where THandler : class, ICallbackHandler
    {
        Services.AddScoped<THandler>();
        return OnCallback(THandler.Prefix, context =>
            context.Services.GetRequiredService<THandler>().HandleAsync(context));
    }

    public CnetBuilder OnMessage<THandler>()
        where THandler : class, IMessageHandler
    {
        Services.AddScoped<THandler>();
        return OnMessage(context =>
            context.Services.GetRequiredService<THandler>().HandleAsync(context));
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
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, PollingService>());
        return this;
    }

    public CnetBuilder OnError(Func<ErrorContext, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Services.AddOptions<ErrorHandlers>().Configure(handlers => handlers.Handlers.Add(handler));
        return this;
    }

    public CnetBuilder OnMessage(Func<Telegram.Bot.Types.Message, bool> filter, Func<MessageContext, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(handler);
        return OnMessage(context => filter(context.Message) ? handler(context) : Task.CompletedTask);
    }

    public CnetBuilder OnState(string state, Func<MessageContext, Task> handler)
    {
        Services.AddOptions<RouterRegistrations>().Configure(registrations =>
            registrations.Actions.Add(router => router.AddStateHandler(state, handler)));
        EnsureRouterConfigured();
        return this;
    }

    public CnetBuilder OnAlbum(Func<AlbumContext, Task> handler)
    {
        Services.AddOptions<RouterRegistrations>().Configure(registrations =>
            registrations.Actions.Add(router => router.AddAlbumHandler(handler)));
        EnsureRouterConfigured();
        Services.TryAddSingleton<AlbumAggregator>();
        Services.AddScoped<IUpdateMiddleware, MediaGroupMiddleware>();
        return this;
    }

    public CnetBuilder UseRateLimit(int updatesPerMinutePerUser)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(updatesPerMinutePerUser, 1);
        Services.TryAddSingleton(new InboundRateLimitState(updatesPerMinutePerUser));
        Services.AddScoped<IUpdateMiddleware, InboundRateLimitMiddleware>();
        return this;
    }

    public CnetBuilder AddTexts(Action<TextCatalog> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var catalog = new TextCatalog();
        configure(catalog);
        Services.TryAddSingleton(catalog);
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
