using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Evaluation;

/// <summary>
/// Unit tests for EvaluationMetrics.
/// ADR-016 Phase 0: Validates metric creation, clamping, and phase target checks.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class EvaluationMetricsTests
{
    [Fact]
    public void Create_WithValidValues_CreatesMetrics()
    {
        // Arrange & Act
        var metrics = EvaluationMetrics.Create(
            recallAt5: 0.75,
            recallAt10: 0.85,
            ndcgAt10: 0.80,
            mrr: 0.90,
            p95LatencyMs: 1200.0,
            answerCorrectness: 0.88,
            sampleCount: 50);

        // Assert
        metrics.RecallAt5.Should().Be(0.75);
        metrics.RecallAt10.Should().Be(0.85);
        metrics.NdcgAt10.Should().Be(0.80);
        metrics.Mrr.Should().Be(0.90);
        metrics.P95LatencyMs.Should().Be(1200.0);
        metrics.AnswerCorrectness.Should().Be(0.88);
        metrics.SampleCount.Should().Be(50);
    }

    [Fact]
    public void Create_WithValuesAboveOne_ClampsToBounds()
    {
        // Arrange & Act
        var metrics = EvaluationMetrics.Create(
            recallAt5: 1.5, // Should clamp to 1.0
            recallAt10: 2.0, // Should clamp to 1.0
            ndcgAt10: 1.1, // Should clamp to 1.0
            mrr: 1.2, // Should clamp to 1.0
            p95LatencyMs: 1000.0,
            answerCorrectness: 1.5, // Should clamp to 1.0
            sampleCount: 30);

        // Assert
        metrics.RecallAt5.Should().Be(1.0);
        metrics.RecallAt10.Should().Be(1.0);
        metrics.NdcgAt10.Should().Be(1.0);
        metrics.Mrr.Should().Be(1.0);
        metrics.AnswerCorrectness.Should().Be(1.0);
    }

    [Fact]
    public void Create_WithNegativeValues_ClampsToZero()
    {
        // Arrange & Act
        var metrics = EvaluationMetrics.Create(
            recallAt5: -0.5, // Should clamp to 0.0
            recallAt10: -1.0, // Should clamp to 0.0
            ndcgAt10: -0.2, // Should clamp to 0.0
            mrr: -0.3, // Should clamp to 0.0
            p95LatencyMs: -100.0, // Should clamp to 0.0
            answerCorrectness: -0.1, // Should clamp to 0.0
            sampleCount: -5); // Should clamp to 0

        // Assert
        metrics.RecallAt5.Should().Be(0.0);
        metrics.RecallAt10.Should().Be(0.0);
        metrics.NdcgAt10.Should().Be(0.0);
        metrics.Mrr.Should().Be(0.0);
        metrics.P95LatencyMs.Should().Be(0.0);
        metrics.AnswerCorrectness.Should().Be(0.0);
        metrics.SampleCount.Should().Be(0);
    }

    [Fact]
    public void Empty_ReturnsZeroMetrics()
    {
        // Arrange & Act
        var metrics = EvaluationMetrics.Empty;

        // Assert
        metrics.RecallAt5.Should().Be(0.0);
        metrics.RecallAt10.Should().Be(0.0);
        metrics.NdcgAt10.Should().Be(0.0);
        metrics.Mrr.Should().Be(0.0);
        metrics.P95LatencyMs.Should().Be(0.0);
        metrics.AnswerCorrectness.Should().Be(0.0);
        metrics.SampleCount.Should().Be(0);
    }

    [Fact]
    public void MeetsBaselineRequirements_WithSufficientSamples_ReturnsTrue()
    {
        // Arrange
        var metrics = EvaluationMetrics.Create(
            recallAt5: 0.5,
            recallAt10: 0.6,
            ndcgAt10: 0.5,
            mrr: 0.5,
            p95LatencyMs: 1000.0,
            answerCorrectness: 0.5,
            sampleCount: 30); // Exactly 30 samples

        // Act & Assert
        Assert.True(metrics.MeetsBaselineRequirements());
    }

    [Fact]
    public void MeetsBaselineRequirements_WithInsufficientSamples_ReturnsFalse()
    {
        // Arrange
        var metrics = EvaluationMetrics.Create(
            recallAt5: 0.9,
            recallAt10: 0.95,
            ndcgAt10: 0.9,
            mrr: 0.9,
            p95LatencyMs: 500.0,
            answerCorrectness: 0.9,
            sampleCount: 29); // Below 30 threshold

        // Act & Assert
        Assert.False(metrics.MeetsBaselineRequirements());
    }

    [Fact]
    public void MeetsPhase4Target_WithRecallAt10Above60Percent_ReturnsTrue()
    {
        // Arrange
        var metrics = EvaluationMetrics.Create(
            recallAt5: 0.5,
            recallAt10: 0.60, // Exactly 60%
            ndcgAt10: 0.5,
            mrr: 0.5,
            p95LatencyMs: 2000.0,
            answerCorrectness: 0.5,
            sampleCount: 30);

        // Act & Assert
        Assert.True(metrics.MeetsPhase4Target());
    }

    [Fact]
    public void MeetsPhase4Target_WithRecallAt10Below60Percent_ReturnsFalse()
    {
        // Arrange
        var metrics = EvaluationMetrics.Create(
            recallAt5: 0.5,
            recallAt10: 0.59, // Below 60%
            ndcgAt10: 0.5,
            mrr: 0.5,
            p95LatencyMs: 1000.0,
            answerCorrectness: 0.5,
            sampleCount: 30);

        // Act & Assert
        Assert.False(metrics.MeetsPhase4Target());
    }

    [Fact]
    public void MeetsPhase5Target_WithHighRecallAndLowLatency_ReturnsTrue()
    {
        // Arrange
        var metrics = EvaluationMetrics.Create(
            recallAt5: 0.65,
            recallAt10: 0.70, // >= 70%
            ndcgAt10: 0.65,
            mrr: 0.7,
            p95LatencyMs: 1400.0, // < 1500ms
            answerCorrectness: 0.7,
            sampleCount: 50);

        // Act & Assert
        Assert.True(metrics.MeetsPhase5Target());
    }

    [Fact]
    public void MeetsPhase5Target_WithLowRecall_ReturnsFalse()
    {
        // Arrange
        var metrics = EvaluationMetrics.Create(
            recallAt5: 0.65,
            recallAt10: 0.69, // Below 70%
            ndcgAt10: 0.65,
            mrr: 0.7,
            p95LatencyMs: 1400.0, // Good latency
            answerCorrectness: 0.7,
            sampleCount: 50);

        // Act & Assert
        Assert.False(metrics.MeetsPhase5Target());
    }

    [Fact]
    public void MeetsPhase5Target_WithHighLatency_ReturnsFalse()
    {
        // Arrange
        var metrics = EvaluationMetrics.Create(
            recallAt5: 0.75,
            recallAt10: 0.80, // Good recall
            ndcgAt10: 0.75,
            mrr: 0.8,
            p95LatencyMs: 1600.0, // Above 1500ms threshold
            answerCorrectness: 0.8,
            sampleCount: 50);

        // Act & Assert
        Assert.False(metrics.MeetsPhase5Target());
    }

    [Fact]
    public void MeetsPhase5Target_AtExactThresholds_ReturnsTrue()
    {
        // Arrange
        var metrics = EvaluationMetrics.Create(
            recallAt5: 0.65,
            recallAt10: 0.70, // Exactly 70%
            ndcgAt10: 0.65,
            mrr: 0.7,
            p95LatencyMs: 1499.0, // Just below 1500ms
            answerCorrectness: 0.7,
            sampleCount: 50);

        // Act & Assert
        Assert.True(metrics.MeetsPhase5Target());
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Create_WithBoundaryValues_CreatesValidMetrics(double value)
    {
        // Arrange & Act
        var metrics = EvaluationMetrics.Create(
            recallAt5: value,
            recallAt10: value,
            ndcgAt10: value,
            mrr: value,
            p95LatencyMs: value * 1000,
            answerCorrectness: value,
            sampleCount: 30);

        // Assert
        metrics.RecallAt5.Should().Be(value);
        metrics.RecallAt10.Should().Be(value);
        metrics.NdcgAt10.Should().Be(value);
        metrics.Mrr.Should().Be(value);
        metrics.AnswerCorrectness.Should().Be(value);
    }
}
