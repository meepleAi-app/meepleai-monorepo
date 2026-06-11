using System.Diagnostics.Metrics;
using Api.Observability;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Observability;

/// <summary>
/// Unit tests for the Wave 3 M11 Wikidata enrichment metric additions
/// (<see cref="MeepleAiMetrics.WikidataQueueDepth"/> + <see cref="MeepleAiMetrics.WikidataBatchDuration"/>).
/// Issue #1823 Wave 3 M11.
/// </summary>
[Collection("WikidataMetrics")]
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Observability")]
[Trait("Issue", "1823")]
public class WikidataEnrichmentMetricsTests
{
    [Fact]
    public void SetWikidataQueueDepth_PositiveValue_GaugeReportsSameValue()
    {
        MeepleAiMetrics.SetWikidataQueueDepth(42);

        var observed = ReadObservableGaugeValue(MeepleAiMetrics.WikidataQueueDepth);

        observed.Should().Be(42);
    }

    [Fact]
    public void SetWikidataQueueDepth_Zero_GaugeReports0()
    {
        MeepleAiMetrics.SetWikidataQueueDepth(0);

        ReadObservableGaugeValue(MeepleAiMetrics.WikidataQueueDepth).Should().Be(0);
    }

    [Fact]
    public void SetWikidataQueueDepth_Negative_ClampedToZero()
    {
        MeepleAiMetrics.SetWikidataQueueDepth(-5);

        ReadObservableGaugeValue(MeepleAiMetrics.WikidataQueueDepth)
            .Should().Be(0, "negative queue depth is non-sensical — the setter MUST clamp");
    }

    [Fact]
    public void WikidataBatchDuration_RecordsValue_AndIsCollectable()
    {
        var measurements = new List<double>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == "meepleai.wikidata.batch_duration_seconds")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<double>((_, value, _, _) => measurements.Add(value));
        listener.Start();

        MeepleAiMetrics.WikidataBatchDuration.Record(1.23);
        MeepleAiMetrics.WikidataBatchDuration.Record(4.56);

        // Tolerant assertion: any concurrent test or scheduler tick may have
        // recorded additional values on the shared global histogram between
        // listener.Start() and the assertion. We only care that OUR two
        // measurements made it through the MeterListener wiring.
        measurements.Should().Contain(1.23).And.Contain(4.56);
    }

    [Fact]
    public void WikidataBatchDuration_Name_MatchesAdrConvention()
    {
        // ADR DEC-3g locks the histogram name; future renames must update the
        // ADR + ops dashboards in lockstep — this test surfaces the dependency.
        MeepleAiMetrics.WikidataBatchDuration.Name
            .Should().Be("meepleai.wikidata.batch_duration_seconds");
        MeepleAiMetrics.WikidataBatchDuration.Unit.Should().Be("s");
    }

    [Fact]
    public void WikidataQueueDepth_Name_MatchesAdrConvention()
    {
        MeepleAiMetrics.WikidataQueueDepth.Name.Should().Be("meepleai.wikidata.queue_depth");
        MeepleAiMetrics.WikidataQueueDepth.Unit.Should().Be("games");
    }

    // ──────────────────────────────────────────────────────────────────────
    // F1 — dead_letter_count gauge (#1823 Wave 3 F1)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void SetWikidataDeadLetterCount_PositiveValue_GaugeReportsSameValue()
    {
        MeepleAiMetrics.SetWikidataDeadLetterCount(17);

        ReadObservableGaugeValue(MeepleAiMetrics.WikidataDeadLetterCount).Should().Be(17);
    }

    [Fact]
    public void SetWikidataDeadLetterCount_Negative_ClampedToZero()
    {
        MeepleAiMetrics.SetWikidataDeadLetterCount(-3);

        ReadObservableGaugeValue(MeepleAiMetrics.WikidataDeadLetterCount)
            .Should().Be(0, "negative dead-letter counts are non-sensical — the setter MUST clamp");
    }

    [Fact]
    public void IncrementWikidataDeadLetterCount_FromAnchor_IncreasesByOnePerCall()
    {
        // Anchor to a known value, then increment twice; the gauge MUST report
        // anchor+2 (atomic Interlocked.Increment, no Read-Modify-Write race).
        MeepleAiMetrics.SetWikidataDeadLetterCount(10);
        MeepleAiMetrics.IncrementWikidataDeadLetterCount();
        MeepleAiMetrics.IncrementWikidataDeadLetterCount();

        ReadObservableGaugeValue(MeepleAiMetrics.WikidataDeadLetterCount).Should().Be(12);
    }

    [Fact]
    public void WikidataDeadLetterCount_Name_MatchesAdrConvention()
    {
        // ADR DEC-3g + F1 follow-up: dashboard query string lives downstream.
        MeepleAiMetrics.WikidataDeadLetterCount.Name
            .Should().Be("meepleai.wikidata.dead_letter_count");
        MeepleAiMetrics.WikidataDeadLetterCount.Unit.Should().Be("attempts");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    private static int ReadObservableGaugeValue(ObservableGauge<int> gauge)
    {
        var collected = new List<int>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument == gauge)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<int>((_, value, _, _) => collected.Add(value));
        listener.Start();
        listener.RecordObservableInstruments();

        collected.Should().HaveCountGreaterThanOrEqualTo(1);
        return collected[^1];
    }
}
