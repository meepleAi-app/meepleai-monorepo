using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Api.Observability;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Observability;

/// <summary>
/// Issue #833 — gamebook translation + OCR + glossary metric exporters.
/// Verifies the 7 instrument names match the spec (these names are consumed by
/// Prometheus alert rules and Grafana dashboards) and that record helpers emit
/// values with the documented label set.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Area", "Observability")]
[Trait("BoundedContext", "SessionTracking")]
public class GamebookTranslationMetricsTests
{
    private const string ExpectedRequestsName = "meepleai.gamebook.translation_requests_total";
    private const string ExpectedLatencyName = "meepleai.gamebook.translation_latency_seconds";
    private const string ExpectedTokenName = "meepleai.gamebook.translation_token_count";
    private const string ExpectedCostName = "meepleai.gamebook.translation_cost_eur";
    private const string ExpectedOcrConfidenceName = "meepleai.gamebook.ocr_confidence_score";
    private const string ExpectedOcrSegMatchName = "meepleai.gamebook.ocr_segmentation_match_quality_total";
    private const string ExpectedGlossaryName = "meepleai.gamebook.glossary_consistency_rate";

    [Fact]
    public void AllSevenGamebookInstruments_AreRegistered_WithMeepleAiMeterName()
    {
        // Force static field initialisation by referencing each metric.
        _ = MeepleAiMetrics.GamebookTranslationRequestsTotal;
        _ = MeepleAiMetrics.GamebookTranslationLatencySeconds;
        _ = MeepleAiMetrics.GamebookTranslationTokenCount;
        _ = MeepleAiMetrics.GamebookTranslationCostEur;
        _ = MeepleAiMetrics.GamebookOcrConfidenceScore;
        _ = MeepleAiMetrics.GamebookOcrSegmentationMatchQualityTotal;
        _ = MeepleAiMetrics.GamebookGlossaryConsistencyRate;

        MeepleAiMetrics.GamebookTranslationRequestsTotal.Name.Should().Be(ExpectedRequestsName);
        MeepleAiMetrics.GamebookTranslationLatencySeconds.Name.Should().Be(ExpectedLatencyName);
        MeepleAiMetrics.GamebookTranslationTokenCount.Name.Should().Be(ExpectedTokenName);
        MeepleAiMetrics.GamebookTranslationCostEur.Name.Should().Be(ExpectedCostName);
        MeepleAiMetrics.GamebookOcrConfidenceScore.Name.Should().Be(ExpectedOcrConfidenceName);
        MeepleAiMetrics.GamebookOcrSegmentationMatchQualityTotal.Name.Should().Be(ExpectedOcrSegMatchName);
        MeepleAiMetrics.GamebookGlossaryConsistencyRate.Name.Should().Be(ExpectedGlossaryName);

        MeepleAiMetrics.GamebookTranslationRequestsTotal.Meter.Name.Should().Be(MeepleAiMetrics.MeterName);
        MeepleAiMetrics.GamebookOcrConfidenceScore.Meter.Name.Should().Be(MeepleAiMetrics.MeterName);
    }

    [Fact]
    public void RecordGamebookTranslationRequest_Success_EmitsAllMetricsWithExpectedTags()
    {
        var measurements = new MeasurementCapture();
        using var listener = measurements.StartListening();

        MeepleAiMetrics.RecordGamebookTranslationRequest(
            status: "success",
            latencyFullSeconds: 2.5,
            latencyStreamingSeconds: 0.4,
            promptTokens: 120,
            completionTokens: 280,
            costUsd: 0.10,
            provider: "openrouter");

        var requestTotal = measurements.FindLast(ExpectedRequestsName);
        requestTotal.Should().NotBeNull();
        requestTotal!.Value.Should().Be(1.0);
        requestTotal.Tags.Should().ContainSingle(t => t.Key == "status" && (string?)t.Value == "success");

        var latencyFull = measurements.FindLast(ExpectedLatencyName, "stage", "full");
        latencyFull!.Value.Should().Be(2.5);

        var latencyStreaming = measurements.FindLast(ExpectedLatencyName, "stage", "streaming");
        latencyStreaming!.Value.Should().Be(0.4);

        var prompt = measurements.FindLast(ExpectedTokenName, "kind", "prompt");
        prompt!.Value.Should().Be(120.0);

        var completion = measurements.FindLast(ExpectedTokenName, "kind", "completion");
        completion!.Value.Should().Be(280.0);

        var cost = measurements.FindLast(ExpectedCostName);
        cost.Should().NotBeNull();
        cost!.Value.Should().BeApproximately(0.10 * MeepleAiMetrics.UsdToEurRate, 0.0001);
        cost.Tags.Should().ContainSingle(t => t.Key == "provider" && (string?)t.Value == "openrouter");
    }

