using Cnet.Pipeline;
using Cnet.Routing;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using Xunit;

namespace Cnet.Tests;

public sealed class PipelineTests
{
    private static readonly CnetClient Client = TestKit.Client();

    private static readonly IServiceProvider Services = new ServiceCollection().BuildServiceProvider();

    private static UpdateContext Context(int updateId)
        => new(new Update { Id = updateId }, Client, Services, CancellationToken.None);

    private sealed class RecordingMiddleware(string name, List<string> log) : IUpdateMiddleware
    {
        public Task InvokeAsync(UpdateContext context, UpdateStep nextStep)
        {
            log.Add(name);
            return nextStep(context);
        }
    }

    [Fact]
    public async Task Middlewares_RunInRegistrationOrder()
    {
        var log = new List<string>();
        var pipeline = new UpdatePipeline(
            [new RecordingMiddleware("first", log), new RecordingMiddleware("second", log)],
            new CnetRouter());

        await pipeline.ExecuteAsync(Context(1));

        Assert.Equal(["first", "second"], log);
    }

    [Fact]
    public async Task ReplayGuard_BlocksDuplicateUpdateIds()
    {
        var log = new List<string>();
        var guard = new ReplayGuardMiddleware();
        var pipeline = new UpdatePipeline([guard, new RecordingMiddleware("inner", log)], new CnetRouter());

        await pipeline.ExecuteAsync(Context(7));
        await pipeline.ExecuteAsync(Context(7));
        await pipeline.ExecuteAsync(Context(8));

        Assert.Equal(["inner", "inner"], log);
    }
}
