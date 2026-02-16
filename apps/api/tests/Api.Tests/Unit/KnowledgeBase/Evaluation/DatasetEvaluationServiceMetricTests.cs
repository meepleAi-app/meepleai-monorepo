using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase.Evaluation;

/// <summary>
/// Unit tests for DatasetEvaluationService metric calculations (Issue #4278).
/// Tests Recall@K, nDCG@K, MRR, and ComputeMetrics without RAG service dependency.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4278")]
public sealed class DatasetEvaluationServiceMetricTests
{
    private readonly DatasetEvaluationService _service;

    public DatasetEvaluationServiceMetricTests()
    {
        var ragService = new Mock<IRagService>();
        var logger = new Mock<ILogger<DatasetEvaluationService>>();
        _service = new DatasetEvaluationService(ragService.Object, logger.Object);
    }

    #region CalculateRecallAtK Tests

    [Fact]
    public void CalculateRecallAtK_AllRelevantRetrieved_ShouldReturnOne()
    {
        // Arrange
        var retrieved = new List<string> { "chunk-1", "chunk-2", "chunk-3" };
        var relevant = new List<string> { "chunk-1", "chunk-2" };

        // Act
        var recall = _service.CalculateRecallAtK(retrieved, relevant, k: 3);

        // Assert
        recall.Should().Be(1.0);
    }

    [Fact]
    public void CalculateRecallAtK_PartialRetrieved_ShouldReturnFraction()
    {
        // Arrange
        var retrieved = new List<string> { "chunk-1", "chunk-3", "chunk-5" };
        var relevant = new List<string> { "chunk-1", "chunk-2", "chunk-4" };

        // Act
        var recall = _service.CalculateRecallAtK(retrieved, relevant, k: 3);

        // Assert
        recall.Should().BeApproximately(1.0 / 3.0, 0.001); // 1 of 3 relevant found
    }

    [Fact]
    public void CalculateRecallAtK_NoRelevantRetrieved_ShouldReturnZero()
    {
        // Arrange
        var retrieved = new List<string> { "chunk-10", "chunk-20" };
        var relevant = new List<string> { "chunk-1", "chunk-2" };

        // Act
        var recall = _service.CalculateRecallAtK(retrieved, relevant, k: 2);

        // Assert
        recall.Should().Be(0.0);
    }

    [Fact]
    public void CalculateRecallAtK_EmptyRelevant_ShouldReturnOne()
    {
        // Arrange - no relevant docs means perfect recall by definition
        var retrieved = new List<string> { "chunk-1" };
        var relevant = new List<string>();

        // Act
        var recall = _service.CalculateRecallAtK(retrieved, relevant, k: 5);

        // Assert
        recall.Should().Be(1.0);
    }

    [Fact]
    public void CalculateRecallAtK_KLessThanRetrieved_ShouldOnlyConsiderTopK()
    {
        // Arrange
        var retrieved = new List<string> { "irrelevant", "irrelevant2", "chunk-1", "chunk-2" };
        var relevant = new List<string> { "chunk-1", "chunk-2" };

        // Act
        var recallAt2 = _service.CalculateRecallAtK(retrieved, relevant, k: 2);
        var recallAt4 = _service.CalculateRecallAtK(retrieved, relevant, k: 4);

        // Assert
        recallAt2.Should().Be(0.0); // chunk-1/2 not in top 2
        recallAt4.Should().Be(1.0); // chunk-1/2 in top 4
    }

    #endregion

    #region CalculateNdcgAtK Tests

    [Fact]
    public void CalculateNdcgAtK_PerfectRanking_ShouldReturnOne()
    {
        // Arrange - all relevant docs ranked first
        var retrieved = new List<string> { "chunk-1", "chunk-2", "irrelevant" };
        var relevant = new List<string> { "chunk-1", "chunk-2" };

        // Act
        var ndcg = _service.CalculateNdcgAtK(retrieved, relevant, k: 3);

        // Assert
        ndcg.Should().Be(1.0);
    }

    [Fact]
    public void CalculateNdcgAtK_NoRelevantRetrieved_ShouldReturnZero()
    {
        // Arrange
        var retrieved = new List<string> { "irrelevant-1", "irrelevant-2" };
        var relevant = new List<string> { "chunk-1" };

        // Act
        var ndcg = _service.CalculateNdcgAtK(retrieved, relevant, k: 2);

        // Assert
        ndcg.Should().Be(0.0);
    }

    [Fact]
    public void CalculateNdcgAtK_EmptyRelevant_ShouldReturnZero()
    {
        // Arrange
        var retrieved = new List<string> { "chunk-1" };
        var relevant = new List<string>();

        // Act
        var ndcg = _service.CalculateNdcgAtK(retrieved, relevant, k: 5);

        // Assert
        ndcg.Should().Be(0.0);
    }

    [Fact]
    public void CalculateNdcgAtK_LowerRankedRelevant_ShouldBeLessThanOne()
    {
        // Arrange - relevant doc at position 3 instead of 1
        var retrieved = new List<string> { "irrelevant-1", "irrelevant-2", "chunk-1" };
        var relevant = new List<string> { "chunk-1" };

        // Act
        var ndcg = _service.CalculateNdcgAtK(retrieved, relevant, k: 3);

        // Assert
        ndcg.Should().BeLessThan(1.0);
        ndcg.Should().BeGreaterThan(0.0);
    }