    [Fact]
    public void RecordGamebookTranslationRequest_Failure_SkipsTokenAndCostWhenAbsent()
    {
        var measurements = new MeasurementCapture();
        using var listener = measurements.StartListening();

        MeepleAiMetrics.RecordGamebookTranslationRequest(
            status: "failure",
            latencyFullSeconds: 0.3,
            latencyStreamingSeconds: null,
            promptTokens: null,
            completionTokens: null,
            costUsd: null,
            provider: "unknown");

        measurements.FindLast(ExpectedRequestsName)!.Tags
            .Should().ContainSingle(t => t.Key == "status" && (string?)t.Value == "failure");
        measurements.FindLast(ExpectedLatencyName, "stage", "full")!.Value.Should().Be(0.3);
        measurements.FindLast(ExpectedLatencyName, "stage", "streaming").Should().BeNull();
        measurements.FindLast(ExpectedTokenName, "kind", "prompt").Should().BeNull();
        measurements.FindLast(ExpectedTokenName, "kind", "completion").Should().BeNull();
        measurements.FindLast(ExpectedCostName).Should().BeNull();
    }

    [Fact]
    public void RecordGamebookOcrSegmentation_EmitsConfidenceHistogramAndQualityCounters()
    {
        var measurements = new MeasurementCapture();
        using var listener = measurements.StartListening();

        MeepleAiMetrics.RecordGamebookOcrSegmentation(
            confidenceScores: new[] { 0.92, 0.88 },
            language: "ita",
            exactMatches: 2,
            partialMatches: 1,
            misses: 0);

        var confidences = measurements.FindAll(ExpectedOcrConfidenceName);
        confidences.Should().HaveCount(2);
        confidences.Should().AllSatisfy(m => m.Tags.Should().ContainSingle(t => t.Key == "language" && (string?)t.Value == "ita"));
        confidences.Select(m => m.Value).Should().BeEquivalentTo(new[] { 0.92, 0.88 });

        var exact = measurements.FindLast(ExpectedOcrSegMatchName, "quality", "exact");
        exact!.Value.Should().Be(2.0);

        var partial = measurements.FindLast(ExpectedOcrSegMatchName, "quality", "partial");
        partial!.Value.Should().Be(1.0);

        measurements.FindLast(ExpectedOcrSegMatchName, "quality", "miss").Should().BeNull();
    }

    [Fact]
    public void RecordGamebookGlossaryConsistency_EmitsRateWithCampaignHashLabel()
    {
        var measurements = new MeasurementCapture();
        using var listener = measurements.StartListening();

        MeepleAiMetrics.RecordGamebookGlossaryConsistency(0.75, "ab12cd34");

        var record = measurements.FindLast(ExpectedGlossaryName);
        record.Should().NotBeNull();
        record!.Value.Should().Be(0.75);
        record.Tags.Should().ContainSingle(t => t.Key == "campaign_id_hash" && (string?)t.Value == "ab12cd34");
    }

    private sealed class MeasurementCapture
    {
        private readonly List<Captured> _captured = new();

        public MeterListener StartListening()
        {
            var listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == MeepleAiMetrics.MeterName
                        && instrument.Name.StartsWith("meepleai.gamebook.", System.StringComparison.Ordinal))
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                }
            };

            listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                _captured.Add(new Captured(instrument.Name, value, tags.ToArray())));
            listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                _captured.Add(new Captured(instrument.Name, value, tags.ToArray())));
            listener.SetMeasurementEventCallback<int>((instrument, value, tags, _) =>
                _captured.Add(new Captured(instrument.Name, value, tags.ToArray())));

            listener.Start();
            return listener;
        }

        public Captured? FindLast(string name, string? tagKey = null, string? tagValue = null)
            => _captured.Where(m => m.Name == name
                                    && (tagKey is null || m.Tags.Any(t => t.Key == tagKey && (string?)t.Value == tagValue)))
                .LastOrDefault();

        public IReadOnlyList<Captured> FindAll(string name)
            => _captured.Where(m => m.Name == name).ToList();
    }

    private sealed record Captured(string Name, double Value, KeyValuePair<string, object?>[] Tags);
}
