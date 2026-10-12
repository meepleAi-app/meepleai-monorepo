using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase;

public sealed class SemanticResponseCacheTests
{
    [Fact]
    public void CachedRagResponse_ConstructsWithAllFields()
    {
        // #3855: la citazione porta (snippet, pagina, documento). La fixture precedente era
        // `["[Page 1] Context"]`, che suggeriva un marcatore di pagina dentro il testo: in
        // produzione li' finiva `c.Snippet` nudo, e la pagina veniva poi reinventata in lettura.
        var response = new CachedRagResponse(
            Answer: "Test answer",
            Citations: [new CachedCitation("Context from the rulebook.", 12, "doc-1")],
            ModelUsed: "deepseek-chat",
            CachedAt: DateTimeOffset.UtcNow);

        Assert.Equal("Test answer", response.Answer);
        Assert.Single(response.Citations);
        Assert.Equal(12, response.Citations[0].PageNumber);
        Assert.Equal("doc-1", response.Citations[0].DocumentId);
    }

    [Theory]
    [InlineData(0.96f, true)]   // over threshold — cache hit
    [InlineData(0.95f, true)]   // exactly at threshold — cache hit
    [InlineData(0.94f, false)]  // under threshold — cache miss
    public void CosineSimilarity_DeterminesHit(float similarity, bool expectedHit)
    {
        const float cacheThreshold = 0.95f;
        Assert.Equal(expectedHit, similarity >= cacheThreshold);
    }

    [Fact]
    public void CachedRagResponse_Citations_IsReadOnlyList()
    {
        // Verify Citations is exposed as IReadOnlyList<CachedCitation> (immutable contract)
        var citations = new List<CachedCitation>
        {
            new("Opening move rules.", 1, "doc-1"),
            new("Endgame scoring.", 3, "doc-1"),
        };
        var response = new CachedRagResponse("answer", citations, "model", DateTimeOffset.UtcNow);

        Assert.IsAssignableFrom<IReadOnlyList<CachedCitation>>(response.Citations);
        Assert.Equal(2, response.Citations.Count);
        Assert.Equal(1, response.Citations[0].PageNumber);
        Assert.Equal(3, response.Citations[1].PageNumber);
    }

    [Fact]
    public void CachedRagResponse_ModelUsed_RoundTrips()
    {
        const string model = "deepseek-chat-v3";
        var cachedAt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var response = new CachedRagResponse("answer", [], model, cachedAt);

        Assert.Equal(model, response.ModelUsed);
        Assert.Equal(cachedAt, response.CachedAt);
    }

    [Fact]
    public void CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        var v = new float[] { 1f, 0f, 0f };
        var similarity = SemanticResponseCache.CosineSimilarity(v, v);

        Assert.Equal(1.0f, similarity, precision: 5);
        Assert.True(similarity >= 0.95f); // identical vectors must produce a cache hit
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        var a = new float[] { 1f, 0f, 0f };
        var b = new float[] { 0f, 1f, 0f };
        var similarity = SemanticResponseCache.CosineSimilarity(a, b);

        Assert.Equal(0f, similarity, precision: 5);
        Assert.False(similarity >= 0.95f); // orthogonal vectors must not produce a cache hit
    }

    [Fact]
    public void CosineSimilarity_MismatchedLengths_ReturnsZero()
    {
        var a = new float[] { 1f, 0f };
        var b = new float[] { 1f, 0f, 0f };
        var similarity = SemanticResponseCache.CosineSimilarity(a, b);

        Assert.Equal(0f, similarity);
    }

    // ─── #4168: l'ambito nella chiave ────────────────────────────────────────────

    private static readonly Guid GameId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid VersionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public async Task SetAsync_WritesUnderThePrefixOfTheScope()
    {
        var (redis, db, _) = FakeRedis();
        var cache = new SemanticResponseCache(redis.Object, NullLogger<SemanticResponseCache>.Instance);

        await cache.SetAsync(GameId, new SemanticCacheScope(VersionId, "it"), [1f, 0f],
            new CachedRagResponse("Risposta.", [], "deepseek-flash", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        // Qualunque overload di StringSetAsync usi la cache: conta la chiave, primo argomento.
        var write = db.Invocations.Should().ContainSingle(i => i.Method.Name == nameof(IDatabase.StringSetAsync)).Which;
        ((RedisKey)write.Arguments[0]).ToString()
            .Should().StartWith($"rag:cache:{GameId}:v3:{VersionId:N}:it:");
    }

    [Fact]
    public async Task TryGetAsync_ScansOnlyTheKeysOfTheScope()
    {
        var (redis, _, server) = FakeRedis();
        var cache = new SemanticResponseCache(redis.Object, NullLogger<SemanticResponseCache>.Instance);

        await cache.TryGetAsync(GameId, new SemanticCacheScope(VersionId, "IT"), [1f, 0f], TestContext.Current.CancellationToken);

        server.Verify(s => s.Keys(
            It.IsAny<int>(),
            It.Is<RedisValue>(p => p.ToString() == $"rag:cache:{GameId}:v3:{VersionId:N}:it:*"),
            It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public void Scope_OfAnotherVersionOrLanguage_HasAnotherPrefix()
    {
        var prefix = new SemanticCacheScope(VersionId, "en").KeyPrefix(GameId);

        new SemanticCacheScope(Guid.NewGuid(), "en").KeyPrefix(GameId).Should().NotBe(prefix);
        new SemanticCacheScope(VersionId, "it").KeyPrefix(GameId).Should().NotBe(prefix);
        // L'invalidazione per gioco usa il prefisso nudo, che li copre tutti.
        prefix.Should().StartWith($"rag:cache:{GameId}:");
    }

    private static (Mock<IConnectionMultiplexer> Redis, Mock<IDatabase> Db, Mock<IServer> Server) FakeRedis()
    {
        var db = new Mock<IDatabase>();
        var server = new Mock<IServer>();
        server.Setup(s => s.Keys(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns([]);
        var endpoint = new System.Net.DnsEndPoint("localhost", 6379);
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);
        redis.Setup(r => r.GetEndPoints(It.IsAny<bool>())).Returns([endpoint]);
        redis.Setup(r => r.GetServer(endpoint, It.IsAny<object>())).Returns(server.Object);
        return (redis, db, server);
    }
}
