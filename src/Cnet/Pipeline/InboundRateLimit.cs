using System.Collections.Concurrent;
using Cnet.Routing;

namespace Cnet.Pipeline;

public sealed class InboundRateLimitState(int updatesPerMinutePerUser)
{
    private readonly ConcurrentDictionary<long, (long WindowIndex, int Count)> _counters = new();

    public int Limit { get; } = updatesPerMinutePerUser;

    public bool TryRegister(long userId)
    {
        var windowIndex = Environment.TickCount64 / 60000;

        while (true)
        {
            if (!_counters.TryGetValue(userId, out var current))
            {
                if (_counters.TryAdd(userId, (windowIndex, 1)))
                {
                    return true;
                }

                continue;
            }

            var next = current.WindowIndex == windowIndex
                ? (windowIndex, current.Count + 1)
                : (windowIndex, 1);

            if (_counters.TryUpdate(userId, next, current))
            {
                return next.Item2 <= Limit;
            }
        }
    }
}

public sealed class InboundRateLimitMiddleware(InboundRateLimitState state) : IUpdateMiddleware
{
    public Task InvokeAsync(UpdateContext context, UpdateStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        if (context.FromId is long userId && !state.TryRegister(userId))
        {
            return Task.CompletedTask;
        }

        return nextStep(context);
    }
}
