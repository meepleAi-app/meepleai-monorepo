using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Api.Observability;
using Api.Routing;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Routing;

/// <summary>
/// Issue #2123 — unit tests for the beacon's <c>ProcessBeacon</c> logic.
/// The Minimal-API endpoint lambda is exercised end-to-end via
/// WebApplicationFactory integration tests in a follow-up; this unit suite
/// covers the metric-emission and truncation contract.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class BggAttemptBeaconEndpointsTests
{
    [Fact]
    public void ProcessBeacon_WithFullPayload_IncrementsCounterWithPathTag()
    {
        using var capture = new BggAttemptMetricsCapture();
        var body = new BggAttemptBeaconRequest("https://cf.geekdo-images.com/x.jpg", "/shared-games", 12345);

        BggAttemptBeaconEndpoints.ProcessBeacon(body, NullLogger.Instance);

        capture.LongMeasurements.Should().ContainSingle(m =>
            m.Name == "meepleai.bgg.url.attempted_render.total" &&
            m.Value == 1 &&
            string.Equals(m.Tags["path"] as string, "/shared-games", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProcessBeacon_NullOrBlankPath_IncrementsWithUnknownTag(string? path)
    {
        using var capture = new BggAttemptMetricsCapture();
        var body = new BggAttemptBeaconRequest("https://cf.geekdo-images.com/x.jpg", path, null);

        BggAttemptBeaconEndpoints.ProcessBeacon(body, NullLogger.Instance);

        capture.LongMeasurements.Should().ContainSingle(m =>
            m.Name == "meepleai.bgg.url.attempted_render.total" &&
            string.Equals(m.Tags["path"] as string, "unknown", StringComparison.Ordinal));
    }

    [Fact]
    public void ProcessBeacon_NullBody_IncrementsWithUnknownTag()
    {
        using var capture = new BggAttemptMetricsCapture();

        BggAttemptBeaconEndpoints.ProcessBeacon(null, NullLogger.Instance);

        capture.LongMeasurements.Should().ContainSingle(m =>
            m.Name == "meepleai.bgg.url.attempted_render.total" &&
            string.Equals(m.Tags["path"] as string, "unknown", StringComparison.Ordinal));
    }

    [Fact]
    public void ProcessBeacon_OverlongPath_TruncatesToCardinalitySafeLength()
    {
        using var capture = new BggAttemptMetricsCapture();
        var longPath = "/" + new string('a', 1024);
        var body = new BggAttemptBeaconRequest(null, longPath, null);

        BggAttemptBeaconEndpoints.ProcessBeacon(body, NullLogger.Instance);

        var emission = capture.LongMeasurements.Single();
        var path = (string)emission.Tags["path"]!;
        path.Length.Should().BeLessThanOrEqualTo(257, "path tag is truncated to 256 chars + ellipsis to bound Prometheus cardinality");
        path.Should().StartWith("/aaaa");
    }

    private sealed class BggAttemptMetricsCapture : IDisposable
    {
        private readonly MeterListener _listener;
        public ConcurrentBag<(string Name, long Value, IReadOnlyDictionary<string, object?> Tags)> LongMeasurements { get; } = new();

        public BggAttemptMetricsCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == MeepleAiMetrics.MeterName &&
                        instrument.Name.StartsWith("meepleai.bgg.", StringComparison.Ordinal))
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                var dict = new Dictionary<string, object?>(tags.Length, StringComparer.Ordinal);
                foreach (var t in tags) dict[t.Key] = t.Value;
                LongMeasurements.Add((instrument.Name, measurement, dict));
            });
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }
}
