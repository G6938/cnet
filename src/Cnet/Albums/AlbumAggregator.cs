using System.Collections.Concurrent;
using Cnet.Pipeline;
using Cnet.Routing;
using Microsoft.Extensions.DependencyInjection;
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

public sealed class AlbumAggregator(
    IServiceScopeFactory scopeFactory,
    CnetRouter router,
    CnetClient client,
    IOptions<CnetOptions> options,
    ILogger<AlbumAggregator> logger)
{
    private readonly ConcurrentDictionary<string, List<Message>> _groups = new(StringComparer.Ordinal);

    public void Add(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.MediaGroupId is not { Length: > 0 } mediaGroupId || message.From is null)
        {
            return;
        }

        var groupKey = message.Chat.Id + ":" + mediaGroupId;
        var isFirst = false;

        var group = _groups.GetOrAdd(groupKey, _ =>
        {
            isFirst = true;
            return [];
        });

        lock (group)
        {
            group.Add(message);
        }

        if (isFirst)
        {
            _ = FlushAfterDelayAsync(groupKey);
        }
    }

    private async Task FlushAfterDelayAsync(string groupKey)
    {
        try
        {
            await Task.Delay(options.Value.AlbumFlushDelayMilliseconds).ConfigureAwait(false);

            if (!_groups.TryRemove(groupKey, out var group))
            {
                return;
            }

            List<Message> snapshot;
            lock (group)
            {
                snapshot = [.. group.OrderBy(message => message.Id)];
            }

            if (snapshot.Count == 0)
            {
                return;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var context = new AlbumContext(snapshot, client, scope.ServiceProvider, CancellationToken.None);
            await router.RouteAlbumAsync(context).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.AlbumFlushFailed(exception);
        }
    }
}

public sealed class MediaGroupMiddleware(AlbumAggregator aggregator) : IUpdateMiddleware
{
    public Task InvokeAsync(UpdateContext context, UpdateStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        if (context.Update.Message is { MediaGroupId.Length: > 0 } message)
        {
            aggregator.Add(message);
            return Task.CompletedTask;
        }

        return nextStep(context);
    }
}

internal static partial class AlbumAggregatorLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Album flush failed")]
    internal static partial void AlbumFlushFailed(this ILogger logger, Exception exception);
}
