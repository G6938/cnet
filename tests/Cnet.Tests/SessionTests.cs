using Cnet.DependencyInjection;
using Cnet.Routing;
using Cnet.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using Xunit;

namespace Cnet.Tests;

public sealed class SessionTests
{
    [Fact]
    public async Task Session_StateAndData_Roundtrip()
    {
        var storage = new InMemorySessionStorage();
        var session = new Session(storage, "k1", CancellationToken.None);

        Assert.Null(await session.GetStateAsync());

        await session.SetStateAsync("waiting_name");
        await session.SetAsync("name", "parsa");
        await session.SetAsync("age", 30);

        Assert.Equal("waiting_name", await session.GetStateAsync());
        Assert.Equal("parsa", await session.GetAsync<string>("name"));
        Assert.Equal(30, await session.GetAsync<int>("age"));

        await session.ClearAsync();
        Assert.Null(await session.GetStateAsync());
    }

    [Fact]
    public async Task Storage_RespectsLifetime()
    {
        var storage = new InMemorySessionStorage();
        await storage.SetAsync("temp", "v", TimeSpan.FromMilliseconds(30));

        Assert.Equal("v", await storage.GetAsync("temp"));
        await Task.Delay(80);
        Assert.Null(await storage.GetAsync("temp"));
    }

    [Fact]
    public async Task Router_RoutesByState_BeforePlainHandlers()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddCnet(options => options.BotToken = "1000000000:t")
            .OnState("waiting_name", ctx =>
            {
                log.Add("state:" + ctx.Message.Text);
                return Task.CompletedTask;
            })
            .OnMessage(ctx =>
            {
                log.Add("plain:" + ctx.Message.Text);
                return Task.CompletedTask;
            });

        await using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<CnetRouter>();
        var client = TestKit.Client();
        var storage = provider.GetRequiredService<ISessionStorage>();

        var update = new Update
        {
            Id = 1,
            Message = new Message
            {
                Id = 1,
                Date = DateTime.UtcNow,
                Chat = new Chat { Id = 7, Type = Telegram.Bot.Types.Enums.ChatType.Private },
                From = new User { Id = 7, FirstName = "T" },
                Text = "parsa",
            },
        };

        await router.RouteAsync(new UpdateContext(update, client, provider, CancellationToken.None));

        var context = new UpdateContext(update, client, provider, CancellationToken.None);
        await context.Session().SetStateAsync("waiting_name");
        await router.RouteAsync(new UpdateContext(update, client, provider, CancellationToken.None));

        Assert.Equal(["plain:parsa", "state:parsa"], log);
        _ = storage;
    }
}
