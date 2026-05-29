using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
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

    private MultiGameHybridSearchService CreateSut() =>
        new(_hybridSearchMock.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<MultiGameHybridSearchService>.Instance);

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
}
