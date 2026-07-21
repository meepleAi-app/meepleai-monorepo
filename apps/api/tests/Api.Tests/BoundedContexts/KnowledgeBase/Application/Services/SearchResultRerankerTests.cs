using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Issue #2708: cross-encoder reranking applied on the /agents/qa[/stream] paths.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class SearchResultRerankerTests
{
    private readonly Mock<ILogger> _loggerMock = new();

    private static SearchResultDto Dto(string id, string text, double score, int rank = 0)
        => new(VectorDocumentId: id, TextContent: text, PageNumber: 1, RelevanceScore: score, Rank: rank, SearchMethod: "hybrid");

    /// <summary>
    /// Reranker that returns the input in REVERSE order — proves the final ordering follows the
    /// cross-encoder, not the raw RRF order.
    /// </summary>
    private static Mock<ICrossEncoderReranker> ReverseReranker()
    {
        var mock = new Mock<ICrossEncoderReranker>();
        mock
            .Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<RerankChunk>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyList<RerankChunk> chunks, int? topK, CancellationToken _) =>
            {
                var reranked = chunks
                    .Reverse()
                    .Take(topK ?? chunks.Count)
                    .Select((c, i) => new RerankedChunk(c.Id, c.Content, c.OriginalScore, 1.0 - (i * 0.1)))
                    .ToList();
                return new RerankResult(reranked, "test-model", 1.0);
            });
        return mock;
    }

    [Fact]
    public async Task RerankAsync_ReordersResultsBySemanticRelevance()
    {
        var results = new List<SearchResultDto>
        {
            Dto("doc-a", "Pawns move forward.", 0.80),
            Dto("doc-b", "Knights move in an L.", 0.78),
            Dto("doc-c", "Castling rules.", 0.76),
        };
        var reranker = ReverseReranker();

        var reranked = await SearchResultReranker.RerankAsync(
            reranker.Object, "how do pawns move", results, topK: 3, _loggerMock.Object, CancellationToken.None);

        reranked.Select(r => r.VectorDocumentId).Should().Equal("doc-c", "doc-b", "doc-a");
    }

    [Fact]
    public async Task RerankAsync_SelectsTopKFromLargerCandidatePool()
    {
        // 5 candidates in, top 3 out — the reranker narrows the wider retrieval pool (Issue #2708).
        var results = Enumerable.Range(0, 5)
            .Select(i => Dto($"doc-{i}", $"chunk {i}", 0.80 - (i * 0.01)))
            .ToList();
        var reranker = ReverseReranker();

        var reranked = await SearchResultReranker.RerankAsync(
            reranker.Object, "query", results, topK: 3, _loggerMock.Object, CancellationToken.None);

        reranked.Should().HaveCount(3);
        reranked.Select(r => r.VectorDocumentId).Should().Equal("doc-4", "doc-3", "doc-2");
    }

    [Fact]
    public async Task RerankAsync_WhenRerankerThrows_FallsBackToRawTopK()
    {
        var results = new List<SearchResultDto>
        {
            Dto("doc-a", "x", 0.80),
            Dto("doc-b", "y", 0.78),
            Dto("doc-c", "z", 0.76),
        };
        var reranker = new Mock<ICrossEncoderReranker>();
        reranker
            .Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<RerankChunk>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("reranker service down"));

        var reranked = await SearchResultReranker.RerankAsync(
            reranker.Object, "query", results, topK: 2, _loggerMock.Object, CancellationToken.None);

        // Graceful degradation: raw top-2 in original order.
        reranked.Should().HaveCount(2);
        reranked.Select(r => r.VectorDocumentId).Should().Equal("doc-a", "doc-b");
    }

    [Fact]
    public async Task RerankAsync_SingleResult_SkipsRerankerCall()
    {
        var results = new List<SearchResultDto> { Dto("doc-a", "only chunk", 0.80) };
        var reranker = new Mock<ICrossEncoderReranker>();

        var reranked = await SearchResultReranker.RerankAsync(
            reranker.Object, "query", results, topK: 5, _loggerMock.Object, CancellationToken.None);

        reranked.Should().ContainSingle().Which.VectorDocumentId.Should().Be("doc-a");
        reranker.Verify(
            r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<RerankChunk>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------------
    // Slice B: HybridSearchResult overload — lets the PLAYGROUND path rerank
    // its List<HybridSearchResult> WITHOUT a lossy conversion to SearchResultDto
    // (which lacks ChunkIndex and would need a throwing Guid.Parse). The overload
    // returns the ORIGINAL objects reordered/trimmed, so every field survives.
    // ---------------------------------------------------------------------

    private static HybridSearchResult Hybrid(string chunkId, string content, float score, int chunkIndex)
        => new()
        {
            ChunkId = chunkId,
            Content = content,
            PdfDocumentId = "pdf-1",
            GameId = Guid.Empty,
            ChunkIndex = chunkIndex,
            PageNumber = 3,
            HybridScore = score,
            Mode = SearchMode.Hybrid,
            MatchedTerms = new List<string>(),
            RoleTags = GameBookRole.None,
        };

    [Fact]
    public async Task RerankAsync_HybridResults_ReordersBySemanticRelevance()
    {
        var results = new List<HybridSearchResult>
        {
            Hybrid("chunk-a", "Pawns move forward.", 0.80f, 0),
            Hybrid("chunk-b", "Knights move in an L.", 0.78f, 1),
            Hybrid("chunk-c", "Castling rules.", 0.76f, 2),
        };
        var reranker = ReverseReranker();

        var reranked = await SearchResultReranker.RerankAsync(
            reranker.Object, "how do pawns move", results, topK: 3, _loggerMock.Object, CancellationToken.None);

        reranked.Select(r => r.ChunkId).Should().Equal("chunk-c", "chunk-b", "chunk-a");
    }

    [Fact]
    public async Task RerankAsync_HybridResults_SelectsTopKFromLargerPool_PreservingChunkIndex()
    {
        // 20 candidates in, top 5 out — mirrors the playground retrieve-wide/rerank-narrow wiring.
        var results = Enumerable.Range(0, 20)
            .Select(i => Hybrid($"chunk-{i}", $"content {i}", 0.90f - (i * 0.01f), chunkIndex: i * 10))
            .ToList();
        var reranker = ReverseReranker();

        var reranked = await SearchResultReranker.RerankAsync(
            reranker.Object, "query", results, topK: 5, _loggerMock.Object, CancellationToken.None);

        reranked.Should().HaveCount(5);
        // ChunkIndex (dropped by any SearchResultDto conversion) must survive intact.
        reranked.Select(r => r.ChunkIndex).Should().Equal(190, 180, 170, 160, 150);
        reranked.Select(r => r.ChunkId).Should().Equal("chunk-19", "chunk-18", "chunk-17", "chunk-16", "chunk-15");
    }

    [Fact]
    public async Task RerankAsync_HybridResults_WhenRerankerThrows_FallsBackToRawTopK()
    {
        var results = new List<HybridSearchResult>
        {
            Hybrid("chunk-a", "x", 0.80f, 0),
            Hybrid("chunk-b", "y", 0.78f, 1),
            Hybrid("chunk-c", "z", 0.76f, 2),
        };
        var reranker = new Mock<ICrossEncoderReranker>();
        reranker
            .Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<RerankChunk>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("reranker service down"));

        var reranked = await SearchResultReranker.RerankAsync(
            reranker.Object, "query", results, topK: 2, _loggerMock.Object, CancellationToken.None);

        reranked.Should().HaveCount(2);
        reranked.Select(r => r.ChunkId).Should().Equal("chunk-a", "chunk-b");
    }

    [Fact]
    public async Task RerankAsync_HybridResults_SingleResult_SkipsRerankerCall()
    {
        var results = new List<HybridSearchResult> { Hybrid("chunk-a", "only chunk", 0.80f, 0) };
        var reranker = new Mock<ICrossEncoderReranker>();

        var reranked = await SearchResultReranker.RerankAsync(
            reranker.Object, "query", results, topK: 5, _loggerMock.Object, CancellationToken.None);

        reranked.Should().ContainSingle().Which.ChunkId.Should().Be("chunk-a");
        reranker.Verify(
            r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<RerankChunk>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------------
    // Pure index-mapping seam shared by both overloads (no infra/mocks).
    // ---------------------------------------------------------------------

    private static RerankedChunk Ranked(string id) => new(id, "c", 0.5, 0.9);

    [Fact]
    public void MapRerankedIndices_MapsIdsBackToOriginalsInOrder()
    {
        var results = new List<string> { "a", "b", "c" };
        var reranked = new List<RerankedChunk> { Ranked("2"), Ranked("0") };

        SearchResultReranker.MapRerankedIndices(results, reranked, topK: 3)
            .Should().Equal("c", "a");
    }

    [Fact]
    public void MapRerankedIndices_FiltersUnparseableAndOutOfRangeIds()
    {
        var results = new List<string> { "a", "b", "c" };
        var reranked = new List<RerankedChunk> { Ranked("5"), Ranked("x"), Ranked("1") };

        SearchResultReranker.MapRerankedIndices(results, reranked, topK: 3)
            .Should().Equal("b");
    }

    [Fact]
    public void MapRerankedIndices_NoMappableChunks_FallsBackToRawTopK()
    {
        var results = new List<string> { "a", "b", "c" };
        var reranked = new List<RerankedChunk> { Ranked("9"), Ranked("bad") };

        SearchResultReranker.MapRerankedIndices(results, reranked, topK: 2)
            .Should().Equal("a", "b");
    }
}
