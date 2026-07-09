using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;

namespace Cnet.Pipeline;

public sealed class UpdateLease(Update update, Func<CancellationToken, ValueTask> onComplete)
{
    public Update Update { get; } = update;

    public ValueTask CompleteAsync(CancellationToken cancellationToken = default) => onComplete(cancellationToken);
}

public interface IUpdateChannel
{
    ValueTask EnqueueAsync(Update update, CancellationToken cancellationToken);

    ValueTask<UpdateLease?> DequeueAsync(CancellationToken cancellationToken);

    void Complete();
}

public sealed class BoundedUpdateChannel : IUpdateChannel
{
    private readonly Channel<Update> _channel;

    public BoundedUpdateChannel(IOptions<CnetOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _channel = Channel.CreateBounded<Update>(new BoundedChannelOptions(options.Value.UpdateQueueCapacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public ValueTask EnqueueAsync(Update update, CancellationToken cancellationToken)
        => _channel.Writer.WriteAsync(update, cancellationToken);

    public async ValueTask<UpdateLease?> DequeueAsync(CancellationToken cancellationToken)
    {
        try
        {
            var update = await _channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            return new UpdateLease(update, _ => ValueTask.CompletedTask);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    public void Complete() => _channel.Writer.TryComplete();
}
