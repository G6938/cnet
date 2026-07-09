using Cnet.Pipeline;
using Cnet.Polling;
using Cnet.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Xunit;

namespace Cnet.Tests;

public sealed class PollingAndTextTests
{
    [Fact]
    public async Task Polling_EnqueuesUpdates_AndAdvancesOffset()
    {
        var served = 0;
        var offsets = new List<int>();
        var bot = new FakeBotClient();
        bot.Responder = request =>
        {
            if (request is GetUpdatesRequest getUpdates)
            {
                offsets.Add(getUpdates.Offset ?? 0);
                if (served == 0)
                {
                    served++;
                    return new[] { new Update { Id = 100 }, new Update { Id = 101 } };
                }

                Thread.Sleep(20);
                return Array.Empty<Update>();
            }

            return null;
        };

        var options = Options.Create(TestKit.Options(o => o.PollingTimeoutSeconds = 0));
        var channel = new BoundedUpdateChannel(options);
        using var service = new PollingService(TestKit.Client(bot), channel, options, NullLogger<PollingService>.Instance);

        await service.StartAsync(CancellationToken.None);

        var received = new List<int>();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (received.Count < 2 && DateTime.UtcNow < deadline)
        {
            while (channel.TryDequeue(out var update))
            {
                received.Add(update.Id);
            }

            await Task.Delay(20);
        }

        await service.StopAsync(CancellationToken.None);

        Assert.Equal([100, 101], received);
        Assert.Contains(102, offsets);
    }

    [Fact]
    public void TextCatalog_FallsBack_AndFormats()
    {
        var catalog = new TextCatalog { FallbackLocale = "en" };
        catalog
            .Add("en", "hello", "Hello {0}!")
            .Add("fa", "hello", "سلام {0}!");

        Assert.Equal("سلام parsa!", catalog.Get("fa", "hello", "parsa"));
        Assert.Equal("Hello parsa!", catalog.Get("de", "hello", "parsa"));
        Assert.Equal("missing.key", catalog.Get("fa", "missing.key"));
    }
}
