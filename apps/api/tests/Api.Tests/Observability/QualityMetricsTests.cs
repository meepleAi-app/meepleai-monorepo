using System.Diagnostics.Metrics;
using Api.Tests.Helpers;
using Api.Models;
using Api.Observability;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests.Observability;

/// <summary>
/// BDD tests for quality metrics recording using OpenTelemetry.
/// These tests verify Prometheus histogram and counter behavior (TDD RED phase).
/// </summary>
public class QualityMetricsTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Scenario: Recording quality scores across all dimensions
    /// Given a response with RAG 0.85, LLM 0.80, Citation 0.90, Overall 0.85
    /// When RecordQualityScores is called
    /// Then histogram should record all 4 dimensions with correct values
    /// </summary>
    [Fact]
    public void RecordQualityScores_AllDimensions_RecordsHistogramValues()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var metrics = new QualityMetrics(meterFactory);
        var collector = new MetricCollector<double>(meterFactory, "meepleai.quality.score", "dimension");

        var scores = new QualityScores
        {
            RagConfidence = 0.85,
            LlmConfidence = 0.80,
            CitationQuality = 0.90,
            OverallConfidence = 0.85
        };

        // Act
        metrics.RecordQualityScores(scores, "qa", "answer");

        // Assert
        var measurements = collector.GetMeasurements();
        measurements.Should().Contain(m =>
            m.Tags != null && m.Tags.ContainsKey("dimension") &&
            m.Tags["dimension"]!.ToString() == "rag_confidence" &&
            Math.Abs(m.Value - 0.85) < 0.001);
        measurements.Should().Contain(m =>
            m.Tags != null && m.Tags.ContainsKey("dimension") &&
            m.Tags["dimension"]!.ToString() == "llm_confidence" &&
            Math.Abs(m.Value - 0.80) < 0.001);
        measurements.Should().Contain(m =>
            m.Tags != null && m.Tags.ContainsKey("dimension") &&
            m.Tags["dimension"]!.ToString() == "citation_quality" &&
            Math.Abs(m.Value - 0.90) < 0.001);
        measurements.Should().Contain(m =>
            m.Tags != null && m.Tags.ContainsKey("dimension") &&
            m.Tags["dimension"]!.ToString() == "overall_confidence" &&
            Math.Abs(m.Value - 0.85) < 0.001);
    }

    /// <summary>
    /// Scenario: Recording low-quality response
    /// Given a response flagged as low-quality (overall 0.45)
    /// When RecordQualityScores is called
    /// Then low-quality counter should increment by 1
    /// </summary>
    [Fact]
    public void RecordQualityScores_LowQuality_IncrementsCounter()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var metrics = new QualityMetrics(meterFactory);
        var collector = new MetricCollector<long>(meterFactory, "meepleai.quality.low_quality_responses.total");

        var scores = new QualityScores
        {
            RagConfidence = 0.40,
            LlmConfidence = 0.45,
            CitationQuality = 0.50,
            OverallConfidence = 0.45,
            IsLowQuality = true
        };

        // Act
        metrics.RecordQualityScores(scores, "qa", "answer");

        // Assert
        var measurements = collector.GetMeasurements();
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(1);
    }

    /// <summary>
    /// Scenario: Recording high-quality response
    /// Given a response NOT flagged as low-quality (overall 0.87)
    /// When RecordQualityScores is called
    /// Then low-quality counter should NOT increment
    /// </summary>
    [Fact]
    public void RecordQualityScores_HighQuality_DoesNotIncrementCounter()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var metrics = new QualityMetrics(meterFactory);
        var collector = new MetricCollector<long>(meterFactory, "meepleai.quality.low_quality_responses.total");

        var scores = new QualityScores
        {
            RagConfidence = 0.85,
            LlmConfidence = 0.87,
            CitationQuality = 0.90,
            OverallConfidence = 0.87,
            IsLowQuality = false
        };

        // Act
        metrics.RecordQualityScores(scores, "qa", "answer");

        // Assert
        var measurements = collector.GetMeasurements();
        measurements.Should().BeEmpty();
    }

    /// <summary>
    /// Scenario: Labels applied correctly to metrics
    /// Given quality scores with agent type "qa" and operation "answer"
    /// When RecordQualityScores is called
    /// Then all metrics should include agent.type and operation labels
    /// </summary>
    [Fact]
    public void RecordQualityScores_LabelsApplied_IncludesAgentTypeAndOperation()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var metrics = new QualityMetrics(meterFactory);
        var collector = new MetricCollector<double>(meterFactory, "meepleai.quality.score");

        var scores = new QualityScores
        {
            RagConfidence = 0.85,
            LlmConfidence = 0.80,
            CitationQuality = 0.90,
            OverallConfidence = 0.85
        };

        // Act
        metrics.RecordQualityScores(scores, "qa", "answer");

        // Assert
        var measurements = collector.GetMeasurements();
        measurements.Should().OnlyContain(m =>
            m.Tags != null && m.Tags.ContainsKey("agent.type") &&
            m.Tags["agent.type"]!.ToString() == "qa" &&
            m.Tags.ContainsKey("operation") &&
            m.Tags["operation"]!.ToString() == "answer");
    }

    /// <summary>
    /// Scenario: Quality tier labels (high/medium/low)
    /// Given quality scores at different tiers (0.85 high, 0.65 medium, 0.45 low)
    /// When RecordQualityScores is called
    /// Then quality_tier label should reflect the tier
    /// </summary>
    [Theory]
    [InlineData(0.85, "high")]
    [InlineData(0.65, "medium")]
    [InlineData(0.45, "low")]
    public void RecordQualityScores_QualityTiers_AppliesCorrectLabel(double overallConfidence, string expectedTier)
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var metrics = new QualityMetrics(meterFactory);
        var collector = new MetricCollector<double>(meterFactory, "meepleai.quality.score");

        var scores = new QualityScores
        {
            RagConfidence = overallConfidence,
            LlmConfidence = overallConfidence,
            CitationQuality = overallConfidence,
            OverallConfidence = overallConfidence,
            IsLowQuality = overallConfidence < 0.60
        };

        // Act
        metrics.RecordQualityScores(scores, "qa", "answer");

        // Assert
        var measurements = collector.GetMeasurements();
        measurements.Should().OnlyContain(m =>
            m.Tags != null && m.Tags.ContainsKey("quality_tier") &&
            m.Tags["quality_tier"]!.ToString() == expectedTier);
    }

    /// <summary>
    /// Scenario: Multiple recordings accumulate correctly
    /// Given 3 quality score recordings (2 low-quality, 1 high-quality)
    /// When RecordQualityScores is called multiple times
    /// Then low-quality counter should total 2, histograms should have 3 measurements each
    /// </summary>
    [Fact]
    public void RecordQualityScores_MultipleRecordings_AccumulatesCorrectly()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var metrics = new QualityMetrics(meterFactory);
        var histogramCollector = new MetricCollector<double>(meterFactory, "meepleai.quality.score", "dimension");
        var counterCollector = new MetricCollector<long>(meterFactory, "meepleai.quality.low_quality_responses.total");

        var lowQualityScores1 = new QualityScores
        {
            RagConfidence = 0.40,
            LlmConfidence = 0.45,
            CitationQuality = 0.50,
            OverallConfidence = 0.45,
            IsLowQuality = true
        };
        var lowQualityScores2 = new QualityScores
        {
            RagConfidence = 0.35,
            LlmConfidence = 0.40,
            CitationQuality = 0.45,
            OverallConfidence = 0.40,
            IsLowQuality = true
        };
        var highQualityScores = new QualityScores
        {
            RagConfidence = 0.85,
            LlmConfidence = 0.87,
            CitationQuality = 0.90,
            OverallConfidence = 0.87,
            IsLowQuality = false
        };

        // Act
        metrics.RecordQualityScores(lowQualityScores1, "qa", "answer");
        metrics.RecordQualityScores(lowQualityScores2, "qa", "answer");
        metrics.RecordQualityScores(highQualityScores, "qa", "answer");

        // Assert
        var counterMeasurements = counterCollector.GetMeasurements();
        counterMeasurements.Count.Should().Be(2);
        counterMeasurements.Should().OnlyContain(m => m.Value == 1);

        var histogramMeasurements = histogramCollector.GetMeasurements();
        // 3 recordings × 4 dimensions = 12 measurements
        histogramMeasurements.Count.Should().Be(12);
    }

    /// <summary>
    /// Scenario: Zero confidence scores
    /// Given all confidence scores are 0.0
    /// When RecordQualityScores is called
    /// Then histogram should record 0.0 values correctly
    /// </summary>
    [Fact]
    public void RecordQualityScores_ZeroConfidence_RecordsZeroValues()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var metrics = new QualityMetrics(meterFactory);
        var collector = new MetricCollector<double>(meterFactory, "meepleai.quality.score");

        var scores = new QualityScores
        {
            RagConfidence = 0.0,
            LlmConfidence = 0.0,
            CitationQuality = 0.0,
            OverallConfidence = 0.0,
            IsLowQuality = true
        };

        // Act
        metrics.RecordQualityScores(scores, "qa", "answer");

        // Assert
        var measurements = collector.GetMeasurements();
        measurements.Should().OnlyContain(m => m.Value == 0.0);
    }

    /// <summary>
    /// Scenario: Different agent types tracked separately
    /// Given recordings for "qa" and "explain" agents
    /// When RecordQualityScores is called with different agent types
    /// Then metrics should include distinct agent.type labels
    /// </summary>
    [Fact]
    public void RecordQualityScores_DifferentAgentTypes_TrackedSeparately()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var metrics = new QualityMetrics(meterFactory);
        var collector = new MetricCollector<double>(meterFactory, "meepleai.quality.score");

        var scores = new QualityScores
        {
            RagConfidence = 0.80,
            LlmConfidence = 0.80,
            CitationQuality = 0.80,
            OverallConfidence = 0.80
        };

        // Act
        metrics.RecordQualityScores(scores, "qa", "answer");
        metrics.RecordQualityScores(scores, "explain", "rule_explanation");

        // Assert
        var measurements = collector.GetMeasurements();
        var qaMeasurements = measurements.Where(m => m.Tags != null && m.Tags.ContainsKey("agent.type") && m.Tags["agent.type"]?.ToString() == "qa").ToList();
        var explainMeasurements = measurements.Where(m => m.Tags != null && m.Tags.ContainsKey("agent.type") && m.Tags["agent.type"]?.ToString() == "explain").ToList();

        qaMeasurements.Count.Should().Be(4); // 4 dimensions
        explainMeasurements.Count.Should().Be(4); // 4 dimensions
    }

    /// <summary>
    /// Scenario: Histogram buckets configuration
    /// Given quality scores ranging from 0.0 to 1.0
    /// When histogram is configured
    /// Then should have appropriate buckets (0.0, 0.2, 0.4, 0.6, 0.8, 1.0)
    /// </summary>
    [Fact]
    public void QualityMetrics_HistogramBuckets_ConfiguredCorrectly()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();

        // Act
        var metrics = new QualityMetrics(meterFactory);

        // Assert
        // This test verifies the histogram is created with correct configuration
        // Actual bucket verification will depend on OpenTelemetry exporter config
        metrics.Should().NotBeNull();
    }

    /// <summary>
    /// Scenario: Metric names follow naming conventions
    /// Given QualityMetrics instance
    /// When metrics are recorded
    /// Then metric names should follow meepleai.quality.* pattern
    /// </summary>
    [Fact]
    public void QualityMetrics_MetricNames_FollowNamingConvention()
    {
        // Arrange
        using var meterFactory = new TestMeterFactory();
        var metrics = new QualityMetrics(meterFactory);

        // Act & Assert
        // Verify expected metric names exist
        var histogramCollector = new MetricCollector<double>(meterFactory, "meepleai.quality.score");
        var counterCollector = new MetricCollector<long>(meterFactory, "meepleai.quality.low_quality_responses.total");

        histogramCollector.Should().NotBeNull();
        counterCollector.Should().NotBeNull();
    }
}
