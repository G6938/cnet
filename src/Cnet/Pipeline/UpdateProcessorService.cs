using Cnet.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cnet.Pipeline;

public sealed class UpdateProcessorService(
    IUpdateChannel channel,
    IServiceScopeFactory scopeFactory,
    IOptions<CnetOptions> options,
    ILogger<UpdateProcessorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumers = new Task[options.Value.WorkerConcurrency];
        for (var i = 0; i < consumers.Length; i++)
        {
            consumers[i] = ConsumeAsync(stoppingToken);
        }

        await Task.WhenAll(consumers).ConfigureAwait(false);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        channel.Complete();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await channel.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
            {
                while (channel.TryDequeue(out var update))
                {
                    try
                    {
                        await using var scope = scopeFactory.CreateAsyncScope();
                        var pipeline = scope.ServiceProvider.GetRequiredService<UpdatePipeline>();
                        var client = scope.ServiceProvider.GetRequiredService<CnetClient>();
                        await pipeline.ExecuteAsync(new UpdateContext(update, client, scope.ServiceProvider, stoppingToken))
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        logger.UpdateProcessingFailed(exception, update.Id);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}

internal static partial class UpdateProcessorServiceLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Processing failed for update {UpdateId}")]
    internal static partial void UpdateProcessingFailed(this ILogger logger, Exception exception, int updateId);
}
