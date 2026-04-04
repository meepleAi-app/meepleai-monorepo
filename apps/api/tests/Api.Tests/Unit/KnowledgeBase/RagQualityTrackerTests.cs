using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public async Task TrackQueryAsync_SavesEntityToDatabase()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var tracker = new RagQualityTracker(db, NullLogger<RagQualityTracker>.Instance);
        var metrics = new RagQueryMetrics(
            ThreadId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            QueryLength: 55,
            ChunksRetrieved: 8,
            ChunksUsed: 4,
            CitationsCount: 2,
            Strategy: "Balanced",
            ModelUsed: "deepseek-chat",
            LatencyMs: 800,
            CacheHit: false,
            NoRelevantContext: false);

        // Act
        await tracker.TrackQueryAsync(metrics);

        // Assert
        Assert.Equal(1, db.RagQualityLogs.Count());
        Assert.Equal(metrics.LatencyMs, db.RagQualityLogs.Single().LatencyMs);
    }

    [Fact]
    public async Task TrackQueryAsync_WhenDbThrows_DoesNotThrow()
    {
        // Arrange — dispose the context before calling to trigger ObjectDisposedException
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var tracker = new RagQualityTracker(db, NullLogger<RagQualityTracker>.Instance);
        db.Dispose();

        var metrics = new RagQueryMetrics(
            ThreadId: null, GameId: null,
            QueryLength: 10, ChunksRetrieved: 0, ChunksUsed: 0,
            CitationsCount: 0, Strategy: "Fast", ModelUsed: "none",
            LatencyMs: 1, CacheHit: false, NoRelevantContext: true);

        // Act — must complete without throwing (non-fatal metric tracking)
        var exception = await Record.ExceptionAsync(() => tracker.TrackQueryAsync(metrics));

        // Assert
        Assert.Null(exception);
    }
}
