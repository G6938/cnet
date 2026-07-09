using Cnet.Pipeline;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Xunit;

namespace Cnet.Tests;

public sealed class UpdateChannelTests
{
    [Fact]
    public void Enqueue_Dequeue_Roundtrips()
    {
        var channel = new BoundedUpdateChannel(Options.Create(new CnetOptions { UpdateQueueCapacity = 10 }));

        Assert.True(channel.TryEnqueue(new Update { Id = 1 }));
        Assert.True(channel.TryDequeue(out var update));
        Assert.Equal(1, update.Id);
    }

    [Fact]
    public void FullQueue_DropsWrites()
    {
        var channel = new BoundedUpdateChannel(Options.Create(new CnetOptions { UpdateQueueCapacity = 1 }));

        Assert.True(channel.TryEnqueue(new Update { Id = 1 }));
        Assert.False(channel.TryEnqueue(new Update { Id = 2 }));
    }

    [Fact]
    public async Task Complete_EndsReading()
    {
        var channel = new BoundedUpdateChannel(Options.Create(new CnetOptions()));
        channel.Complete();

        Assert.False(await channel.WaitToReadAsync(CancellationToken.None));
    }
}
