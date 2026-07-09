using Cnet.Routing;

namespace Cnet.Pipeline;

public delegate Task UpdateStep(UpdateContext context);

public interface IUpdateMiddleware
{
    Task InvokeAsync(UpdateContext context, UpdateStep nextStep);
}

public interface IReplayGuard
{
    ValueTask<bool> TryRegisterAsync(int updateId, CancellationToken cancellationToken = default);
}

public sealed class UpdatePipeline
{
    private readonly UpdateStep _entryPoint;

    public UpdatePipeline(IEnumerable<IUpdateMiddleware> middlewares, CnetRouter router)
    {
        ArgumentNullException.ThrowIfNull(middlewares);
        ArgumentNullException.ThrowIfNull(router);

        _entryPoint = middlewares
            .Reverse()
            .Aggregate(
                (UpdateStep)(ctx => router.RouteAsync(ctx)),
                (nextStep, middleware) => ctx => middleware.InvokeAsync(ctx, nextStep));
    }

    public Task ExecuteAsync(UpdateContext context) => _entryPoint(context);
}

public sealed class InMemoryReplayGuard : IReplayGuard
{
    private const int MaxTrackedUpdates = 100000;

    private readonly HashSet<int> _seen = [];
    private readonly Queue<int> _order = new();
    private readonly Lock _gate = new();

    public ValueTask<bool> TryRegisterAsync(int updateId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_seen.Add(updateId))
            {
                return ValueTask.FromResult(false);
            }

            _order.Enqueue(updateId);
            if (_order.Count > MaxTrackedUpdates)
            {
                _seen.Remove(_order.Dequeue());
            }

            return ValueTask.FromResult(true);
        }
    }
}

public sealed class ReplayGuardMiddleware(IReplayGuard guard) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext context, UpdateStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        if (await guard.TryRegisterAsync(context.Update.Id, context.CancellationToken).ConfigureAwait(false))
        {
            await nextStep(context).ConfigureAwait(false);
        }
    }
}
