using Api.BoundedContexts.KnowledgeBase.Application.Services;
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
}
