using Cnet.DependencyInjection;
using Cnet.Routing;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using Xunit;

namespace Cnet.Tests;

public sealed class ClassHandlerTests
{
    private sealed class Recorder
    {
        public List<string> Entries { get; } = [];
    }

    private sealed class PingHandler(Recorder recorder) : ICommandHandler
    {
        public static string Command => "ping";

        public Task HandleAsync(CommandContext context)
        {
            recorder.Entries.Add("ping:" + context.Arguments);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockHandler(Recorder recorder) : ICallbackHandler
    {
        public static string Prefix => "blk:";

        public Task HandleAsync(CallbackContext context)
        {
            recorder.Entries.Add("blk:" + context.Payload);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ClassHandlers_ResolveFromScope_AndRoute()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Recorder>();
        services
            .AddCnet(options => options.BotToken = "1000000000:token")
            .OnCommand<PingHandler>()
            .OnCallback<BlockHandler>();

        await using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<CnetRouter>();
        var client = provider.GetRequiredService<CnetClient>();
        var recorder = provider.GetRequiredService<Recorder>();

        await using (var scope = provider.CreateAsyncScope())
        {
            await router.RouteAsync(new UpdateContext(
                new Update
                {
                    Id = 1,
                    Message = new Message
                    {
                        Id = 1,
                        Date = DateTime.UtcNow,
                        Chat = new Chat { Id = 1, Type = Telegram.Bot.Types.Enums.ChatType.Private },
                        From = new User { Id = 1, FirstName = "T" },
                        Text = "/ping hello",
                    },
                },
                client,
                scope.ServiceProvider,
                CancellationToken.None));
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            await router.RouteAsync(new UpdateContext(
                new Update
                {
                    Id = 2,
                    CallbackQuery = new CallbackQuery
                    {
                        Id = "cb",
                        From = new User { Id = 1, FirstName = "T" },
                        Data = "blk:42",
                        ChatInstance = "ci",
                    },
                },
                client,
                scope.ServiceProvider,
                CancellationToken.None));
        }

        Assert.Equal(["ping:hello", "blk:42"], recorder.Entries);
    }
}
