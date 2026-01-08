using Api.BoundedContexts.Administration.Application.Commands.PromptEvaluation;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers.PromptHandlers;

[Trait("Category", TestCategories.Unit)]
public class ComparePromptVersionsCommandHandlerTests
{
    private readonly Mock<IPromptEvaluationService> _mockEvaluationService;
    private readonly Mock<ILogger<ComparePromptVersionsCommandHandler>> _mockLogger;
    private readonly ComparePromptVersionsCommandHandler _handler;

    public ComparePromptVersionsCommandHandlerTests()
    {
        _mockEvaluationService = new Mock<IPromptEvaluationService>();
        _mockLogger = new Mock<ILogger<ComparePromptVersionsCommandHandler>>();
        _handler = new ComparePromptVersionsCommandHandler(
            _mockEvaluationService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithCandidateBetterThanBaseline_ShouldRecommendActivate()
    {
        // Arrange
        var command = new ComparePromptVersionsCommand
        {
            TemplateId = "template-123",
            BaselineVersionId = "version-1",
            CandidateVersionId = "version-2",
            DatasetPath = "/datasets/qa-test-dataset.json"
        };

        var comparisonResult = new PromptComparisonResult
        {
            ComparisonId = "comparison-456",
            TemplateId = "template-123",
            BaselineResult = new PromptEvaluationResult
            {
                EvaluationId = "eval-baseline",
                TemplateId = "template-123",
                VersionId = "version-1",
                DatasetId = "qa-test-dataset",
                ExecutedAt = DateTime.UtcNow,
                TotalQueries = 10,
                Passed = true,
                Metrics = new EvaluationMetrics
                {
                    Accuracy = 80.0,
                    Relevance = 85.0,
                    Completeness = 75.0,
                    Clarity = 80.0,
                    CitationQuality = 85.0
                },
                QueryResults = new List<QueryEvaluationResult>()
            },
            CandidateResult = new PromptEvaluationResult
            {
                EvaluationId = "eval-candidate",
                TemplateId = "template-123",
                VersionId = "version-2",
                DatasetId = "qa-test-dataset",
                ExecutedAt = DateTime.UtcNow,
                TotalQueries = 10,
                Passed = true,
                Metrics = new EvaluationMetrics
                {
                    Accuracy = 90.0,
                    Relevance = 92.0,
                    Completeness = 88.0,
                    Clarity = 89.0,
                    CitationQuality = 93.0
                },
                QueryResults = new List<QueryEvaluationResult>()
            },
            Deltas = new MetricDeltas
            {
                AccuracyDelta = 10.0,
                RelevanceDelta = 7.0,
                CompletenessDelta = 13.0,
                ClarityDelta = 9.0,
                CitationQualityDelta = 8.0
            },
            Recommendation = ComparisonRecommendation.Activate,
            RecommendationReason = "Candidate shows significant improvement across all metrics (Accuracy +10.0%, Relevance +7.0%, Completeness +13.0%, Clarity +9.0%, Citation Quality +8.0%)"
        };

        _mockEvaluationService
            .Setup(s => s.CompareVersionsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(comparisonResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ComparisonId.Should().Be("comparison-456");
        result.Recommendation.Should().Be(ComparisonRecommendation.Activate);
        result.Deltas.AccuracyDelta.Should().Be(10.0);
        result.Deltas.RelevanceDelta.Should().Be(7.0);
        result.CandidateResult.Metrics.Accuracy.Should().BeGreaterThan(result.BaselineResult.Metrics.Accuracy);

        _mockEvaluationService.Verify(s => s.CompareVersionsAsync(
            "template-123",
            "version-1",
            "version-2",
            "/datasets/qa-test-dataset.json",
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCandidateWorseThanBaseline_ShouldRecommendReject()
    {
        // Arrange
        var command = new ComparePromptVersionsCommand
        {
            TemplateId = "template-123",
            BaselineVersionId = "version-1",
            CandidateVersionId = "version-2",
            DatasetPath = "/datasets/qa-test-dataset.json"
        };

        var comparisonResult = new PromptComparisonResult
        {
            ComparisonId = "comparison-456",
            TemplateId = "template-123",
            BaselineResult = new PromptEvaluationResult
            {
                EvaluationId = "eval-baseline",
                TemplateId = "template-123",
                VersionId = "version-1",
                DatasetId = "qa-test-dataset",
                ExecutedAt = DateTime.UtcNow,
                TotalQueries = 10,
                Passed = true,
                Metrics = new EvaluationMetrics
                {
                    Accuracy = 85.0,
                    Relevance = 90.0,
                    Completeness = 80.0,
                    Clarity = 88.0,
                    CitationQuality = 92.0
                },
                QueryResults = new List<QueryEvaluationResult>()
            },
            CandidateResult = new PromptEvaluationResult
            {
                EvaluationId = "eval-candidate",
                TemplateId = "template-123",
                VersionId = "version-2",
                DatasetId = "qa-test-dataset",
                ExecutedAt = DateTime.UtcNow,
                TotalQueries = 10,
                Passed = false,
                Metrics = new EvaluationMetrics
                {
                    Accuracy = 70.0,
                    Relevance = 75.0,
                    Completeness = 68.0,
                    Clarity = 72.0,
                    CitationQuality = 78.0
                },
                QueryResults = new List<QueryEvaluationResult>()
            },
            Deltas = new MetricDeltas
            {
                AccuracyDelta = -15.0,
                RelevanceDelta = -15.0,
                CompletenessDelta = -12.0,
                ClarityDelta = -16.0,
                CitationQualityDelta = -14.0
            },
            Recommendation = ComparisonRecommendation.Reject,
            RecommendationReason = "Candidate shows regression across all metrics (Accuracy -15.0%, Relevance -15.0%, Completeness -12.0%, Clarity -16.0%, Citation Quality -14.0%)"
        };

        _mockEvaluationService
            .Setup(s => s.CompareVersionsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(comparisonResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Recommendation.Should().Be(ComparisonRecommendation.Reject);
        result.Deltas.AccuracyDelta.Should().BeLessThan(0);
        result.CandidateResult.Passed.Should().BeFalse();
        result.RecommendationReason.Should().Contain("regression");
    }

    [Fact]
    public async Task Handle_WithMixedResults_ShouldRecommendManualReview()
    {
        // Arrange
        var command = new ComparePromptVersionsCommand
        {
            TemplateId = "template-123",
            BaselineVersionId = "version-1",
            CandidateVersionId = "version-2",
            DatasetPath = "/datasets/qa-test-dataset.json"
        };

        var comparisonResult = new PromptComparisonResult
        {
            ComparisonId = "comparison-456",
            TemplateId = "template-123",
            BaselineResult = new PromptEvaluationResult
            {
                EvaluationId = "eval-baseline",
                TemplateId = "template-123",
                VersionId = "version-1",
                DatasetId = "qa-test-dataset",
                ExecutedAt = DateTime.UtcNow,
                TotalQueries = 10,
                Passed = true,
                Metrics = new EvaluationMetrics
                {
                    Accuracy = 85.0,
                    Relevance = 90.0,
                    Completeness = 80.0,
                    Clarity = 88.0,
                    CitationQuality = 92.0
                },
                QueryResults = new List<QueryEvaluationResult>()
            },
            CandidateResult = new PromptEvaluationResult
            {
                EvaluationId = "eval-candidate",
                TemplateId = "template-123",
                VersionId = "version-2",
                DatasetId = "qa-test-dataset",
                ExecutedAt = DateTime.UtcNow,
                TotalQueries = 10,
                Passed = true,
                Metrics = new EvaluationMetrics
                {
                    Accuracy = 87.0,
                    Relevance = 88.0,
                    Completeness = 85.0,
                    Clarity = 86.0,
                    CitationQuality = 90.0
                },
                QueryResults = new List<QueryEvaluationResult>()
            },
            Deltas = new MetricDeltas
            {
                AccuracyDelta = 2.0,
                RelevanceDelta = -2.0,
                CompletenessDelta = 5.0,
                ClarityDelta = -2.0,
                CitationQualityDelta = -2.0
            },
            Recommendation = ComparisonRecommendation.ManualReview,
            RecommendationReason = "Mixed results with both improvements and regressions. Manual review recommended."
        };

        _mockEvaluationService
            .Setup(s => s.CompareVersionsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(comparisonResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Recommendation.Should().Be(ComparisonRecommendation.ManualReview);
        result.RecommendationReason.Should().Contain("Manual review");
        result.Deltas.AccuracyDelta.Should().BeGreaterThan(0);
        result.Deltas.RelevanceDelta.Should().BeLessThan(0);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public void Constructor_WithNullEvaluationService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ComparePromptVersionsCommandHandler(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ComparePromptVersionsCommandHandler(_mockEvaluationService.Object, null!));
    }
}
