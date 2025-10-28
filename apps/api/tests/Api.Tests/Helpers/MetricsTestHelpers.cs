// Test helpers for OpenTelemetry metrics testing (replacement for non-existent package)
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;

namespace Api.Tests.Helpers;

/// <summary>
/// Test implementation of IMeterFactory for unit testing metrics.
/// </summary>
public class TestMeterFactory : IMeterFactory, IDisposable
{
    private readonly List<Meter> _meters = new();

    public Meter Create(MeterOptions options)
    {
        var meter = new Meter(options.Name, options.Version);
        _meters.Add(meter);
        return meter;
    }

    public void Dispose()
    {
        foreach (var meter in _meters)
        {
            meter.Dispose();
        }
        _meters.Clear();
    }
}

/// <summary>
/// Collects measurements from a specific metric for testing.
/// </summary>
public class MetricCollector<T> where T : struct
{
    private readonly List<Measurement<T>> _measurements = new();
    private readonly MeterListener _listener;

    public MetricCollector(IMeterFactory meterFactory, string metricName, string? tagKey = null)
    {
        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == metricName && instrument.Meter.Name == "MeepleAI.Api")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<T>((instrument, measurement, tags, state) =>
        {
            var tagList = new TagList();
            foreach (var tag in tags)
            {
                tagList.Add(tag);
            }
            _measurements.Add(new Measurement<T>(measurement, tagList));
        });

        _listener.Start();
    }

    public List<Measurement<T>> GetMeasurements() => _measurements.ToList();
}

/// <summary>
/// Represents a single measurement from a metric.
/// </summary>
public class Measurement<T> where T : struct
{
    public T Value { get; }
    public Dictionary<string, object?> Tags { get; }

    public Measurement(T value, TagList tags)
    {
        Value = value;
        Tags = new Dictionary<string, object?>();
        foreach (var tag in tags)
        {
            Tags[tag.Key] = tag.Value;
        }
    }
}
