using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for MultiGameHybridSearchService.
/// Tests parallel-per-game orchestration + aggregation + deterministic ordering.
/// Issue #1661: cross-game KB search.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class MultiGameHybridSearchServiceTests
{
    private readonly Mock<IHybridSearchService> _hybridSearchMock = new(MockBehavior.Strict);
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();

    // Each per-game search resolves IHybridSearchService from its OWN DI scope (own DbContext).
    // Wire the scope factory so every CreateScope() yields a provider returning the single mock,
    // keeping the existing Setup/Verify on _hybridSearchMock valid while exercising the scope path.
    private MultiGameHybridSearchService CreateSut()
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IHybridSearchService))).Returns(_hybridSearchMock.Object);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(provider.Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scope.Object);
        return new(_scopeFactoryMock.Object, NullLogger<MultiGameHybridSearchService>.Instance);
    }

    // ---------------------------------------------------------------------------
    // EC-1: empty gameIds → empty result, no calls to HybridSearchService
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EmptyGameIds_ReturnsEmpty_WithoutCallingHybridSearch()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.SearchAsync("catan rules", Array.Empty<Guid>(), limit: 10);

        // Assert — no interaction expected (MockBehavior.Strict would blow up)
        result.Should().BeEmpty();
        _hybridSearchMock.VerifyNoOtherCalls();
    }

    // ---------------------------------------------------------------------------
    // Sanity: single game delegates to IHybridSearchService
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SingleGame_DelegatesToHybridSearch_AndReturnsResults()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var fakeResults = BuildResults(gameId, count: 3, baseScore: 0.9f);

        _hybridSearchMock
            .Setup(s => s.SearchAsync(
                "test query", gameId,
                SearchMode.Hybrid,
                It.IsAny<int>(),
                null,
                0.7f, 0.3f, 0.0,
                GameBookRole.None,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeResults);

        var sut = CreateSut();

        // Act
        var result = await sut.SearchAsync("test query", new[] { gameId }, limit: 10);

        // Assert
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(r => r.GameId.Should().Be(gameId));
    }

    // ---------------------------------------------------------------------------
    // Multi-game: parallel calls (once per gameId)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MultipleGames_CallsHybridSearchInParallel_OncePerGame()
    {
        // Arrange
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        var gameId3 = Guid.NewGuid();

        SetupHybridSearchForGame(gameId1, count: 2, baseScore: 0.8f);
        SetupHybridSearchForGame(gameId2, count: 2, baseScore: 0.7f);
        SetupHybridSearchForGame(gameId3, count: 2, baseScore: 0.6f);

        var sut = CreateSut();

        // Act
        await sut.SearchAsync("test", new[] { gameId1, gameId2, gameId3 }, limit: 20);

        // Assert — each game called exactly once
        _hybridSearchMock.Verify(
            s => s.SearchAsync(
                "test", gameId1,
                It.IsAny<SearchMode>(), It.IsAny<int>(), null,
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _hybridSearchMock.Verify(
            s => s.SearchAsync(
                "test", gameId2,
                It.IsAny<SearchMode>(), It.IsAny<int>(), null,
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _hybridSearchMock.Verify(
            s => s.SearchAsync(
                "test", gameId3,
                It.IsAny<SearchMode>(), It.IsAny<int>(), null,
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Regression (#2480): each per-game search runs in its OWN DI scope so it gets
    // its OWN DbContext. Sharing the request-scoped DbContext across the concurrent
    // per-game searches threw "A second operation was started on this context
    // instance…" and made every cross-game search return 0 results.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MultipleGames_CreatesOneDiScopePerGame()
    {
        // Arrange
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        var gameId3 = Guid.NewGuid();

        SetupHybridSearchForGame(gameId1, count: 1, baseScore: 0.8f);
        SetupHybridSearchForGame(gameId2, count: 1, baseScore: 0.7f);
        SetupHybridSearchForGame(gameId3, count: 1, baseScore: 0.6f);

        var sut = CreateSut();

        // Act
        await sut.SearchAsync("test", new[] { gameId1, gameId2, gameId3 }, limit: 20);

        // Assert — one scope (→ one DbContext) per game, never a shared context.
        _scopeFactoryMock.Verify(f => f.CreateScope(), Times.Exactly(3));
    }

    // ---------------------------------------------------------------------------
    // Aggregation: results from all games are combined
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MultipleGames_AggregatesResultsAcrossGames()
    {
        // Arrange
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();

        SetupHybridSearchForGame(gameId1, count: 5, baseScore: 0.9f);
        SetupHybridSearchForGame(gameId2, count: 5, baseScore: 0.8f);

        var sut = CreateSut();

        // Act — limit higher than total
        var result = await sut.SearchAsync("test", new[] { gameId1, gameId2 }, limit: 20);

        // Assert — both games contribute
        result.Should().HaveCount(10);
        result.Count(r => r.GameId == gameId1).Should().Be(5);
        result.Count(r => r.GameId == gameId2).Should().Be(5);
    }

    // ---------------------------------------------------------------------------
    // EC-4: deterministic ordering — score DESC, then ChunkIndex ASC
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MultipleGames_OrdersByScoreDescThenChunkIndexAsc()
    {
        // Arrange
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();

        // game1 → scores 0.5, 0.3
        _hybridSearchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), gameId1,
                It.IsAny<SearchMode>(), It.IsAny<int>(), null,
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>
            {
                MakeResult(gameId1, chunkIndex: 2, score: 0.3f),
                MakeResult(gameId1, chunkIndex: 1, score: 0.5f),
            });

        // game2 → score 0.5 (tie with game1 chunk), chunkIndex 0
        _hybridSearchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), gameId2,
                It.IsAny<SearchMode>(), It.IsAny<int>(), null,
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>
            {
                MakeResult(gameId2, chunkIndex: 0, score: 0.5f),
            });

        var sut = CreateSut();

        // Act
        var result = await sut.SearchAsync("test", new[] { gameId1, gameId2 }, limit: 10);

        // Assert ordering: 0.5/chunk0 ≤ 0.5/chunk1 by chunkIndex → game2(idx0) < game1(idx1)
        // so order: [0.5,chunkIdx0], [0.5,chunkIdx1], [0.3,chunkIdx2]
        result.Should().HaveCount(3);
        result[0].HybridScore.Should().Be(0.5f);
        result[0].ChunkIndex.Should().Be(0);  // tie broken by chunkIndex ASC
        result[1].HybridScore.Should().Be(0.5f);
        result[1].ChunkIndex.Should().Be(1);
        result[2].HybridScore.Should().Be(0.3f);
    }

    // ---------------------------------------------------------------------------
    // #2568: cross-game ranking degeneracy. Per-game RRF is rank-only, so EVERY
    // game's rank-1 chunk gets the IDENTICAL fused HybridScore (0.7/(60+1) ≈ 0.01148).
    // With only a ChunkIndex/PdfDocumentId tiebreak the merge picks the same
    // query-agnostic (lowest-index / lowest-GUID) chunk for every cross-game query.
    // The tie MUST be broken by the globally-comparable raw vector cosine
    // (VectorScore) DESC so the chunk actually most relevant to THIS query wins.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task TiedHybridScore_BrokenByVectorScoreDesc_NotChunkIndex()
    {
        // Arrange
        var lowRelevanceGame = Guid.NewGuid();
        var highRelevanceGame = Guid.NewGuid();
        const float tiedHybrid = 0.01148f; // the rank-1 RRF value every game ties on

        // low-relevance game: chunkIndex 0 → would WIN the legacy ChunkIndex-ASC
        // tiebreak, but only a weak cosine match to the query.
        _hybridSearchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), lowRelevanceGame,
                It.IsAny<SearchMode>(), It.IsAny<int>(), null,
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>
            {
                MakeResultWithScores(lowRelevanceGame, chunkIndex: 0, hybridScore: tiedHybrid, vectorScore: 0.45f),
            });

        // high-relevance game: chunkIndex 7 → would LOSE the legacy tiebreak, but a
        // strong cosine match → it is the genuinely relevant result for the query.
        _hybridSearchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), highRelevanceGame,
                It.IsAny<SearchMode>(), It.IsAny<int>(), null,
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>
            {
                MakeResultWithScores(highRelevanceGame, chunkIndex: 7, hybridScore: tiedHybrid, vectorScore: 0.92f),
            });

        var sut = CreateSut();

        // Act
        var result = await sut.SearchAsync("test", new[] { lowRelevanceGame, highRelevanceGame }, limit: 10);

        // Assert — higher cosine wins the HybridScore tie, despite higher ChunkIndex
        result.Should().HaveCount(2);
        result[0].GameId.Should().Be(highRelevanceGame,
            "the higher raw cosine (VectorScore) must win a HybridScore tie, not the lower ChunkIndex");
        result[0].VectorScore.Should().Be(0.92f);
        result[1].GameId.Should().Be(lowRelevanceGame);
    }

    [Fact]
    public async Task TiedHybridScoreAndVectorScore_FallsBackToChunkIndexAsc()
    {
        // Arrange — when cosine ALSO ties, the deterministic ChunkIndex-ASC fallback
        // still applies (preserves stable ordering, no nondeterminism).
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        const float tiedHybrid = 0.5f;
        const float tiedVector = 0.6f;

        _hybridSearchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), gameId1,
                It.IsAny<SearchMode>(), It.IsAny<int>(), null,
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>
            {
                MakeResultWithScores(gameId1, chunkIndex: 3, hybridScore: tiedHybrid, vectorScore: tiedVector),
            });

        _hybridSearchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), gameId2,
                It.IsAny<SearchMode>(), It.IsAny<int>(), null,
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>
            {
                MakeResultWithScores(gameId2, chunkIndex: 1, hybridScore: tiedHybrid, vectorScore: tiedVector),
            });

        var sut = CreateSut();

        // Act
        var result = await sut.SearchAsync("test", new[] { gameId1, gameId2 }, limit: 10);

        // Assert — cosine ties → ChunkIndex ASC fallback → chunkIndex 1 before 3
        result.Should().HaveCount(2);
        result[0].ChunkIndex.Should().Be(1);
        result[1].ChunkIndex.Should().Be(3);
    }

    // ---------------------------------------------------------------------------
    // EC-7: limit truncates final result
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Limit_TruncatesFinalResultSet()
    {
        // Arrange
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();

        SetupHybridSearchForGame(gameId1, count: 10, baseScore: 0.9f);
        SetupHybridSearchForGame(gameId2, count: 10, baseScore: 0.8f);

        var sut = CreateSut();

        // Act
        var result = await sut.SearchAsync("test", new[] { gameId1, gameId2 }, limit: 7);

        // Assert
        result.Should().HaveCount(7);
    }

    // ---------------------------------------------------------------------------
    // Essential: each result carries the origin gameId
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task EachResult_CarriesItsOriginGameId()
    {
        // Arrange
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();

        SetupHybridSearchForGame(gameId1, count: 3, baseScore: 0.9f);
        SetupHybridSearchForGame(gameId2, count: 3, baseScore: 0.8f);

        var sut = CreateSut();

        // Act
        var result = await sut.SearchAsync("test", new[] { gameId1, gameId2 }, limit: 20);

        // Assert — no result should have an empty/default gameId
        result.Should().AllSatisfy(r => r.GameId.Should().NotBe(Guid.Empty));

        // Game IDs found match the input set
        var foundGameIds = result.Select(r => r.GameId).Distinct().ToList();
        foundGameIds.Should().Contain(gameId1);
        foundGameIds.Should().Contain(gameId2);
    }

    // ---------------------------------------------------------------------------
    // CancellationToken: propagated to all parallel calls
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CancellationToken_Propagated_ToAllParallelCalls()
    {
        // Arrange
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        _hybridSearchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<SearchMode>(), It.IsAny<int>(), null,
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), cts.Token))
            .ReturnsAsync(new List<HybridSearchResult>());

        var sut = CreateSut();

        // Act
        await sut.SearchAsync("test", new[] { gameId1, gameId2 }, limit: 10,
            cancellationToken: cts.Token);

        // Assert — the specific token was passed to both calls
        _hybridSearchMock.Verify(
            s => s.SearchAsync(
                It.IsAny<string>(), gameId1,
                It.IsAny<SearchMode>(), It.IsAny<int>(), null,
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), cts.Token),
            Times.Once);

        _hybridSearchMock.Verify(
            s => s.SearchAsync(
                It.IsAny<string>(), gameId2,
                It.IsAny<SearchMode>(), It.IsAny<int>(), null,
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), cts.Token),
            Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Resilience: single-game exception → logs warn, continues with other games
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SingleGameException_DoesNotFailWholeSearch_ReturnsOtherGamesResults()
    {
        // Arrange
        var failingGameId = Guid.NewGuid();
        var goodGameId = Guid.NewGuid();

        _hybridSearchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), failingGameId,
                It.IsAny<SearchMode>(), It.IsAny<int>(), null,
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("game has no content"));

        SetupHybridSearchForGame(goodGameId, count: 4, baseScore: 0.7f);

        var sut = CreateSut();

        // Act — should not throw
        var result = await sut.SearchAsync("test", new[] { failingGameId, goodGameId }, limit: 10);

        // Assert — results from good game still returned
        result.Should().HaveCount(4);
        result.Should().AllSatisfy(r => r.GameId.Should().Be(goodGameId));
    }

    // ---------------------------------------------------------------------------
    // minScore: results below threshold are excluded from aggregation
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MinScore_FiltersOutLowScoreResults()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        _hybridSearchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), gameId,
                It.IsAny<SearchMode>(), It.IsAny<int>(), null,
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>
            {
                MakeResult(gameId, chunkIndex: 0, score: 0.9f),
                MakeResult(gameId, chunkIndex: 1, score: 0.4f),  // below 0.5 threshold
                MakeResult(gameId, chunkIndex: 2, score: 0.6f),
            });

        var sut = CreateSut();

        // Act
        var result = await sut.SearchAsync("test", new[] { gameId }, limit: 10, minScore: 0.5);

        // Assert — low-score chunk excluded
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(r => r.HybridScore.Should().BeGreaterThanOrEqualTo(0.5f));
    }

    // ---------------------------------------------------------------------------
    // Issue #1686 — documentIds parameter is forwarded to each per-game IHybridSearchService call
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DocumentIds_PassedThrough_ToPerGameSearch()
    {
        // Arrange
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        var docA = Guid.NewGuid();
        var docB = Guid.NewGuid();
        var documentIds = new List<Guid> { docA, docB };

        // Setup with explicit documentIds match — both per-game calls receive the same allowlist
        _hybridSearchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<SearchMode>(), It.IsAny<int>(),
                It.Is<List<Guid>?>(d => d != null && d.SequenceEqual(documentIds)),
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>());

        var sut = CreateSut();

        // Act — call with documentIds allowlist
        await sut.SearchAsync(
            "test",
            new[] { gameId1, gameId2 },
            limit: 10,
            mode: SearchMode.Hybrid,
            minScore: 0.0,
            documentIds: documentIds,
            cancellationToken: CancellationToken.None);

        // Assert — each game received the same documentIds allowlist
        _hybridSearchMock.Verify(
            s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<SearchMode>(), It.IsAny<int>(),
                It.Is<List<Guid>?>(d => d != null && d.Count == 2 && d.Contains(docA) && d.Contains(docB)),
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task DocumentIds_Null_ForwardedAsNull_BackwardCompatible()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        SetupHybridSearchForGame(gameId, count: 1, baseScore: 0.8f);

        var sut = CreateSut();

        // Act — call WITHOUT documentIds (legacy path, default null)
        await sut.SearchAsync("test", new[] { gameId }, limit: 10);

        // Assert — per-game search receives documentIds=null (preserves legacy behaviour, D-3)
        _hybridSearchMock.Verify(
            s => s.SearchAsync(
                It.IsAny<string>(), gameId,
                It.IsAny<SearchMode>(), It.IsAny<int>(),
                null,  // explicit null — backward-compatible default
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Helper methods
    // ---------------------------------------------------------------------------

    private void SetupHybridSearchForGame(Guid gameId, int count, float baseScore)
    {
        _hybridSearchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), gameId,
                It.IsAny<SearchMode>(), It.IsAny<int>(), null,
                It.IsAny<float>(), It.IsAny<float>(), It.IsAny<double>(),
                It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildResults(gameId, count, baseScore));
    }

    private static List<HybridSearchResult> BuildResults(Guid gameId, int count, float baseScore) =>
        Enumerable.Range(0, count)
            .Select(i => MakeResult(gameId, chunkIndex: i, score: baseScore - i * 0.01f))
            .ToList();

    private static HybridSearchResult MakeResult(Guid gameId, int chunkIndex, float score) =>
        new()
        {
            ChunkId = $"{Guid.NewGuid()}_{chunkIndex}",
            Content = $"chunk content {chunkIndex}",
            PdfDocumentId = Guid.NewGuid().ToString(),
            GameId = gameId,
            ChunkIndex = chunkIndex,
            PageNumber = chunkIndex + 1,
            HybridScore = score,
            VectorScore = score,
            KeywordScore = null,
            VectorRank = chunkIndex + 1,
            KeywordRank = null,
            MatchedTerms = new List<string>(),
            Mode = SearchMode.Hybrid,
            RoleTags = GameBookRole.None
        };

    // #2568: lets a test pin HybridScore, VectorScore and KeywordScore independently
    // so the cross-game tiebreak can be exercised (HybridScore tie + differing cosine).
    private static HybridSearchResult MakeResultWithScores(
        Guid gameId, int chunkIndex, float hybridScore, float vectorScore, float? keywordScore = null) =>
        new()
        {
            ChunkId = $"{Guid.NewGuid()}_{chunkIndex}",
            Content = $"chunk content {chunkIndex}",
            PdfDocumentId = Guid.NewGuid().ToString(),
            GameId = gameId,
            ChunkIndex = chunkIndex,
            PageNumber = chunkIndex + 1,
            HybridScore = hybridScore,
            VectorScore = vectorScore,
            KeywordScore = keywordScore,
            VectorRank = chunkIndex + 1,
            KeywordRank = keywordScore.HasValue ? chunkIndex + 1 : (int?)null,
            MatchedTerms = new List<string>(),
            Mode = SearchMode.Hybrid,
            RoleTags = GameBookRole.None
        };
}
