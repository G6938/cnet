using Cnet.DependencyInjection;
using Cnet.Pipeline;
using Cnet.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cnet.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddCnet_RegistersCoreServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCnet(options => options.BotToken = "1000000000:token");

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<CnetClient>());
        Assert.NotNull(provider.GetRequiredService<IUpdateChannel>());
        Assert.NotNull(provider.GetRequiredService<CnetRouter>());
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<UpdatePipeline>());
    }

    [Fact]
    public async Task Builder_RegistersHandlers_IntoRouter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var handled = false;
        services
            .AddCnet(options => options.BotToken = "1000000000:token")
            .OnCommand("ping", _ =>
            {
                handled = true;
                return Task.CompletedTask;
            });

        using var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<CnetRouter>();
        var client = provider.GetRequiredService<CnetClient>();

        var update = new Telegram.Bot.Types.Update
        {
            Id = 1,
            Message = new Telegram.Bot.Types.Message
            {
                Id = 1,
                Date = DateTime.UtcNow,
                Chat = new Telegram.Bot.Types.Chat { Id = 1, Type = Telegram.Bot.Types.Enums.ChatType.Private },
                From = new Telegram.Bot.Types.User { Id = 1, FirstName = "T" },
                Text = "/ping",
            },
        };

        await router.RouteAsync(new UpdateContext(update, client, provider, CancellationToken.None));

        Assert.True(handled);
    }
}
