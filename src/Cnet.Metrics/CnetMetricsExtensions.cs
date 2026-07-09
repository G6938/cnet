using Cnet.DependencyInjection;
using Cnet.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cnet.Metrics;

public static class CnetMetricsExtensions
{
    public static CnetBuilder UseMetrics(this CnetBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<CnetMeter>();
        builder.Services.AddScoped<IUpdateMiddleware, MetricsMiddleware>();
        return builder;
    }
}
