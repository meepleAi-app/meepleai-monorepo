using System.Diagnostics.Metrics;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Api.Observability;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SearchResultItem = Api.Services.SearchResultItem;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// SP5-b T3 — Verifies that <c>meepleai.rag.retrieval_empty</c> is incremented by 1
/// at the single detection site in <see cref="RagPromptAssemblyService.RetrieveRagContextAsync"/>
/// (<c>if (filteredChunks.Count == 0)</c>), and NOT incremented when chunks are present.
///
/// Single-source guarantee: the handler (<c>ChatWithSessionAgentCommandHandler</c>) does NOT
/// call <see cref="MeepleAiMetrics.RecordRetrievalEmpty"/>; only the service does.
/// </summary>
/// <remarks>
/// The [Collection] attribute serializes these tests to prevent a parallel T3-AC-1
/// (which fires the global counter) from contaminating the T3-AC-2 "must NOT fire" assertion.
/// </remarks>
[Collection("RetrievalEmptyMetrics")]
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Area", "Observability")]
[Trait("Issue", "2582")]
public class RagPromptAssemblyServiceRetrievalEmptyMetricsTests
{
    private const string CounterName = "meepleai.rag.retrieval_empty";

    private readonly Mock<IEmbeddingService> _embeddingMock = new();
    private readonly Mock<IEmbeddingRepository> _embeddingRepositoryMock = new();
    private readonly Mock<ICrossEncoderReranker> _rerankerMock = new();
    private readonly Mock<ILlmService> _llmMock = new();
    private readonly Mock<ITextChunkSearchService> _textSearchMock = new();
    private readonly Mock<IExpansionGameResolver> _expansionResolverMock = new();
    private readonly Mock<IRagEnhancementService> _ragEnhancementMock = new();
    private readonly Mock<IQueryComplexityClassifier> _complexityClassifierMock = new();
    private readonly Mock<IRetrievalRelevanceEvaluator> _relevanceEvaluatorMock = new();
    private readonly Mock<IQueryExpander> _queryExpanderMock = new();
    private readonly Mock<IGraphRetrievalService> _graphRetrievalMock = new();
    private readonly Mock<ILogger<RagPromptAssemblyService>> _loggerMock = new();

    private static readonly Guid TestGameId = Guid.NewGuid();
    private static readonly float[] TestEmbedding = [0.1f, 0.2f, 0.3f];

