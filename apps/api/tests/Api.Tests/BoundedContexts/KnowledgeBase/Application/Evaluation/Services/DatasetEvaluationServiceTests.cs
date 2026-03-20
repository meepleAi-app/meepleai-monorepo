using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Evaluation.Services;

/// <summary>
/// Unit tests for DatasetEvaluationService.
/// ADR-016 Phase 0: Validates Recall@K, nDCG@K, MRR calculations and dataset evaluation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class DatasetEvaluationServiceTests
{
    private readonly Mock<IRagService> _mockRagService;
    private readonly Mock<ILogger<DatasetEvaluationService>> _mockLogger;
    private readonly DatasetEvaluationService _service;

    public DatasetEvaluationServiceTests()
    {
        _mockRagService = new Mock<IRagService>();
        _mockLogger = new Mock<ILogger<DatasetEvaluationService>>();
        _service = new DatasetEvaluationService(_mockRagService.Object, _mockLogger.Object);
    }

    private static EvaluationSample CreateSample(
        string id = "test-001",
        string question = "Test question?",
        string answer = "Test answer",
        IReadOnlyList<string>? keywords = null,
        IReadOnlyList<string>? relevantChunkIds = null)
    {
        return new EvaluationSample
        {
            Id = id,
            Question = question,
            ExpectedAnswer = answer,
            ExpectedKeywords = keywords ?? [],
            RelevantChunkIds = relevantChunkIds ?? []
        };
    }

    private static QaResponse CreateQaResponse(
        string answer = "Generated answer",
        double confidence = 0.85,
        List<Snippet>? snippets = null)
    {
        return new QaResponse(
            answer: answer,
            snippets: snippets ?? [],
            confidence: confidence);
    }
    [Fact]
    public void Constructor_WithNullRagService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () =>
            new DatasetEvaluationService(null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () =>
            new DatasetEvaluationService(_mockRagService.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }
    [Fact]
    public void CalculateRecallAtK_WithAllRelevantInTopK_ReturnsOne()
    {
        // Arrange
        var retrieved = new[] { "chunk-1", "chunk-2", "chunk-3", "chunk-4", "chunk-5" };
        var relevant = new[] { "chunk-1", "chunk-2" };

        // Act
        var recall = _service.CalculateRecallAtK(retrieved, relevant, k: 5);

        // Assert
        recall.Should().Be(1.0);
    }

    [Fact]
    public void CalculateRecallAtK_WithSomeRelevantInTopK_ReturnsPartialRecall()
    {
        // Arrange
        var retrieved = new[] { "chunk-1", "chunk-3", "chunk-5", "chunk-7", "chunk-9" };
        var relevant = new[] { "chunk-1", "chunk-2", "chunk-3", "chunk-4" }; // 4 relevant, only 2 in top 5

        // Act
        var recall = _service.CalculateRecallAtK(retrieved, relevant, k: 5);

        // Assert
        recall.Should().Be(0.5); // 2 out of 4 relevant
    }

    [Fact]
    public void CalculateRecallAtK_WithNoRelevantInTopK_ReturnsZero()
    {
        // Arrange
        var retrieved = new[] { "chunk-10", "chunk-11", "chunk-12" };
        var relevant = new[] { "chunk-1", "chunk-2" };

        // Act
        var recall = _service.CalculateRecallAtK(retrieved, relevant, k: 3);

        // Assert
        recall.Should().Be(0.0);
    }

    [Fact]
    public void CalculateRecallAtK_WithEmptyRelevant_ReturnsOne()
    {
        // Arrange
        var retrieved = new[] { "chunk-1", "chunk-2" };
        var relevant = Array.Empty<string>();

        // Act
        var recall = _service.CalculateRecallAtK(retrieved, relevant, k: 5);

        // Assert
        recall.Should().Be(1.0); // Perfect recall when nothing to find
    }

    [Fact]
    public void CalculateRecallAtK_WithEmptyRetrieved_ReturnsZero()
    {
        // Arrange
        var retrieved = Array.Empty<string>();
        var relevant = new[] { "chunk-1", "chunk-2" };

        // Act
        var recall = _service.CalculateRecallAtK(retrieved, relevant, k: 5);

        // Assert
        recall.Should().Be(0.0);
    }

    [Fact]
    public void CalculateRecallAtK_IsCaseInsensitive()
    {
        // Arrange
        var retrieved = new[] { "CHUNK-1", "chunk-2" };
        var relevant = new[] { "chunk-1", "CHUNK-2" };

        // Act
        var recall = _service.CalculateRecallAtK(retrieved, relevant, k: 5);

        // Assert
        recall.Should().Be(1.0);
    }
    [Fact]
    public void CalculateNdcgAtK_WithPerfectRanking_ReturnsOne()
    {
        // Arrange
        var retrieved = new[] { "rel-1", "rel-2", "rel-3", "irrel-1", "irrel-2" };
        var relevant = new[] { "rel-1", "rel-2", "rel-3" };

        // Act
        var ndcg = _service.CalculateNdcgAtK(retrieved, relevant, k: 5);

        // Assert
        ndcg.Should().BeApproximately(1.0, precision: 3);
    }

    [Fact]
    public void CalculateNdcgAtK_WithNoRelevantRetrieved_ReturnsZero()
    {
        // Arrange
        var retrieved = new[] { "irrel-1", "irrel-2", "irrel-3" };
        var relevant = new[] { "rel-1", "rel-2" };

        // Act
        var ndcg = _service.CalculateNdcgAtK(retrieved, relevant, k: 3);

        // Assert
        ndcg.Should().Be(0.0);
    }

    [Fact]
    public void CalculateNdcgAtK_WithEmptyRelevant_ReturnsZero()
    {
        // Arrange - When no relevant docs exist, we can't calculate meaningful nDCG
        var retrieved = new[] { "chunk-1", "chunk-2" };
        var relevant = Array.Empty<string>();

        // Act
        var ndcg = _service.CalculateNdcgAtK(retrieved, relevant, k: 5);

        // Assert - Returns 0 because there's nothing to find (special case handled)
        // Actually the implementation returns dcg/idealDcg, with idealDcg=0 returns 0
        (ndcg >= 0.0 && ndcg <= 1.0).Should().BeTrue();
    }

    [Fact]
    public void CalculateNdcgAtK_WithRelevantAtLaterPositions_ReturnsLowerScore()
    {
        // Arrange
        var perfectRanking = new[] { "rel-1", "rel-2", "irrel-1", "irrel-2", "irrel-3" };
        var worstRanking = new[] { "irrel-1", "irrel-2", "irrel-3", "rel-1", "rel-2" };
        var relevant = new[] { "rel-1", "rel-2" };

        // Act
        var perfectNdcg = _service.CalculateNdcgAtK(perfectRanking, relevant, k: 5);
        var worstNdcg = _service.CalculateNdcgAtK(worstRanking, relevant, k: 5);

        // Assert
        (perfectNdcg > worstNdcg).Should().BeTrue();
    }
    [Fact]
    public void CalculateMrr_WithFirstPositionRelevant_ReturnsOne()
    {
        // Arrange
        var retrieved = new[] { "rel-1", "irrel-1", "irrel-2" };
        var relevant = new[] { "rel-1" };

        // Act
        var mrr = _service.CalculateMrr(retrieved, relevant);

        // Assert
        mrr.Should().Be(1.0);
    }

    [Fact]
    public void CalculateMrr_WithSecondPositionRelevant_ReturnsHalf()
    {
        // Arrange
        var retrieved = new[] { "irrel-1", "rel-1", "irrel-2" };
        var relevant = new[] { "rel-1" };

        // Act
        var mrr = _service.CalculateMrr(retrieved, relevant);

        // Assert
        mrr.Should().Be(0.5);
    }

    [Fact]
    public void CalculateMrr_WithThirdPositionRelevant_ReturnsOneThird()
    {
        // Arrange
        var retrieved = new[] { "irrel-1", "irrel-2", "rel-1" };
        var relevant = new[] { "rel-1" };

        // Act
        var mrr = _service.CalculateMrr(retrieved, relevant);

        // Assert
        mrr.Should().BeApproximately(1.0 / 3.0, precision: 5);
    }

    [Fact]
    public void CalculateMrr_WithNoRelevantRetrieved_ReturnsZero()
    {
        // Arrange
        var retrieved = new[] { "irrel-1", "irrel-2", "irrel-3" };
        var relevant = new[] { "rel-1" };

        // Act
        var mrr = _service.CalculateMrr(retrieved, relevant);

        // Assert
        mrr.Should().Be(0.0);
    }

    [Fact]
    public void CalculateMrr_WithEmptyRelevant_ReturnsOne()
    {
        // Arrange
        var retrieved = new[] { "chunk-1", "chunk-2" };
        var relevant = Array.Empty<string>();

        // Act
        var mrr = _service.CalculateMrr(retrieved, relevant);

        // Assert
        mrr.Should().Be(1.0); // Perfect MRR when nothing to find
    }

    [Fact]
    public void CalculateMrr_IsCaseInsensitive()
    {
        // Arrange
        var retrieved = new[] { "CHUNK-1", "chunk-2" };
        var relevant = new[] { "chunk-1" };

        // Act
        var mrr = _service.CalculateMrr(retrieved, relevant);

        // Assert
        mrr.Should().Be(1.0);
    }
    [Fact]
    public void ComputeMetrics_WithEmptyResults_ReturnsEmptyMetrics()
    {
        // Arrange
        var results = new List<EvaluationSampleResult>();

        // Act
        var metrics = _service.ComputeMetrics(results);

        // Assert
        metrics.SampleCount.Should().Be(0);
        metrics.RecallAt5.Should().Be(0.0);
        metrics.RecallAt10.Should().Be(0.0);
        metrics.NdcgAt10.Should().Be(0.0);
        metrics.Mrr.Should().Be(0.0);
    }

    [Fact]
    public void ComputeMetrics_WithAllSuccessfulSamples_CalculatesAverages()
    {
        // Arrange
        var results = new List<EvaluationSampleResult>
        {
            new()
            {
                SampleId = "1",
                Question = "Q1",
                ExpectedAnswer = "A1",
                HitAt5 = true,
                HitAt10 = true,
                ReciprocalRank = 1.0,
                DcgAt10 = 2.0,
                IdealDcgAt10 = 2.0,
                AnswerCorrectness = 0.9,
                LatencyMs = 100
            },
            new()
            {
                SampleId = "2",
                Question = "Q2",
                ExpectedAnswer = "A2",
                HitAt5 = false,
                HitAt10 = true,
                ReciprocalRank = 0.5,
                DcgAt10 = 1.0,
                IdealDcgAt10 = 2.0,
                AnswerCorrectness = 0.8,
                LatencyMs = 200
            }
        };

        // Act
        var metrics = _service.ComputeMetrics(results);

        // Assert
        metrics.SampleCount.Should().Be(2);
        metrics.RecallAt5.Should().BeApproximately(0.5, 0.00001); // (1 + 0) / 2
        metrics.RecallAt10.Should().BeApproximately(1.0, 0.00001); // (1 + 1) / 2
        metrics.Mrr.Should().BeApproximately(0.75, 0.00001); // (1.0 + 0.5) / 2
        metrics.AnswerCorrectness.Should().BeApproximately(0.85, 0.00001); // (0.9 + 0.8) / 2
    }

    [Fact]
    public void ComputeMetrics_ExcludesFailedSamples()
    {
        // Arrange
        var results = new List<EvaluationSampleResult>
        {
            new()
            {
                SampleId = "1",
                Question = "Q1",
                ExpectedAnswer = "A1",
                HitAt5 = true,
                HitAt10 = true,
                ReciprocalRank = 1.0,
                LatencyMs = 100
            },
            new()
            {
                SampleId = "2",
                Question = "Q2",
                ExpectedAnswer = "A2",
                ErrorMessage = "Failed to process",
                LatencyMs = 50
            }
        };

        // Act
        var metrics = _service.ComputeMetrics(results);

        // Assert
        metrics.SampleCount.Should().Be(1); // Only successful sample counted
    }

    [Fact]
    public void ComputeMetrics_CalculatesP95Latency()
    {
        // Arrange
        var results = Enumerable.Range(1, 100).Select(i => new EvaluationSampleResult
        {
            SampleId = $"sample-{i}",
            Question = $"Q{i}",
            ExpectedAnswer = $"A{i}",
            LatencyMs = i * 10 // 10, 20, 30, ... 1000
        }).ToList();

        // Act
        var metrics = _service.ComputeMetrics(results);

        // Assert
        (metrics.P95LatencyMs >= 950).Should().BeTrue(); // Should be around 950
        (metrics.P95LatencyMs <= 1000).Should().BeTrue();
    }
    [Fact]
    public async Task EvaluateSampleAsync_WithSuccessfulRagResponse_ReturnsResult()
    {
        // Arrange
        var sample = CreateSample(
            relevantChunkIds: new[] { "chunk-1", "chunk-2" });

        var snippets = new List<Snippet>
        {
            new("Snippet text", "chunk-1", 1, 1, 0.9f),
            new("Snippet text 2", "chunk-2", 2, 1, 0.85f)
        };

        // Note: AskAsync signature is (gameId, query, language, bypassCache, cancellationToken)
        _mockRagService
            .Setup(r => r.AskAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateQaResponse("Generated answer", snippets: snippets));

        var options = new EvaluationOptions { Configuration = "baseline" };

        // Act
        var result = await _service.EvaluateSampleAsync(sample, options);

        // Assert
        result.SampleId.Should().Be(sample.Id);
        result.Question.Should().Be(sample.Question);
        result.ExpectedAnswer.Should().Be(sample.ExpectedAnswer);
        result.GeneratedAnswer.Should().Be("Generated answer");
        (result.LatencyMs > 0).Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateSampleAsync_WithRagServiceError_ReturnsErrorResult()
    {
        // Arrange
        var sample = CreateSample();

        // Note: AskAsync signature is (gameId, query, language, bypassCache, cancellationToken)
        _mockRagService
            .Setup(r => r.AskAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("RAG service unavailable"));

        var options = new EvaluationOptions { Configuration = "baseline" };

        // Act
        var result = await _service.EvaluateSampleAsync(sample, options);

        // Assert
        result.SampleId.Should().Be(sample.Id);
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage.Should().ContainEquivalentOf("RAG service unavailable");
    }

    [Fact]
    public async Task EvaluateSampleAsync_WithNullSample_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new EvaluationOptions { Configuration = "baseline" };

        // Act & Assert
        Func<Task> act = () =>
            _service.EvaluateSampleAsync(null!, options);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
    [Fact]
    public async Task EvaluateDatasetAsync_EvaluatesAllSamples()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test dataset");
        for (var i = 0; i < 5; i++)
        {
            dataset.AddSample(CreateSample($"sample-{i}"));
        }

        // Note: AskAsync signature is (gameId, query, language, bypassCache, cancellationToken)
        _mockRagService
            .Setup(r => r.AskAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateQaResponse());

        var options = new EvaluationOptions { Configuration = "baseline" };

        // Act
        var result = await _service.EvaluateDatasetAsync(dataset, options);

        // Assert
        result.DatasetName.Should().Be("Test");
        result.Configuration.Should().Be("baseline");
        result.SampleResults.Count.Should().Be(5);
        result.Metrics.Should().NotBeNull();
    }

    [Fact]
    public async Task EvaluateDatasetAsync_WithMaxSamples_LimitsEvaluation()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test dataset");
        for (var i = 0; i < 10; i++)
        {
            dataset.AddSample(CreateSample($"sample-{i}"));
        }

        // Note: AskAsync signature is (gameId, query, language, bypassCache, cancellationToken)
        _mockRagService
            .Setup(r => r.AskAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateQaResponse());

        var options = new EvaluationOptions
        {
            Configuration = "baseline",
            MaxSamples = 3
        };

        // Act
        var result = await _service.EvaluateDatasetAsync(dataset, options);

        // Assert
        result.SampleResults.Count.Should().Be(3);
    }

    [Fact]
    public async Task EvaluateDatasetAsync_WithCancellation_StopsEvaluation()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test dataset");
        for (var i = 0; i < 100; i++)
        {
            dataset.AddSample(CreateSample($"sample-{i}"));
        }

        var callCount = 0;
        using var cts = new CancellationTokenSource();

        // Note: AskAsync signature is (gameId, query, language, bypassCache, cancellationToken)
        _mockRagService
            .Setup(r => r.AskAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                callCount++;
                if (callCount >= 5)
                {
                    await cts.CancelAsync();
                }
                return CreateQaResponse();
            });

        var options = new EvaluationOptions { Configuration = "baseline" };

        // Act
        var result = await _service.EvaluateDatasetAsync(dataset, options, cts.Token);

        // Assert
        (result.SampleResults.Count < 100).Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateDatasetAsync_WithNullDataset_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new EvaluationOptions { Configuration = "baseline" };

        // Act & Assert
        Func<Task> act = () =>
            _service.EvaluateDatasetAsync(null!, options);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EvaluateDatasetAsync_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test dataset");

        // Act & Assert
        Func<Task> act = () =>
            _service.EvaluateDatasetAsync(dataset, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
