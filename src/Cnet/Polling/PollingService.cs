using Cnet.Pipeline;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace Cnet.Polling;

public sealed class PollingService(
    CnetClient client,
    IUpdateChannel channel,
    IOptions<CnetOptions> options,
    ILogger<PollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var settings = options.Value;
        var offset = 0;

        try
        {
            await client.Raw.DeleteWebhook(settings.DropPendingUpdates, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.PollingCycleFailed(exception);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await client.Raw.GetUpdates(
                    offset,
                    limit: 100,
                    timeout: settings.PollingTimeoutSeconds,
                    allowedUpdates: settings.AllowedUpdates,
                    cancellationToken: stoppingToken).ConfigureAwait(false);

                foreach (var update in updates)
                {
                    offset = Math.Max(offset, update.Id + 1);
                    await channel.EnqueueAsync(update, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.PollingCycleFailed(exception);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }
}

internal static partial class PollingServiceLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Polling cycle failed")]
    internal static partial void PollingCycleFailed(this ILogger logger, Exception exception);
}
