using Cnet.Routing;

namespace Cnet.Pipeline;

public delegate Task UpdateStep(UpdateContext context);

public interface IUpdateMiddleware
{
    Task InvokeAsync(UpdateContext context, UpdateStep nextStep);
}

public sealed class UpdatePipeline(IEnumerable<IUpdateMiddleware> middlewares, CnetRouter router)
{
    public Task ExecuteAsync(UpdateContext context)
    {
        var pipeline = middlewares
            .Reverse()
            .Aggregate(
                (UpdateStep)(ctx => router.RouteAsync(ctx)),
                (nextStep, middleware) => ctx => middleware.InvokeAsync(ctx, nextStep));

        return pipeline(context);
    }
}

public sealed class ReplayGuardMiddleware : IUpdateMiddleware
{
    private const int MaxTrackedUpdates = 100000;

    private readonly HashSet<int> _seen = [];
    private readonly Queue<int> _order = new();
    private readonly Lock _gate = new();

    public Task InvokeAsync(UpdateContext context, UpdateStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        lock (_gate)
        {
            if (!_seen.Add(context.Update.Id))
            {
                return Task.CompletedTask;
            }

            _order.Enqueue(context.Update.Id);
            if (_order.Count > MaxTrackedUpdates)
            {
                _seen.Remove(_order.Dequeue());
            }
        }

        return nextStep(context);
    }
}
