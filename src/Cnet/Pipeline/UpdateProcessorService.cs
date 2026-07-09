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
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var client = scope.ServiceProvider.GetRequiredService<CnetClient>();
                    var context = new UpdateContext(update, client, scope.ServiceProvider, stoppingToken);

                    try
                    {
                        var pipeline = scope.ServiceProvider.GetRequiredService<UpdatePipeline>();
                        await pipeline.ExecuteAsync(context).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        logger.UpdateProcessingFailed(exception, update.Id);
                        await InvokeErrorHandlersAsync(scope.ServiceProvider, exception, context).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task InvokeErrorHandlersAsync(IServiceProvider services, Exception exception, UpdateContext context)
    {
        var handlers = services.GetService<IOptions<ErrorHandlers>>()?.Value.Handlers;
        if (handlers is not { Count: > 0 })
        {
            return;
        }

        var errorContext = new ErrorContext(exception, context);
        foreach (var handler in handlers)
        {
            try
            {
                await handler(errorContext).ConfigureAwait(false);
            }
            catch (Exception handlerException)
            {
                logger.ErrorHandlerFailed(handlerException);
            }
        }
    }
}

internal static partial class UpdateProcessorServiceLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Processing failed for update {UpdateId}")]
    internal static partial void UpdateProcessingFailed(this ILogger logger, Exception exception, int updateId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "A registered error handler threw")]
    internal static partial void ErrorHandlerFailed(this ILogger logger, Exception exception);
}
