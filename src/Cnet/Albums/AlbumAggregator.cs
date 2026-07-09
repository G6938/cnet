using Cnet.Pipeline;
using Cnet.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;

namespace Cnet.Albums;

public sealed class AlbumContext(
    IReadOnlyList<Message> messages,
    CnetClient client,
    IServiceProvider services,
    CancellationToken cancellationToken)
{
    public IReadOnlyList<Message> Messages { get; } = messages;

    public CnetClient Client { get; } = client;

    public IServiceProvider Services { get; } = services;

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public long ChatId => Messages[0].Chat.Id;

    public long UserId => Messages[0].From!.Id;

    public IReadOnlyList<int> MessageIds => [.. Messages.Select(message => message.Id)];
}

public sealed class MediaGroupMiddleware(IAlbumStore store, IOptions<CnetOptions> options) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext context, UpdateStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        if (context.Update.Message is { MediaGroupId.Length: > 0, From: not null } message)
        {
            await store.AddAsync(
                message,
                TimeSpan.FromMilliseconds(options.Value.AlbumFlushDelayMilliseconds),
                context.CancellationToken).ConfigureAwait(false);
            return;
        }

        await nextStep(context).ConfigureAwait(false);
    }
}

public sealed class AlbumFlushService(
    IAlbumStore store,
    IServiceScopeFactory scopeFactory,
    CnetRouter router,
    ILogger<AlbumFlushService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var due = await store.CollectDueAsync(stoppingToken).ConfigureAwait(false);
                foreach (var album in due)
                {
                    await FlushAsync(album, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.AlbumFlushFailed(exception);
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task FlushAsync(IReadOnlyList<Message> album, CancellationToken cancellationToken)
    {
        if (album.Count == 0)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<CnetClient>();
        var context = new AlbumContext(album, client, scope.ServiceProvider, cancellationToken);
        await router.RouteAlbumAsync(context).ConfigureAwait(false);
    }
}

internal static partial class AlbumFlushServiceLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Album flush failed")]
    internal static partial void AlbumFlushFailed(this ILogger logger, Exception exception);
}
