using Cnet.Albums;
using Cnet.DependencyInjection;
using Cnet.Pipeline;
using Cnet.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Xunit;

namespace Cnet.Tests;

public sealed class AlbumAndErrorTests
{
    private static Message MediaMessage(int id, string groupId) => new()
    {
        Id = id,
        Date = DateTime.UtcNow,
        Chat = new Chat { Id = 9, Type = Telegram.Bot.Types.Enums.ChatType.Private },
        From = new User { Id = 9, FirstName = "T" },
        MediaGroupId = groupId,
    };

    [Fact]
    public async Task Aggregator_MergesMediaGroup_IntoSingleAlbum()
    {
        var albums = new List<AlbumContext>();
        var router = new CnetRouter();
        router.AddAlbumHandler(context =>
        {
            albums.Add(context);
            return Task.CompletedTask;
        });

        await using var provider = new ServiceCollection().BuildServiceProvider();
        var aggregator = new AlbumAggregator(
            provider.GetRequiredService<IServiceScopeFactory>(),
            router,
            TestKit.Client(),
            Options.Create(TestKit.Options(options => options.AlbumFlushDelayMilliseconds = 100)),
            NullLogger<AlbumAggregator>.Instance);

        aggregator.Add(MediaMessage(11, "g1"));
        aggregator.Add(MediaMessage(10, "g1"));

        await Task.Delay(400);

        var album = Assert.Single(albums);
        Assert.Equal([10, 11], album.MessageIds);
        Assert.Equal(9, album.ChatId);
    }

    [Fact]
    public async Task MediaGroupMiddleware_SwallowsGroupMessages()
    {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var aggregator = new AlbumAggregator(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new CnetRouter(),
            TestKit.Client(),
            Options.Create(TestKit.Options()),
            NullLogger<AlbumAggregator>.Instance);
        var middleware = new MediaGroupMiddleware(aggregator);
        var passed = 0;

        Task Next(UpdateContext _)
        {
            passed++;
            return Task.CompletedTask;
        }

        await middleware.InvokeAsync(
            new UpdateContext(new Update { Id = 1, Message = MediaMessage(1, "g2") }, TestKit.Client(), provider, CancellationToken.None),
            Next);
        await middleware.InvokeAsync(
            new UpdateContext(new Update { Id = 2 }, TestKit.Client(), provider, CancellationToken.None),
            Next);

        Assert.Equal(1, passed);
    }

    [Fact]
    public async Task ErrorHook_ReceivesHandlerExceptions()
    {
        var errors = new List<ErrorContext>();
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddCnet(options => options.BotToken = "1000000000:t")
            .OnCommand("boom", _ => throw new InvalidOperationException("handler exploded"))
            .OnError(context =>
            {
                errors.Add(context);
                return Task.CompletedTask;
            });
        services.AddSingleton(TestKit.Client());

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetServices<IHostedService>().OfType<UpdateProcessorService>().Single();
        var channel = provider.GetRequiredService<IUpdateChannel>();

        await processor.StartAsync(CancellationToken.None);
        channel.TryEnqueue(new Update
        {
            Id = 1,
            Message = new Message
            {
                Id = 1,
                Date = DateTime.UtcNow,
                Chat = new Chat { Id = 1, Type = Telegram.Bot.Types.Enums.ChatType.Private },
                From = new User { Id = 1, FirstName = "T" },
                Text = "/boom",
            },
        });

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (errors.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        await processor.StopAsync(CancellationToken.None);

        var error = Assert.Single(errors);
        Assert.IsType<InvalidOperationException>(error.Exception);
        Assert.Equal(1, error.Update.Update.Id);
    }
}
