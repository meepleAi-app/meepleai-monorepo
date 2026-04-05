using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Caching;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase;

public sealed class SemanticResponseCacheTests
{
    [Fact]
    public void CachedRagResponse_ConstructsWithAllFields()
    {
        var response = new CachedRagResponse(
            Answer: "Test answer",
            Citations: ["[Page 1] Context"],
            ModelUsed: "deepseek-chat",
            CachedAt: DateTimeOffset.UtcNow);

        Assert.Equal("Test answer", response.Answer);
        Assert.Single(response.Citations);
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
        // Verify Citations is exposed as IReadOnlyList<string> (immutable contract on the record)
        var citations = new List<string> { "[Page 1]", "[Page 3]" };
        var response = new CachedRagResponse("answer", citations, "model", DateTimeOffset.UtcNow);

        Assert.IsAssignableFrom<IReadOnlyList<string>>(response.Citations);
        Assert.Equal(2, response.Citations.Count);
        Assert.Equal("[Page 1]", response.Citations[0]);
        Assert.Equal("[Page 3]", response.Citations[1]);
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
}
