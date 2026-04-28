using System.Diagnostics.Metrics;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
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
/// Verifies the <see cref="MeepleAiMetrics.RagRetrievalFallbacks"/> Counter is
/// emitted with the bounded <c>fallback_type</c> / <c>severity</c> tag set
/// from every fallback site in <see cref="RagPromptAssemblyService"/>.
///
/// The Counter feeds the Prometheus alert family
/// <c>RAGRetrievalFallback*</c> — silent regressions in any nested catch
/// block would degrade observability without a test in place.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Area", "Observability")]
public class RagPromptAssemblyServiceFallbackMetricsTests
{
    private const string CounterName = "meepleai.rag.retrieval.fallbacks";

    private readonly Mock<IEmbeddingService> _embeddingMock = new();
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

    public RagPromptAssemblyServiceFallbackMetricsTests()
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
    }

    private RagPromptAssemblyService CreateService()
    {
        return new RagPromptAssemblyService(
            _embeddingMock.Object,
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
    /// Captures Counter measurements for the fallback instrument and exposes a
    /// list of (fallback_type, severity) tag pairs for assertion.
    /// </summary>
    private sealed class FallbackCapture : IDisposable
    {
        private readonly MeterListener _listener;
        public List<(string FallbackType, string Severity)> Events { get; } = new();

        public FallbackCapture()
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
            _listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            {
                string fallbackType = "<missing>";
                string severity = "<missing>";
                foreach (var tag in tags)
                {
                    if (tag.Key == "fallback_type" && tag.Value is string ft)
                    {
                        fallbackType = ft;
                    }
                    else if (tag.Key == "severity" && tag.Value is string sv)
                    {
                        severity = sv;
                    }
                }
                Events.Add((fallbackType, severity));
            });
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    // ============================================================
    // Helper unit tests — verify the Counter contract directly.
    // ============================================================

    [Fact]
    public void RecordRetrievalFallback_DefaultSeverity_EmitsGracefulTag()
    {
        using var capture = new FallbackCapture();

        MeepleAiMetrics.RecordRetrievalFallback(MeepleAiMetrics.RagFallbackTypes.Reranker);

        capture.Events.Should().ContainSingle()
            .Which.Should().Be(("reranker", "graceful"));
    }

    [Fact]
    public void RecordRetrievalFallback_PartialLossSeverity_EmitsCorrectTag()
    {
        using var capture = new FallbackCapture();

        MeepleAiMetrics.RecordRetrievalFallback(
            MeepleAiMetrics.RagFallbackTypes.GraphTraversal,
            MeepleAiMetrics.RagFallbackSeverity.PartialLoss);

        capture.Events.Should().ContainSingle()
            .Which.Should().Be(("graph_traversal", "partial_loss"));
    }

    [Theory]
    [InlineData("reranker")]
    [InlineData("hybrid_search")]
    [InlineData("sentence_window")]
    [InlineData("query_expansion")]
    [InlineData("graph_traversal")]
    [InlineData("raptor")]
    [InlineData("unknown")]
    public void RagFallbackTypes_AllValuesAreStableAndKnown(string expected)
    {
        // Guards against accidental rename. The Prometheus alert family
        // (and any downstream Grafana dashboards) depends on these exact strings.
        var actual = expected switch
        {
            "reranker" => MeepleAiMetrics.RagFallbackTypes.Reranker,
            "hybrid_search" => MeepleAiMetrics.RagFallbackTypes.HybridSearch,
            "sentence_window" => MeepleAiMetrics.RagFallbackTypes.SentenceWindow,
            "query_expansion" => MeepleAiMetrics.RagFallbackTypes.QueryExpansion,
            "graph_traversal" => MeepleAiMetrics.RagFallbackTypes.GraphTraversal,
            "raptor" => MeepleAiMetrics.RagFallbackTypes.Raptor,
            "unknown" => MeepleAiMetrics.RagFallbackTypes.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(expected))
        };
        actual.Should().Be(expected);
    }

    // ============================================================
    // Integration tests — verify each fallback site invokes the helper.
    // ============================================================

    [Fact]
    public async Task QueryExpansion_WhenLlmThrows_EmitsQueryExpansionFallback()
    {
        // Arrange — force RAG-Fusion enhancement so query expansion runs,
        // then make the LLM throw so the catch block fires.
        _ragEnhancementMock
            .Setup(r => r.GetActiveEnhancementsAsync(It.IsAny<UserTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RagEnhancement.RagFusionQueries);

        _llmMock.Reset();
        _llmMock
            .Setup(l => l.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        SetupSuccessfulEmbedding();
        SetupEmptyTextSearch();

        using var capture = new FallbackCapture();
        var service = CreateService();

        // Act
        await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, null, "it", CancellationToken.None);

        // Assert
        capture.Events.Should().Contain(("query_expansion", "graceful"));
    }

    [Fact]
    public async Task Reranker_WhenThrows_EmitsRerankerFallback()
    {
        // Arrange — override the retrieval profile so chunks survive RRF-normalized
        // scoring (MinScore=0) and the rerank block is reached (TopK=1 < chunk count),
        // then make the reranker throw to trigger the fallback.
        SetupSuccessfulEmbedding();
        SetupTextSearchResults(
            CreateChunk(Guid.NewGuid().ToString(), 0, 0.95f, "Chunk 1"),
            CreateChunk(Guid.NewGuid().ToString(), 0, 0.90f, "Chunk 2"),
            CreateChunk(Guid.NewGuid().ToString(), 0, 0.85f, "Chunk 3"));

        _rerankerMock
            .Setup(r => r.RerankAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<RerankChunk>>(),
                It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Reranker service unreachable"));

        using var capture = new FallbackCapture();
        var service = CreateService();

        // Act
        await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, null, "it", CancellationToken.None,
            profileOverride: new RetrievalProfile(TopK: 1, MinScore: 0f, FtsTopK: 10, WindowRadius: 1));

        // Assert
        capture.Events.Should().Contain(("reranker", "graceful"));
    }

    [Fact]
    public async Task SentenceWindow_WhenAdjacentLookupThrows_EmitsSentenceWindowFallback()
    {
        // Arrange — override profile so RRF-normalized chunks survive (MinScore=0),
        // pass the reranker through, then make GetAdjacentChunksAsync throw.
        SetupSuccessfulEmbedding();
        SetupTextSearchResults(
            CreateChunk(Guid.NewGuid().ToString(), 0, 0.95f, "Chunk 1"),
            CreateChunk(Guid.NewGuid().ToString(), 0, 0.90f, "Chunk 2"));
        SetupRerankerPassthrough();

        _textSearchMock
            .Setup(t => t.GetAdjacentChunksAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB unreachable"));

        using var capture = new FallbackCapture();
        var service = CreateService();

        // Act
        await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, null, "it", CancellationToken.None,
            profileOverride: new RetrievalProfile(TopK: 5, MinScore: 0f, FtsTopK: 10, WindowRadius: 1));

        // Assert
        capture.Events.Should().Contain(("sentence_window", "graceful"));
    }

    [Fact]
    public async Task GraphTraversal_WhenServiceThrows_EmitsGraphTraversalFallback()
    {
        // Arrange — enable Graph RAG enhancement (requires non-null userTier),
        // override profile so chunks survive, then make the service throw.
        _ragEnhancementMock
            .Setup(r => r.GetActiveEnhancementsAsync(It.IsAny<UserTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RagEnhancement.GraphTraversal);

        _graphRetrievalMock
            .Setup(g => g.GetEntityContextAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Neo4j unreachable"));

        SetupSuccessfulEmbedding();
        SetupTextSearchResults(
            CreateChunk(Guid.NewGuid().ToString(), 0, 0.95f, "Chunk 1"));
        SetupRerankerPassthrough();

        using var capture = new FallbackCapture();
        var service = CreateService();

        // Act
        await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, UserTier.Free, "it", CancellationToken.None,
            profileOverride: new RetrievalProfile(TopK: 5, MinScore: 0f, FtsTopK: 10, WindowRadius: 1));

        // Assert
        capture.Events.Should().Contain(("graph_traversal", "graceful"));
    }

    [Fact]
    public async Task GraphTraversal_WhenReturnsEmpty_EmitsPartialLossFallback()
    {
        // Arrange — Graph RAG active (requires non-null userTier) but returns
        // empty context: partial-loss severity expected.
        _ragEnhancementMock
            .Setup(r => r.GetActiveEnhancementsAsync(It.IsAny<UserTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RagEnhancement.GraphTraversal);

        _graphRetrievalMock
            .Setup(g => g.GetEntityContextAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        SetupSuccessfulEmbedding();
        SetupTextSearchResults(
            CreateChunk(Guid.NewGuid().ToString(), 0, 0.95f, "Chunk 1"));
        SetupRerankerPassthrough();

        using var capture = new FallbackCapture();
        var service = CreateService();

        // Act
        await service.AssemblePromptAsync(
            "tutor", "Chess", null, "How do pawns move?",
            TestGameId, null, UserTier.Free, "it", CancellationToken.None,
            profileOverride: new RetrievalProfile(TopK: 5, MinScore: 0f, FtsTopK: 10, WindowRadius: 1));

        // Assert
        capture.Events.Should().Contain(("graph_traversal", "partial_loss"));
    }

    // ============================================================
    // Test setup helpers (mirror RagPromptAssemblyServiceTests).
    // ============================================================

    private void SetupSuccessfulEmbedding()
    {
        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess([TestEmbedding]));
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

    private void SetupEmptyTextSearch()
    {
        _textSearchMock
            .Setup(t => t.FullTextSearchAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TextChunkMatch>());
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