    public RagPromptAssemblyServiceRetrievalEmptyMetricsTests()
    {
        _llmMock
            .Setup(l => l.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult { Success = false });

        _expansionResolverMock
            .Setup(r => r.GetExpansionGameIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        _ragEnhancementMock
            .Setup(r => r.GetActiveEnhancementsAsync(It.IsAny<UserTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RagEnhancement.None);

        _embeddingRepositoryMock
            .Setup(r => r.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ScoredEmbedding>());

        SetupSuccessfulEmbedding();
    }

    private RagPromptAssemblyService CreateService()
    {
        return new RagPromptAssemblyService(
            _embeddingMock.Object,
            _embeddingRepositoryMock.Object,
            _rerankerMock.Object,
            _llmMock.Object,
            _textSearchMock.Object,
            _expansionResolverMock.Object,
            _ragEnhancementMock.Object,
            _complexityClassifierMock.Object,
            _relevanceEvaluatorMock.Object,
            _queryExpanderMock.Object,
            _graphRetrievalMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// MeterListener capture scoped to <c>meepleai.rag.retrieval_empty</c>.
    /// Uses tolerant Contain() assertion (global static counter may fire from concurrent tests).
    /// </summary>
    private sealed class RetrievalEmptyCapture : IDisposable
    {
        private readonly MeterListener _listener;
        public List<long> Measurements { get; } = new();

        public RetrievalEmptyCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == MeepleAiMetrics.MeterName
                        && instrument.Name == CounterName)
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _listener.SetMeasurementEventCallback<long>((_, value, _, _) => Measurements.Add(value));
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T3-AC-1: zero chunks → counter increments
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T3-AC-1: retrieval_empty increments by 1 when retrieval returns zero chunks")]
    public async Task WhenRetrievalReturnsZeroChunks_RetrievalEmptyCounterIncrements()
    {
        // Arrange — FTS returns empty list → allChunks stays empty → filteredChunks.Count == 0
        SetupEmptyTextSearch();
        using var capture = new RetrievalEmptyCapture();
        var service = CreateService();

        // Act
        var result = await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, null, "it", CancellationToken.None);

        // Assert — returned fallback prompt (expected for empty context) AND counter fired
        result.Should().NotBeNull();
        result.Citations.Should().BeEmpty("no chunks means no citations");
        capture.Measurements.Should().Contain(1L,
            "meepleai.rag.retrieval_empty must fire Add(1) when filteredChunks.Count == 0");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T3-AC-2: non-empty chunks → counter does NOT increment
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T3-AC-2: retrieval_empty does NOT increment when retrieval returns chunks")]
    public async Task WhenRetrievalReturnsChunks_RetrievalEmptyCounterDoesNotIncrement()
    {
        // Arrange — FTS returns chunks with scores above MinScore=0 (profileOverride).
        // With MinScore=0, RRF-normalised chunks survive the score filter.
        SetupTextSearchResults(
            CreateChunk(Guid.NewGuid().ToString(), 0, 0.95f, "Pawns move forward one square."));
        SetupRerankerPassthrough();
        var service = CreateService();

        // Arrange — bool flag toggled by listener while the call is in flight.
        // We track whether ANY measurement fired during the invocation window.
        bool counterFiredDuringCall = false;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == MeepleAiMetrics.MeterName
                    && instrument.Name == CounterName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) => counterFiredDuringCall = true);
        listener.Start();

        // Act — use MinScore=0 so the chunk survives the score filter
        await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, null, "it", CancellationToken.None,
            profileOverride: new RetrievalProfile(TopK: 5, MinScore: 0f, FtsTopK: 10, WindowRadius: 1));

        listener.Dispose();

        // Assert — the listener was active only for the duration of this call.
        // Parallel tests on the global counter are a known concern; however, the
        // test class name is unique to this suite and T3-AC-1 uses a separate Capture
        // instance. The listener is disposed immediately after Act, minimising the window.
        // If this assertion is flaky under heavy parallelism it is an environmental issue,
        // not a code defect — documented here per SP5-b cardinality rules.
        counterFiredDuringCall.Should().BeFalse(
            "meepleai.rag.retrieval_empty must NOT fire when filteredChunks.Count > 0");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Setup helpers (mirror FallbackMetricsTests)
    // ──────────────────────────────────────────────────────────────────────────

    private void SetupSuccessfulEmbedding()
    {
        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess([TestEmbedding]));
    }

    private void SetupEmptyTextSearch()
    {
        _textSearchMock
            .Setup(t => t.FullTextSearchAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TextChunkMatch>());
    }

    private void SetupTextSearchResults(params SearchResultItem[] items)
    {
        var ftsResults = items.Select(i => new TextChunkMatch(
            PdfDocumentId: Guid.TryParse(i.PdfId, out var pid) ? pid : Guid.NewGuid(),
            Content: i.Text,
            ChunkIndex: i.ChunkIndex,
            PageNumber: i.Page,
            Rank: i.Score)).ToList();
        _textSearchMock
            .Setup(t => t.FullTextSearchAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ftsResults);
    }

    private void SetupRerankerPassthrough()
    {
        _rerankerMock
            .Setup(r => r.RerankAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<RerankChunk>>(),
                It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyList<RerankChunk> chunks, int? topK, CancellationToken _) =>
            {
                var reranked = chunks.Take(topK ?? chunks.Count)
                    .Select((c, i) => new RerankedChunk(c.Id, c.Content, 0.9 - (i * 0.1), c.OriginalScore))
                    .ToList();
                return new RerankResult(reranked, "test-model", 10.0);
            });
    }

    private static SearchResultItem CreateChunk(string pdfId, int chunkIndex, float score, string text = "Rule text", int page = 1)
    {
        return new SearchResultItem
        {
            Score = score,
            Text = text,
            PdfId = pdfId,
            Page = page,
            ChunkIndex = chunkIndex
        };
    }
}
