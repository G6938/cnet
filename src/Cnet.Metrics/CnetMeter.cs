using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Cnet.Metrics;

public sealed class CnetMeter : IDisposable
{
    public const string MeterName = "Cnet";

    private readonly Meter _meter;
    private readonly Counter<long> _received;
    private readonly Counter<long> _processed;
    private readonly Counter<long> _failed;
    private readonly Histogram<double> _duration;

    public CnetMeter()
    {
        _meter = new Meter(MeterName, "1.0.0");
        _received = _meter.CreateCounter<long>("cnet.updates.received", unit: "{update}");
        _processed = _meter.CreateCounter<long>("cnet.updates.processed", unit: "{update}");
        _failed = _meter.CreateCounter<long>("cnet.updates.failed", unit: "{update}");
        _duration = _meter.CreateHistogram<double>("cnet.update.duration", unit: "ms");
    }

    public void Received() => _received.Add(1);

    public void Processed(double elapsedMilliseconds)
    {
        _processed.Add(1);
        _duration.Record(elapsedMilliseconds);
    }

    public void Failed() => _failed.Add(1);

    public void Dispose() => _meter.Dispose();
}
