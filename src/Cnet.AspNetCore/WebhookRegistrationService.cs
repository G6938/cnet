using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace Cnet.AspNetCore;

public sealed class WebhookRegistrationService(
    CnetClient client,
    IOptions<CnetWebhookOptions> webhookOptions,
    IOptions<CnetOptions> cnetOptions,
    ILogger<WebhookRegistrationService> logger) : BackgroundService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = webhookOptions.Value;
        if (!options.AutoRegister)
        {
            return;
        }

        var url = new Uri(new Uri(options.PublicUrl!, UriKind.Absolute), options.Path);

        for (var attempt = 1; attempt <= MaxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await client.Raw.SetWebhook(
                    url.AbsoluteUri,
                    maxConnections: options.MaxConnections,
                    allowedUpdates: cnetOptions.Value.AllowedUpdates,
                    dropPendingUpdates: cnetOptions.Value.DropPendingUpdates,
                    secretToken: options.SecretToken,
                    cancellationToken: stoppingToken).ConfigureAwait(false);

                logger.WebhookRegistered(attempt);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (attempt < MaxAttempts)
            {
                logger.WebhookAttemptFailed(exception, attempt, MaxAttempts);
                await Task.Delay(RetryDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.WebhookFailedPermanently(exception, MaxAttempts);
                return;
            }
        }
    }
}

internal static partial class WebhookRegistrationServiceLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Telegram webhook registered on attempt {Attempt}")]
    internal static partial void WebhookRegistered(this ILogger logger, int attempt);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Webhook registration attempt {Attempt} of {MaxAttempts} failed")]
    internal static partial void WebhookAttemptFailed(this ILogger logger, Exception exception, int attempt, int maxAttempts);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Webhook registration failed after {MaxAttempts} attempts")]
    internal static partial void WebhookFailedPermanently(this ILogger logger, Exception exception, int maxAttempts);
}
