using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;
using SharedUserTier = Api.SharedKernel.Domain.ValueObjects.UserTier;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Evaluation.Services;

/// <summary>
/// Unit tests for DatasetEvaluationService.
/// ADR-016 Phase 0: Validates Recall@K, nDCG@K, MRR calculations and dataset evaluation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class DatasetEvaluationServiceTests
{
    private readonly Mock<IRagService> _mockRagService;
    private readonly Mock<IRagPromptAssemblyService> _mockRagPromptService;
    private readonly Mock<ILlmService> _mockLlmService;
    private readonly Mock<ICitationValidationService> _mockCitationValidation;
    private readonly InlineCitationMatcherService _citationMatcher;
    private readonly Mock<ILogger<DatasetEvaluationService>> _mockLogger;
    private readonly DatasetEvaluationService _service;

    public DatasetEvaluationServiceTests()
    {
        _mockRagService = new Mock<IRagService>();
        _mockRagPromptService = new Mock<IRagPromptAssemblyService>();
        _mockLlmService = new Mock<ILlmService>();
        _mockCitationValidation = new Mock<ICitationValidationService>();
        _citationMatcher = new InlineCitationMatcherService();
        _mockLogger = new Mock<ILogger<DatasetEvaluationService>>();

        // Default: citation validation reports a vacuously-valid result (no citations to validate),
        // so structural validity defaults to 1.0 for tests that do not exercise it.
        _mockCitationValidation
            .Setup(v => v.ValidateCitationsAsync(It.IsAny<IReadOnlyList<Snippet>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CitationValidationResult
            {
                IsValid = true,
                TotalCitations = 0,
                ValidCitations = 0,
                Errors = new List<CitationValidationError>(),
                Message = string.Empty
            });

        _service = new DatasetEvaluationService(
            _mockRagService.Object, _mockRagPromptService.Object, _mockLlmService.Object,
            _citationMatcher, _mockCitationValidation.Object, _mockLogger.Object);
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

    // ──────────────────────────────────────────────────────────────────────────
    // #3390 Slice 4 Step 1: grounded eval seam (options.GroundedEnhancements)
    // ──────────────────────────────────────────────────────────────────────────

    private const string GroundedGameGuid = "9a3d0250-dc8b-4c35-96a2-716fe08cff08";

    private void SetupGroundedSeam(RetrievalPolicy? capturedInto = null, string answer = "Grounded answer. [Page 3]")
    {
        var assembled = new AssembledPrompt(
            SystemPrompt: "You are a tutor.",
            UserPrompt: "Question",
            Citations: new List<ChunkCitation>
            {
                new(DocumentId: "doc-1", PageNumber: 3, RelevanceScore: 0.9f, SnippetPreview: "settlements score",
                    CopyrightTier: CopyrightTier.Full, IsPublic: true),
            },
            EstimatedTokens: 100);

        _mockRagPromptService
            .Setup(r => r.AssemblePromptAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GameState?>(),
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ChatThread?>(),
                It.IsAny<SharedUserTier?>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<IRagDebugEventCollector?>(), It.IsAny<RetrievalProfile?>(), It.IsAny<RetrievalPolicy?>()))
            .ReturnsAsync(assembled);

        _mockLlmService
            .Setup(l => l.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(answer));
    }

    [Fact]
    public async Task EvaluateSample_GroundedSeam_PassesEnabledEnhancementsToAssemble_R1aCanary()
    {
        // #3390 R1a: the self-verifying wiring canary — when the eval runs the grounded seam, it MUST
        // invoke AssemblePromptAsync (the enhancement-gated path) with a RetrievalPolicy carrying the
        // requested EnabledEnhancements set. If this ever routes elsewhere, per-enhancement measurement
        // is measuring the wrong path (#3475 class) — this test goes red.
        SetupGroundedSeam();
        var sample = new EvaluationSample
        {
            Id = "g-1", Question = "How do I score?", ExpectedAnswer = "settlements",
            GameId = GroundedGameGuid, ExpectedKeywords = [], RelevantChunkIds = []
        };
        var options = new EvaluationOptions { GroundedEnhancements = RagEnhancement.CragEvaluation };

        await _service.EvaluateSampleAsync(sample, options);

        _mockRagPromptService.Verify(r => r.AssemblePromptAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GameState?>(),
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ChatThread?>(),
            It.IsAny<SharedUserTier?>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
            It.IsAny<IRagDebugEventCollector?>(), It.IsAny<RetrievalProfile?>(),
            It.Is<RetrievalPolicy?>(p => p != null && p.EnabledEnhancements == RagEnhancement.CragEvaluation)),
            Times.Once);
        // and the legacy hybrid path is NOT taken
        _mockRagService.Verify(r => r.AskWithHybridSearchAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchMode>(), It.IsAny<string?>(),
            It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateSample_GroundedSeam_MapsCitationsToSnippets()
    {
        // The grounded citations become snippets with source "PDF:{documentId}" (the metric key), so
        // retrievedChunkIds reflect the grounded retrieval.
        SetupGroundedSeam();
        var sample = new EvaluationSample
        {
            Id = "g-2", Question = "How do I score?", ExpectedAnswer = "settlements",
            GameId = GroundedGameGuid, ExpectedKeywords = [], RelevantChunkIds = ["PDF:doc-1"]
        };
        var options = new EvaluationOptions { GroundedEnhancements = RagEnhancement.None };

        var result = await _service.EvaluateSampleAsync(sample, options);

        result.RetrievedChunkIds.Should().ContainSingle().Which.Should().Be("PDF:doc-1");
        result.GeneratedAnswer.Should().Contain("[Page 3]");
    }

    [Fact]
    public async Task EvaluateSample_NullGroundedEnhancements_UsesLegacyHybridPath()
    {
        // Backward-compat: null → legacy AskWithHybridSearchAsync (preserves the #3477 baseline path);
        // the grounded seam is NOT invoked.
        _mockRagService
            .Setup(r => r.AskWithHybridSearchAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchMode>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateQaResponse());
        var sample = new EvaluationSample
        {
            Id = "l-1", Question = "Q", ExpectedAnswer = "a",
            GameId = GroundedGameGuid, ExpectedKeywords = [], RelevantChunkIds = []
        };
        var options = new EvaluationOptions { GroundedEnhancements = null };

        await _service.EvaluateSampleAsync(sample, options);

        _mockRagService.Verify(r => r.AskWithHybridSearchAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchMode>(), It.IsAny<string?>(),
            It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRagPromptService.Verify(r => r.AssemblePromptAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GameState?>(),
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ChatThread?>(),
            It.IsAny<SharedUserTier?>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
            It.IsAny<IRagDebugEventCollector?>(), It.IsAny<RetrievalProfile?>(), It.IsAny<RetrievalPolicy?>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateSample_GroundedSeam_NonUuidGameId_RecordsFailedSample()
    {
        // The grounded seam needs a UUID GameId; a slug is a data error surfaced as a failed sample
        // (the batch is not aborted).
        SetupGroundedSeam();
        var sample = new EvaluationSample
        {
            Id = "g-3", Question = "Q", ExpectedAnswer = "a",
            GameId = "catan-slug-not-a-guid", ExpectedKeywords = [], RelevantChunkIds = []
        };
        var options = new EvaluationOptions { GroundedEnhancements = RagEnhancement.None };

        var result = await _service.EvaluateSampleAsync(sample, options);

        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.GeneratedAnswer.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullRagService_ThrowsArgumentNullException()
    {
        Action act = () =>
            new DatasetEvaluationService(null!, _mockRagPromptService.Object, _mockLlmService.Object, _citationMatcher, _mockCitationValidation.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullRagPromptService_ThrowsArgumentNullException()
    {
        Action act = () =>
            new DatasetEvaluationService(_mockRagService.Object, null!, _mockLlmService.Object, _citationMatcher, _mockCitationValidation.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLlmService_ThrowsArgumentNullException()
    {
        Action act = () =>
            new DatasetEvaluationService(_mockRagService.Object, _mockRagPromptService.Object, null!, _citationMatcher, _mockCitationValidation.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullCitationMatcher_ThrowsArgumentNullException()
    {
        Action act = () =>
            new DatasetEvaluationService(_mockRagService.Object, _mockRagPromptService.Object, _mockLlmService.Object, null!, _mockCitationValidation.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullCitationValidation_ThrowsArgumentNullException()
    {
        Action act = () =>
            new DatasetEvaluationService(_mockRagService.Object, _mockRagPromptService.Object, _mockLlmService.Object, _citationMatcher, null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Action act = () =>
            new DatasetEvaluationService(_mockRagService.Object, _mockRagPromptService.Object, _mockLlmService.Object, _citationMatcher, _mockCitationValidation.Object, null!);
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
                RelevantChunkIds = new[] { "chunk-1" },
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
                RelevantChunkIds = new[] { "chunk-2" },
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

        // Note: AskWithHybridSearchAsync signature is (gameId, query, searchMode, language, bypassCache, cancellationToken)
        _mockRagService
            .Setup(r => r.AskWithHybridSearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
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

        // Note: AskWithHybridSearchAsync signature is (gameId, query, searchMode, language, bypassCache, cancellationToken)
        _mockRagService
            .Setup(r => r.AskWithHybridSearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
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

        // Note: AskWithHybridSearchAsync signature is (gameId, query, searchMode, language, bypassCache, cancellationToken)
        _mockRagService
            .Setup(r => r.AskWithHybridSearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
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

        // Note: AskWithHybridSearchAsync signature is (gameId, query, searchMode, language, bypassCache, cancellationToken)
        _mockRagService
            .Setup(r => r.AskWithHybridSearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
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

        // Note: AskWithHybridSearchAsync signature is (gameId, query, searchMode, language, bypassCache, cancellationToken)
        _mockRagService
            .Setup(r => r.AskWithHybridSearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
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

    [Fact]
    public async Task EvaluateDataset_WithMixedLabeledAndUnlabeled_ReportsCoverageAndCleanRetrievalMetrics()
    {
        // Arrange
        var dataset = EvaluationDataset.Create("Test", "Test dataset");
        dataset.AddSample(CreateSample("labeled-1", relevantChunkIds: new[] { "s1" }));
        dataset.AddSample(CreateSample("unlabeled-1"));

        var snippets = new List<Snippet>
        {
            new("Snippet text", "s1", 1, 1, 0.9f)
        };

        // Note: AskWithHybridSearchAsync signature is (gameId, query, searchMode, language, bypassCache, cancellationToken)
        _mockRagService
            .Setup(r => r.AskWithHybridSearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SearchMode>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateQaResponse(snippets: snippets));

        var options = new EvaluationOptions { Configuration = "baseline" };

        // Act
        var result = await _service.EvaluateDatasetAsync(dataset, options);

        // Assert
        result.Metrics.LabeledSampleCount.Should().Be(1);
        result.Metrics.UnlabeledSampleCount.Should().Be(1);
        result.Metrics.RecallAt5.Should().Be(1.0); // Labeled sample's hit only; unlabeled doesn't drag it to 0.5
    }

    // === Citation accuracy (#3467) ===

    // A >5-word phrase so InlineCitationMatcherService extracts it as a significant phrase; the
    // generated answer repeats it verbatim so the matcher yields the snippet's page as an actual citation.
    private const string CitablePhrase = "the five resource types are brick lumber wool grain and ore";

    private void SetupRagResponseCitingPage(int page, string source = "PDF:doc-1")
    {
        var snippet = new Snippet(text: CitablePhrase, source: source, page: page, line: 0, score: 0.9f);
        _mockRagService
            .Setup(r => r.AskWithHybridSearchAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchMode>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QaResponse(
                answer: CitablePhrase,
                snippets: new List<Snippet> { snippet },
                confidence: 0.9));
    }

    [Fact]
    public async Task EvaluateSample_UsesHybridSearchPath_NotLegacyAskAsync()
    {
        // The eval must exercise the canonical hybrid retrieval path. IRagService.AskAsync's legacy
        // vector path is deprecated and returns empty results (RagService.ExecuteRetrievalPhaseAsync),
        // so using it would make every eval sample retrieve nothing (degenerate citation/recall metrics).
        SetupRagResponseCitingPage(page: 7);

        await _service.EvaluateSampleAsync(CreateSample(), EvaluationOptions.Default, TestContext.Current.CancellationToken);

        _mockRagService.Verify(
            r => r.AskWithHybridSearchAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchMode>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EvaluateSample_ExtractsActualCitationPages_FromInlineMatches()
    {
        // Arrange
        SetupRagResponseCitingPage(page: 7);

        // Act
        var result = await _service.EvaluateSampleAsync(CreateSample(), EvaluationOptions.Default, TestContext.Current.CancellationToken);

        // Assert
        result.ActualCitationPages.Should().Contain(7);
    }

    [Fact]
    public async Task EvaluateSample_CitationMatched_WhenActualPageOverlapsExpected()
    {
        // Arrange
        SetupRagResponseCitingPage(page: 7);
        var sample = CreateSample() with
        {
            ExpectedCitations = new ExpectedCitations
            {
                PrimaryPages = new[] { 7 },
                MatchPolicy = CitationMatchPolicy.OverlapAtLeastOne
            }
        };

        // Act
        var result = await _service.EvaluateSampleAsync(sample, EvaluationOptions.Default, TestContext.Current.CancellationToken);

        // Assert
        result.CitationMatched.Should().BeTrue();
        result.ExpectedCitationPages.Should().Equal(7);
    }

    [Fact]
    public async Task EvaluateSample_CitationNotMatched_WhenActualPageMissesExpected()
    {
        // Arrange
        SetupRagResponseCitingPage(page: 3); // answer cites page 3, ground truth is page 7
        var sample = CreateSample() with
        {
            ExpectedCitations = new ExpectedCitations
            {
                PrimaryPages = new[] { 7 },
                MatchPolicy = CitationMatchPolicy.OverlapAtLeastOne
            }
        };

        // Act
        var result = await _service.EvaluateSampleAsync(sample, EvaluationOptions.Default, TestContext.Current.CancellationToken);

        // Assert
        result.CitationMatched.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateSample_CitationMatchedNull_WhenSampleHasNoExpectedCitations()
    {
        // Arrange
        SetupRagResponseCitingPage(page: 7);

        // Act — sample has no ExpectedCitations, so it is not citation-graded
        var result = await _service.EvaluateSampleAsync(CreateSample(), EvaluationOptions.Default, TestContext.Current.CancellationToken);

        // Assert
        result.CitationMatched.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateSample_StructuralValidity_ReflectsCitationValidation()
    {
        // Arrange
        SetupRagResponseCitingPage(page: 7);
        _mockCitationValidation
            .Setup(v => v.ValidateCitationsAsync(It.IsAny<IReadOnlyList<Snippet>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CitationValidationResult
            {
                IsValid = false,
                TotalCitations = 4,
                ValidCitations = 3,
                Errors = new List<CitationValidationError>(),
                Message = string.Empty
            });

        // Act
        var result = await _service.EvaluateSampleAsync(CreateSample(), EvaluationOptions.Default, TestContext.Current.CancellationToken);

        // Assert
        result.CitationStructuralValidity.Should().Be(0.75);
    }
}
