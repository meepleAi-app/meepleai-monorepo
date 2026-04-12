using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.DevTools.MockImpls;
using Xunit;

namespace Api.Tests.DevTools;

public class MockRerankerServiceTests
{
    private static readonly MockRerankerService Sut = new();

    // ── IsHealthyAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task IsHealthyAsync_AlwaysReturnsTrue()
    {
        var result = await Sut.IsHealthyAsync(CancellationToken.None);
        Assert.True(result);
    }

    // ── RerankAsync — basic contract ──────────────────────────────────────────

    [Fact]
    public async Task RerankAsync_EmptyChunks_ReturnsEmptyResult()
    {
        var result = await Sut.RerankAsync("any query", new List<RerankChunk>(), null, CancellationToken.None);
        Assert.Empty(result.Chunks);
    }

    [Fact]
    public async Task RerankAsync_ModelName_IsMockName()
    {
        var chunks = new List<RerankChunk>
        {
            new("id1", "hello world", 0.5)
        };
        var result = await Sut.RerankAsync("hello", chunks, null, CancellationToken.None);
        Assert.Equal("mock-bge-reranker-jaccard", result.Model);
    }

    [Fact]
    public async Task RerankAsync_PreservesAllChunksWhenTopKIsNull()
    {
        var chunks = new List<RerankChunk>
        {
            new("a", "alpha beta", 0.9),
            new("b", "gamma delta", 0.8),
            new("c", "epsilon zeta", 0.7),
        };
        var result = await Sut.RerankAsync("alpha", chunks, null, CancellationToken.None);
        Assert.Equal(3, result.Chunks.Count);
    }

    [Fact]
    public async Task RerankAsync_TopK_LimitsReturnedChunks()
    {
        var chunks = new List<RerankChunk>
        {
            new("a", "word1 word2", 0.9),
            new("b", "word3 word4", 0.8),
            new("c", "word5 word6", 0.7),
        };
        var result = await Sut.RerankAsync("word1", chunks, topK: 2, CancellationToken.None);
        Assert.Equal(2, result.Chunks.Count);
    }

    // ── Jaccard scoring ───────────────────────────────────────────────────────

    [Fact]
    public async Task RerankAsync_ExactMatchChunk_ScoresHigherThanUnrelated()
    {
        var query = "settlers of catan board game";
        var chunks = new List<RerankChunk>
        {
            new("exact", "settlers of catan board game", 0.5),
            new("unrelated", "sushi roll dice tower", 0.9),
        };
        var result = await Sut.RerankAsync(query, chunks, null, CancellationToken.None);

        var exactChunk = result.Chunks.First(c => c.Id == "exact");
        var unrelatedChunk = result.Chunks.First(c => c.Id == "unrelated");
        Assert.True(exactChunk.RerankScore > unrelatedChunk.RerankScore,
            $"Expected exact match score ({exactChunk.RerankScore:F4}) > unrelated score ({unrelatedChunk.RerankScore:F4})");
    }

    [Fact]
    public async Task RerankAsync_ExactMatchChunk_ScoresOne()
    {
        var text = "catan board game";
        var chunks = new List<RerankChunk>
        {
            new("id1", text, 0.5)
        };
        var result = await Sut.RerankAsync(text, chunks, null, CancellationToken.None);
        Assert.Equal(1.0, result.Chunks[0].RerankScore, precision: 10);
    }

    [Fact]
    public async Task RerankAsync_CompletelyDisjointChunk_ScoresZero()
    {
        var chunks = new List<RerankChunk>
        {
            new("id1", "alpha beta gamma", 0.5)
        };
        var result = await Sut.RerankAsync("delta epsilon zeta", chunks, null, CancellationToken.None);
        Assert.Equal(0.0, result.Chunks[0].RerankScore, precision: 10);
    }

    [Fact]
    public async Task RerankAsync_PartialOverlap_ScoresBetweenZeroAndOne()
    {
        var chunks = new List<RerankChunk>
        {
            new("id1", "alpha beta gamma delta", 0.5)
        };
        var result = await Sut.RerankAsync("alpha beta epsilon zeta", chunks, null, CancellationToken.None);
        Assert.InRange(result.Chunks[0].RerankScore, 0.01, 0.99);
    }

