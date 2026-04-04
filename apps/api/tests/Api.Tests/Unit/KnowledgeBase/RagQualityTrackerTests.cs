using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase;

public sealed class RagQualityTrackerTests
{
    [Fact]
    public void RagQueryMetrics_ConstructsCorrectly()
    {
        var metrics = new RagQueryMetrics(
            ThreadId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            QueryLength: 42,
            ChunksRetrieved: 10,
            ChunksUsed: 5,
            CitationsCount: 3,
            Strategy: "Balanced",
            ModelUsed: "deepseek-chat",
            LatencyMs: 1234,
            CacheHit: false,
            NoRelevantContext: false);

        Assert.Equal(42, metrics.QueryLength);
        Assert.Equal("Balanced", metrics.Strategy);
        Assert.False(metrics.CacheHit);
    }

    [Fact]
    public void RagQueryMetrics_NoRelevantContext_IsTrackable()
    {
        var metrics = new RagQueryMetrics(
            ThreadId: null, GameId: null,
            QueryLength: 20, ChunksRetrieved: 0, ChunksUsed: 0,
            CitationsCount: 0, Strategy: "Fast", ModelUsed: "none",
            LatencyMs: 5, CacheHit: false, NoRelevantContext: true);

        Assert.True(metrics.NoRelevantContext);
        Assert.Equal(0, metrics.ChunksRetrieved);
    }
}
