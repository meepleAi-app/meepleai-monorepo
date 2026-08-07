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

    // #3383: il test IncrementWikidataDeadLetterCount_FromAnchor_IncreasesByOnePerCall è stato
    // RIMOSSO insieme al metodo che copriva. Il gauge è ora puramente DB-derivato: l'unico punto
    // di scrittura è SetWikidataDeadLetterCount, coperto dai due test qui sopra.

    [Fact]
    public void WikidataDeadLetterCount_Name_MatchesAdrConvention()
    {
        // ADR DEC-3g + F1 follow-up: dashboard query string lives downstream.
        MeepleAiMetrics.WikidataDeadLetterCount.Name
            .Should().Be("meepleai.wikidata.dead_letter_count");
        MeepleAiMetrics.WikidataDeadLetterCount.Unit.Should().Be("attempts");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Issue #2470 — SSE plane (subscribers + publish/receive + admin clients)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void WikidataSseSubscribers_BeforeCallbackSet_GaugeReports0()
    {
        MeepleAiMetrics.ResetWikidataSseSubscribersCallback();

        ReadObservableGaugeValue(MeepleAiMetrics.WikidataSseSubscribers)
            .Should().Be(0, "the gauge degrades to 0 when no broadcaster callback is bound");
    }

    [Fact]
    public void WikidataSseSubscribers_AfterCallbackSet_GaugeReportsCallbackValue()
    {
        try
        {
            MeepleAiMetrics.SetWikidataSseSubscribersCallback(() => 7);

            ReadObservableGaugeValue(MeepleAiMetrics.WikidataSseSubscribers).Should().Be(7);
        }
        finally
        {
            MeepleAiMetrics.ResetWikidataSseSubscribersCallback();
        }
    }

    [Fact]
    public void WikidataSseSubscribers_CallbackThrows_GaugeDegradesTo0()
    {
        try
        {
            MeepleAiMetrics.SetWikidataSseSubscribersCallback(() => throw new InvalidOperationException("broadcaster torn down"));

            ReadObservableGaugeValue(MeepleAiMetrics.WikidataSseSubscribers)
                .Should().Be(0, "scrape MUST NOT throw — a thrown callback is treated as 0");
        }
        finally
        {
            MeepleAiMetrics.ResetWikidataSseSubscribersCallback();
        }
    }

    [Fact]
    public void SetWikidataSseSubscribersCallback_Null_Throws()
    {
        var act = () => MeepleAiMetrics.SetWikidataSseSubscribersCallback(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WikidataSseAdminClientsConnected_BeforeCallbackSet_GaugeReports0()
    {
        MeepleAiMetrics.ResetWikidataSseAdminClientsConnectedCallback();

        ReadObservableGaugeValue(MeepleAiMetrics.WikidataSseAdminClientsConnected).Should().Be(0);
    }

    [Fact]
    public void WikidataSseAdminClientsConnected_AfterCallbackSet_GaugeReportsCallbackValue()
    {
        try
        {
            MeepleAiMetrics.SetWikidataSseAdminClientsConnectedCallback(() => 3);

            ReadObservableGaugeValue(MeepleAiMetrics.WikidataSseAdminClientsConnected).Should().Be(3);
        }
        finally
        {
            MeepleAiMetrics.ResetWikidataSseAdminClientsConnectedCallback();
        }
    }

    [Fact]
    public void SetWikidataSseAdminClientsConnectedCallback_Null_Throws()
    {
        var act = () => MeepleAiMetrics.SetWikidataSseAdminClientsConnectedCallback(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WikidataSseMessagesPublished_Name_MatchesIssue2470Contract()
    {
        // PR review / Grafana panel 10 + alert WikidataSseSubscriberStarvation
        // depend on this exact metric name; renaming requires updating the
        // dashboard JSON + the alert YAML in lockstep.
        MeepleAiMetrics.WikidataSseMessagesPublished.Name
            .Should().Be("meepleai.wikidata.sse.messages.published.total");
        MeepleAiMetrics.WikidataSseMessagesPublished.Unit.Should().Be("messages");
    }

    [Fact]
    public void WikidataSseMessagesReceived_Name_MatchesIssue2470Contract()
    {
        MeepleAiMetrics.WikidataSseMessagesReceived.Name
            .Should().Be("meepleai.wikidata.sse.messages.received.total");
        MeepleAiMetrics.WikidataSseMessagesReceived.Unit.Should().Be("messages");
    }

    [Fact]
    public void WikidataSseSubscribers_Name_MatchesIssue2470Contract()
    {
        MeepleAiMetrics.WikidataSseSubscribers.Name
            .Should().Be("meepleai.wikidata.sse.subscribers");
        MeepleAiMetrics.WikidataSseSubscribers.Unit.Should().Be("subscribers");
    }

    [Fact]
    public void WikidataSseAdminClientsConnected_Name_MatchesIssue2470Contract()
    {
        MeepleAiMetrics.WikidataSseAdminClientsConnected.Name
            .Should().Be("meepleai.wikidata.sse.admin_clients_connected");
        MeepleAiMetrics.WikidataSseAdminClientsConnected.Unit.Should().Be("clients");
    }

    [Fact]
    public void WikidataSseMessagesPublished_RecordsIncrements_AndIsCollectable()
    {
        var measurements = new List<long>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == "meepleai.wikidata.sse.messages.published.total")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => measurements.Add(value));
        listener.Start();

        MeepleAiMetrics.WikidataSseMessagesPublished.Add(1);
        MeepleAiMetrics.WikidataSseMessagesPublished.Add(5);

        // Tolerant: shared global counter; only assert OUR two increments
        // arrived through the listener wiring.
        measurements.Should().Contain(1).And.Contain(5);
    }

    [Fact]
    public void WikidataSseMessagesReceived_RecordsIncrements_AndIsCollectable()
    {
        var measurements = new List<long>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == "meepleai.wikidata.sse.messages.received.total")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => measurements.Add(value));
        listener.Start();

        MeepleAiMetrics.WikidataSseMessagesReceived.Add(3);
        MeepleAiMetrics.WikidataSseMessagesReceived.Add(7);

        measurements.Should().Contain(3).And.Contain(7);
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
