using System.Diagnostics.Metrics;
using Api.Observability;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Observability;

/// <summary>
/// Unit tests for the native SSE stream observability metrics (Issue #2561 SP2 T12, AC-OBS-1):
///   - <c>meepleai.live_session.sse.active_connections</c> (ObservableGauge&lt;int&gt;)
///   - <c>meepleai.live_session.sse.reconnect.total</c> (Counter&lt;long&gt;)
/// </summary>
/// <remarks>
/// Pattern mirrors <see cref="WikidataEnrichmentMetricsTests"/>:
/// MeterListener-based capture for counters; ReadObservableGaugeValue helper for the gauge.
/// Tests are structured as: instrument names, increment/decrement semantics, reconnect counter.
/// Isolation: each test resets the active-connections gauge to a known baseline via the
/// internal test helper <c>ResetLiveSseActiveConnections</c>; this avoids order-dependence
/// between tests that happen to run in the same process.
/// </remarks>
[Collection("SseMetrics")]
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Observability")]
[Trait("Issue", "2561")]
public sealed class SseMetricsTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // Active-connections gauge: naming
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "LiveSseActiveConnections metric name matches AC-OBS-1 contract")]
    public void LiveSseActiveConnections_Name_MatchesAcObs1Contract()
    {
        MeepleAiMetrics.LiveSseActiveConnections.Name
            .Should().Be("meepleai.live_session.sse.active_connections");
        MeepleAiMetrics.LiveSseActiveConnections.Unit.Should().Be("connections");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Active-connections gauge: increment / decrement semantics
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "IncrementLiveSseActiveConnections increases gauge by 1")]
    public void IncrementLiveSseActiveConnections_IncreasesGaugeByOne()
    {
        MeepleAiMetrics.ResetLiveSseActiveConnections(0);

        MeepleAiMetrics.IncrementLiveSseActiveConnections();

        ReadObservableGaugeValue(MeepleAiMetrics.LiveSseActiveConnections).Should().Be(1);
    }

    [Fact(DisplayName = "DecrementLiveSseActiveConnections decreases gauge by 1 (connect then disconnect)")]
    public void DecrementLiveSseActiveConnections_DecreasesGaugeByOne_AfterPriorIncrement()
    {
        MeepleAiMetrics.ResetLiveSseActiveConnections(0);

        // Simulate: client connects (+1) then disconnects (-1)
        MeepleAiMetrics.IncrementLiveSseActiveConnections();
        MeepleAiMetrics.DecrementLiveSseActiveConnections();

        ReadObservableGaugeValue(MeepleAiMetrics.LiveSseActiveConnections).Should().Be(0);
    }

    [Fact(DisplayName = "Gauge tracks N concurrent connections correctly")]
    public void Gauge_TracksMultipleConcurrentConnections()
    {
        MeepleAiMetrics.ResetLiveSseActiveConnections(0);

        // 3 concurrent connections open
        MeepleAiMetrics.IncrementLiveSseActiveConnections();
        MeepleAiMetrics.IncrementLiveSseActiveConnections();
        MeepleAiMetrics.IncrementLiveSseActiveConnections();
        ReadObservableGaugeValue(MeepleAiMetrics.LiveSseActiveConnections).Should().Be(3);

        // 1 disconnects
        MeepleAiMetrics.DecrementLiveSseActiveConnections();
        ReadObservableGaugeValue(MeepleAiMetrics.LiveSseActiveConnections).Should().Be(2);

        // Remaining 2 disconnect
        MeepleAiMetrics.DecrementLiveSseActiveConnections();
        MeepleAiMetrics.DecrementLiveSseActiveConnections();
        ReadObservableGaugeValue(MeepleAiMetrics.LiveSseActiveConnections).Should().Be(0);
    }

    [Fact(DisplayName = "LiveSseActiveConnections gauge is observable via MeterListener")]
    public void LiveSseActiveConnections_IsObservableViaMeterListener()
    {
        MeepleAiMetrics.ResetLiveSseActiveConnections(5);

        var collected = new List<int>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument == MeepleAiMetrics.LiveSseActiveConnections)
                    l.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<int>((_, value, _, _) => collected.Add(value));
        listener.Start();
        listener.RecordObservableInstruments();

        collected.Should().ContainSingle().Which.Should().Be(5);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Reconnect counter: naming
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "LiveSseReconnectTotal metric name matches AC-OBS-1 contract")]
    public void LiveSseReconnectTotal_Name_MatchesAcObs1Contract()
    {
        MeepleAiMetrics.LiveSseReconnectTotal.Name
            .Should().Be("meepleai.live_session.sse.reconnect.total");
        MeepleAiMetrics.LiveSseReconnectTotal.Unit.Should().Be("reconnects");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Reconnect counter: increment via MeterListener
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "RecordLiveSseReconnect increments counter and is observable via MeterListener")]
    public void RecordLiveSseReconnect_IncrementsCounterAndIsObservable()
    {
        // Capture increments fired by OUR calls only (shared global counter — tolerant assertion).
        var measurements = new List<long>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == "meepleai.live_session.sse.reconnect.total")
                    l.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => measurements.Add(value));
        listener.Start();

        MeepleAiMetrics.RecordLiveSseReconnect();
        MeepleAiMetrics.RecordLiveSseReconnect();

        // The reconnect counter is ONLY ever incremented by Add(1) (RecordLiveSseReconnect),
        // so every measurement the listener captures must be exactly 1L — even if a concurrent
        // test on the same global counter contributes more. OnlyContain(== 1L) catches an
        // accidental Add(2)/Add(N) regression that the previous Contain(1L) check could not.
        // Count delta: our two calls must contribute at least two measurements.
        measurements.Should().HaveCountGreaterThanOrEqualTo(2,
            "our two RecordLiveSseReconnect calls each emit one measurement")
            .And.OnlyContain(v => v == 1L,
            "the reconnect counter must only ever be incremented by Add(1)");
    }

    [Fact(DisplayName = "Both live-SSE metrics expose a non-empty Description for observability dashboards")]
    public void BothMetrics_HaveNonEmptyDescription()
    {
        // A NotBeNull() assertion on a `static readonly` field is vacuous — it is initialized at
        // type load and can never be null. Assert the real observability contract instead: every
        // metric must carry a human-readable Description so Grafana/SLO dashboards stay legible.
        MeepleAiMetrics.LiveSseReconnectTotal.Description.Should().NotBeNullOrWhiteSpace();
        MeepleAiMetrics.LiveSseActiveConnections.Description.Should().NotBeNullOrWhiteSpace();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static int ReadObservableGaugeValue(ObservableGauge<int> gauge)
    {
        var collected = new List<int>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument == gauge)
                    l.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<int>((_, value, _, _) => collected.Add(value));
        listener.Start();
        listener.RecordObservableInstruments();

        collected.Should().HaveCountGreaterThanOrEqualTo(1,
            "ObservableGauge must report at least one measurement on RecordObservableInstruments()");
        return collected[^1];
    }
}