    // ── Ordering ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RerankAsync_ChunksReturnedInScoreDescendingOrder()
    {
        var query = "board game strategy";
        var chunks = new List<RerankChunk>
        {
            new("low",  "dice tower review",            0.3),
            new("high", "board game strategy guide",    0.5),
            new("mid",  "board game mechanics",          0.4),
        };
        var result = await Sut.RerankAsync(query, chunks, null, CancellationToken.None);

        var scores = result.Chunks.Select(c => c.RerankScore).ToList();
        for (var i = 0; i < scores.Count - 1; i++)
        {
            Assert.True(scores[i] >= scores[i + 1],
                $"Score at index {i} ({scores[i]:F4}) should be >= score at index {i + 1} ({scores[i + 1]:F4})");
        }
    }

    [Fact]
    public async Task RerankAsync_TiebreaksByOriginalScoreDescending()
    {
        // Both chunks have identical content (same Jaccard score). The one with
        // the higher OriginalScore should appear first.
        var chunks = new List<RerankChunk>
        {
            new("low-orig",  "alpha beta gamma", OriginalScore: 0.3),
            new("high-orig", "alpha beta gamma", OriginalScore: 0.9),
        };
        var result = await Sut.RerankAsync("alpha", chunks, null, CancellationToken.None);
        Assert.Equal("high-orig", result.Chunks[0].Id);
        Assert.Equal("low-orig", result.Chunks[1].Id);
    }

    // ── Result field preservation ─────────────────────────────────────────────

    [Fact]
    public async Task RerankAsync_PreservesOriginalScore()
    {
        var chunks = new List<RerankChunk>
        {
            new("id1", "hello world", OriginalScore: 0.777)
        };
        var result = await Sut.RerankAsync("hello", chunks, null, CancellationToken.None);
        Assert.Equal(0.777, result.Chunks[0].OriginalScore);
    }

    [Fact]
    public async Task RerankAsync_PreservesChunkId()
    {
        var chunks = new List<RerankChunk>
        {
            new("my-unique-id", "some text", 0.5)
        };
        var result = await Sut.RerankAsync("text", chunks, null, CancellationToken.None);
        Assert.Equal("my-unique-id", result.Chunks[0].Id);
    }

    [Fact]
    public async Task RerankAsync_PreservesContent()
    {
        const string content = "the quick brown fox";
        var chunks = new List<RerankChunk>
        {
            new("id1", content, 0.5)
        };
        var result = await Sut.RerankAsync("fox", chunks, null, CancellationToken.None);
        Assert.Equal(content, result.Chunks[0].Content);
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RerankAsync_SameInputProducesSameOutput()
    {
        var query = "board game rules";
        var chunks = new List<RerankChunk>
        {
            new("a", "board game rules overview", 0.8),
            new("b", "chess strategy guide", 0.6),
        };

        var r1 = await Sut.RerankAsync(query, chunks, null, CancellationToken.None);
        var r2 = await Sut.RerankAsync(query, chunks, null, CancellationToken.None);

        Assert.Equal(r1.Chunks.Count, r2.Chunks.Count);
        for (var i = 0; i < r1.Chunks.Count; i++)
        {
            Assert.Equal(r1.Chunks[i].Id, r2.Chunks[i].Id);
            Assert.Equal(r1.Chunks[i].RerankScore, r2.Chunks[i].RerankScore);
        }
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RerankAsync_EmptyQuery_ScoresAllZero()
    {
        var chunks = new List<RerankChunk>
        {
            new("a", "board game", 0.5),
            new("b", "strategy guide", 0.4),
        };
        var result = await Sut.RerankAsync(string.Empty, chunks, null, CancellationToken.None);
        Assert.All(result.Chunks, c => Assert.Equal(0.0, c.RerankScore));
    }

    [Fact]
    public async Task RerankAsync_CaseInsensitiveTokenization()
    {
        var chunks = new List<RerankChunk>
        {
            new("id1", "BOARD GAME RULES", 0.5)
        };
        var result = await Sut.RerankAsync("board game rules", chunks, null, CancellationToken.None);
        Assert.Equal(1.0, result.Chunks[0].RerankScore, precision: 10);
    }

    [Fact]
    public async Task RerankAsync_TopKZero_ReturnsEmptyResult()
    {
        var chunks = new List<RerankChunk>
        {
            new("id1", "some text", 0.5)
        };
        var result = await Sut.RerankAsync("text", chunks, topK: 0, CancellationToken.None);
        Assert.Empty(result.Chunks);
    }

    [Fact]
    public async Task RerankAsync_TopKExceedsChunkCount_ReturnsAllChunks()
    {
        var chunks = new List<RerankChunk>
        {
            new("a", "alpha", 0.5),
            new("b", "beta", 0.4),
        };
        var result = await Sut.RerankAsync("alpha", chunks, topK: 100, CancellationToken.None);
        Assert.Equal(2, result.Chunks.Count);
    }
}