    #endregion

    #region CalculateMrr Tests

    [Fact]
    public void CalculateMrr_FirstPositionHit_ShouldReturnOne()
    {
        // Arrange
        var retrieved = new List<string> { "chunk-1", "irrelevant" };
        var relevant = new List<string> { "chunk-1" };

        // Act
        var mrr = _service.CalculateMrr(retrieved, relevant);

        // Assert
        mrr.Should().Be(1.0);
    }

    [Fact]
    public void CalculateMrr_SecondPositionHit_ShouldReturnOneHalf()
    {
        // Arrange
        var retrieved = new List<string> { "irrelevant", "chunk-1" };
        var relevant = new List<string> { "chunk-1" };

        // Act
        var mrr = _service.CalculateMrr(retrieved, relevant);

        // Assert
        mrr.Should().Be(0.5);
    }

    [Fact]
    public void CalculateMrr_ThirdPositionHit_ShouldReturnOneThird()
    {
        // Arrange
        var retrieved = new List<string> { "irr-1", "irr-2", "chunk-1" };
        var relevant = new List<string> { "chunk-1" };

        // Act
        var mrr = _service.CalculateMrr(retrieved, relevant);

        // Assert
        mrr.Should().BeApproximately(1.0 / 3.0, 0.001);
    }

    [Fact]
    public void CalculateMrr_NoHit_ShouldReturnZero()
    {
        // Arrange
        var retrieved = new List<string> { "irr-1", "irr-2" };
        var relevant = new List<string> { "chunk-1" };

        // Act
        var mrr = _service.CalculateMrr(retrieved, relevant);

        // Assert
        mrr.Should().Be(0.0);
    }

    [Fact]
    public void CalculateMrr_EmptyRelevant_ShouldReturnOne()
    {
        // Arrange
        var retrieved = new List<string> { "chunk-1" };
        var relevant = new List<string>();

        // Act
        var mrr = _service.CalculateMrr(retrieved, relevant);

        // Assert
        mrr.Should().Be(1.0);
    }

    #endregion

    #region ComputeMetrics Tests

    [Fact]
    public void ComputeMetrics_EmptyResults_ShouldReturnEmptyMetrics()
    {
        // Act
        var metrics = _service.ComputeMetrics(new List<EvaluationSampleResult>());

        // Assert
        metrics.Should().Be(EvaluationMetrics.Empty);
    }

    [Fact]
    public void ComputeMetrics_AllSuccessful_ShouldComputeCorrectly()
    {
        // Arrange
        var results = new List<EvaluationSampleResult>
        {
            new()
            {
                SampleId = "s-001",
                Question = "Q1?",
                ExpectedAnswer = "A1",
                HitAt5 = true,
                HitAt10 = true,
                ReciprocalRank = 1.0,
                DcgAt10 = 1.0,
                IdealDcgAt10 = 1.0,
                AnswerCorrectness = 0.9,
                LatencyMs = 100.0
            },
            new()
            {
                SampleId = "s-002",
                Question = "Q2?",
                ExpectedAnswer = "A2",
                HitAt5 = false,
                HitAt10 = true,
                ReciprocalRank = 0.0,
                DcgAt10 = 0.0,
                IdealDcgAt10 = 1.0,
                AnswerCorrectness = 0.5,
                LatencyMs = 300.0
            }
        };

        // Act
        var metrics = _service.ComputeMetrics(results);

        // Assert
        metrics.RecallAt5.Should().Be(0.5);
        metrics.RecallAt10.Should().Be(1.0);
        metrics.Mrr.Should().Be(0.5);
        metrics.AnswerCorrectness.Should().Be(0.7);
        metrics.SampleCount.Should().Be(2);
    }

    [Fact]
    public void ComputeMetrics_WithFailedSamples_ShouldExcludeFromAverages()
    {
        // Arrange
        var results = new List<EvaluationSampleResult>
        {
            new()
            {
                SampleId = "s-001",
                Question = "Q1?",
                ExpectedAnswer = "A1",
                HitAt5 = true,
                HitAt10 = true,
                ReciprocalRank = 1.0,
                AnswerCorrectness = 1.0,
                LatencyMs = 100.0
            },
            new()
            {
                SampleId = "s-002",
                Question = "Q2?",
                ExpectedAnswer = "A2",
                ErrorMessage = "Service error",
                LatencyMs = 50.0
            }
        };

        // Act
        var metrics = _service.ComputeMetrics(results);

        // Assert
        metrics.SampleCount.Should().Be(1); // Only successful samples counted
        metrics.RecallAt5.Should().Be(1.0);
        metrics.AnswerCorrectness.Should().Be(1.0);
    }

    [Fact]
    public void ComputeMetrics_AllFailed_ShouldReturnEmptyMetrics()
    {
        // Arrange
        var results = new List<EvaluationSampleResult>
        {
            new()
            {
                SampleId = "s-001",
                Question = "Q1?",
                ExpectedAnswer = "A1",
                ErrorMessage = "Error 1"
            },
            new()
            {
                SampleId = "s-002",
                Question = "Q2?",
                ExpectedAnswer = "A2",
                ErrorMessage = "Error 2"
            }
        };

        // Act
        var metrics = _service.ComputeMetrics(results);

        // Assert
        metrics.Should().Be(EvaluationMetrics.Empty);
    }

    #endregion
}
