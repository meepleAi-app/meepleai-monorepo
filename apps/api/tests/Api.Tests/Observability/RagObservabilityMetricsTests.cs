using System.Diagnostics.Metrics;
using Api.Observability;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Observability;

/// <summary>
/// Unit tests for SP5-b RAG observability metrics (Issue #2582):
///   - <c>meepleai.rag.retrieval_empty</c> (<see cref="MeepleAiMetrics.RagRetrievalEmpty"/>, Counter&lt;long&gt;)
///   - <c>meepleai.rag.citations_per_answer</c> (<see cref="MeepleAiMetrics.RagCitationsPerAnswer"/>, Histogram&lt;long&gt;)
/// </summary>
/// <remarks>
/// Pattern mirrors <see cref="SseMetricsTests"/> (MeterListener-based capture, lines 88-105):
/// InstrumentPublished callback + SetMeasurementEventCallback + Start/RecordObservableInstruments.
/// No tags on either metric — cardinality rule (no session/user labels).
/// <see cref="MeepleAiMetrics.RagFirstTokenLatency"/> is intentionally NOT tested here; it is
/// declared in MeepleAiMetrics.Rag.cs:38-41 and will be wired in a subsequent task.
/// </remarks>
[Collection("RagObservabilityMetrics")]
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Observability")]
[Trait("Issue", "2582")]
public sealed class RagObservabilityMetricsTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // RagRetrievalEmpty: naming
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "RagRetrievalEmpty metric name matches SP5-b contract")]
    public void RagRetrievalEmpty_Name_MatchesSp5bContract()
    {
        MeepleAiMetrics.RagRetrievalEmpty.Name
            .Should().Be("meepleai.rag.retrieval_empty");
    }

    [Fact(DisplayName = "RagRetrievalEmpty is a Counter<long>")]
    public void RagRetrievalEmpty_IsCounterOfLong()
    {
        MeepleAiMetrics.RagRetrievalEmpty.Should().BeOfType<Counter<long>>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // RagRetrievalEmpty: recording via MeterListener
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "RecordRetrievalEmpty increments counter and is observable via MeterListener")]
    public void RecordRetrievalEmpty_IncrementsCounterAndIsObservable()
    {
        // Capture Add() measurements fired by OUR call only (shared global counter — tolerant assertion).
        var measurements = new List<long>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == "meepleai.rag.retrieval_empty")
                    l.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => measurements.Add(value));
        listener.Start();

        MeepleAiMetrics.RecordRetrievalEmpty();

        // Tolerant: concurrent tests on the same global counter may add values.
        // We only assert that OUR Add(1) call flowed through the listener.
        measurements.Should().Contain(1L,
            "RecordRetrievalEmpty must emit one Add(1) measurement");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // RagCitationsPerAnswer: naming
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "RagCitationsPerAnswer metric name matches SP5-b contract")]
    public void RagCitationsPerAnswer_Name_MatchesSp5bContract()
    {
        MeepleAiMetrics.RagCitationsPerAnswer.Name
            .Should().Be("meepleai.rag.citations_per_answer");
    }

    [Fact(DisplayName = "RagCitationsPerAnswer is a Histogram<long>")]
    public void RagCitationsPerAnswer_IsHistogramOfLong()
    {
        MeepleAiMetrics.RagCitationsPerAnswer.Should().BeOfType<Histogram<long>>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // RagCitationsPerAnswer: recording via MeterListener
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "RecordCitationsPerAnswer records value and is observable via MeterListener")]
    public void RecordCitationsPerAnswer_RecordsValueAndIsObservable()
    {
        const long expectedCount = 3L;
        var measurements = new List<long>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == "meepleai.rag.citations_per_answer")
                    l.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => measurements.Add(value));
        listener.Start();

        MeepleAiMetrics.RecordCitationsPerAnswer(expectedCount);

        measurements.Should().Contain(expectedCount,
            "RecordCitationsPerAnswer must emit one Record(count) measurement with the supplied value");
    }

    [Fact(DisplayName = "RecordCitationsPerAnswer with zero records a zero measurement (grounded-but-uncited signal)")]
    public void RecordCitationsPerAnswer_WithZero_RecordsZero()
    {
        var measurements = new List<long>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == "meepleai.rag.citations_per_answer")
                    l.EnableMeasurementEvents(instrument);
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => measurements.Add(value));
        listener.Start();

        MeepleAiMetrics.RecordCitationsPerAnswer(0);

        measurements.Should().Contain(0L,
            "le=0 bucket must be populated for the grounded-but-uncited signal");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Sanity: both instruments are non-null
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Both SP5-b instruments are initialized (non-null)")]
    public void BothMetrics_AreInitialized()
    {
        MeepleAiMetrics.RagRetrievalEmpty.Should().NotBeNull();
        MeepleAiMetrics.RagCitationsPerAnswer.Should().NotBeNull();
    }
}
