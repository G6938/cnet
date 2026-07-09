using System.Diagnostics;
using Cnet.Pipeline;
using Cnet.Routing;

namespace Cnet.Metrics;

public sealed class MetricsMiddleware(CnetMeter meter) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext context, UpdateStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        meter.Received();
        var timestamp = Stopwatch.GetTimestamp();

        try
        {
            await nextStep(context).ConfigureAwait(false);
            meter.Processed(Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds);
        }
        catch
        {
            meter.Failed();
            throw;
        }
    }
}
