using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;

namespace Cnet.Pipeline;

public interface IUpdateChannel
{
    bool TryEnqueue(Update update);

    ValueTask EnqueueAsync(Update update, CancellationToken cancellationToken);

    ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken);

    bool TryDequeue(out Update update);

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

    public bool TryEnqueue(Update update) => _channel.Writer.TryWrite(update);

    public ValueTask EnqueueAsync(Update update, CancellationToken cancellationToken)
        => _channel.Writer.WriteAsync(update, cancellationToken);

    public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
        => _channel.Reader.WaitToReadAsync(cancellationToken);

    public bool TryDequeue(out Update update) => _channel.Reader.TryRead(out update!);

    public void Complete() => _channel.Writer.TryComplete();
}
