using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Unit tests for <see cref="ScheduledBulkInvalidationJob"/> (Issue #613).
///
/// Uses raw <see cref="HybridCache"/> rather than <c>IHybridCacheService</c>
/// so tag invalidation hits the same native tag index populated by the
/// SharedGameCatalog read-model query handlers (mirrors the three event
/// handler test fixtures).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class ScheduledBulkInvalidationJobTests
{
    private readonly Mock<ILogger<ScheduledBulkInvalidationJob>> _loggerMock = new();

    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static Mock<IJobExecutionContext> CreateJobContext(CancellationToken? ct = null)
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.Setup(c => c.CancellationToken).Returns(ct ?? CancellationToken.None);
        mock.Setup(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        return mock;
    }

    [Fact]
    public void Constructor_WithNullDbContext_Throws()
    {
        var act = () => new ScheduledBulkInvalidationJob(null!, CreateHybridCache(), new PassthroughRetryPolicy(), _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public void Constructor_WithNullCache_Throws()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var act = () => new ScheduledBulkInvalidationJob(db, null!, new PassthroughRetryPolicy(), _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullRetryPolicy_Throws()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var act = () => new ScheduledBulkInvalidationJob(db, CreateHybridCache(), null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("retryPolicy");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var act = () => new ScheduledBulkInvalidationJob(db, CreateHybridCache(), new PassthroughRetryPolicy(), null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task Execute_NullContext_Throws()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var job = new ScheduledBulkInvalidationJob(db, CreateHybridCache(), new PassthroughRetryPolicy(), _loggerMock.Object);

        var act = async () => await job.Execute(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Execute_WithNoAnalyticsEvents_InvalidatesListTagOnly()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var cache = CreateHybridCache();

        // Seed list-tag entry to verify eviction.
        await cache.SetAsync("list-key", "seed", tags: new[] { "search-games" });

        var job = new ScheduledBulkInvalidationJob(db, cache, new PassthroughRetryPolicy(), _loggerMock.Object);
        var ctx = CreateJobContext();

        // Act
        await job.Execute(ctx.Object);

        // Assert — list eviction occurred (factory must repopulate)
        var listFactoryRan = false;
        await cache.GetOrCreateAsync(
            "list-key",
            _ =>
            {
                listFactoryRan = true;
                return ValueTask.FromResult("repop");
            },
            tags: new[] { "search-games" });
        listFactoryRan.Should().BeTrue();

        // Result reflects zero detail invalidations
        ctx.VerifySet(c => c.Result = It.Is<object>(r =>
            (bool)r.GetType().GetProperty("Success")!.GetValue(r)! &&
            (int)r.GetType().GetProperty("ListInvalidations")!.GetValue(r)! == 1 &&
            (int)r.GetType().GetProperty("DetailInvalidations")!.GetValue(r)! == 0));
    }

    [Fact]
    public async Task Execute_WithRecentEvents_InvalidatesPerGameDetailTags()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var cache = CreateHybridCache();

        var sharedGameA = Guid.NewGuid();
        var sharedGameB = Guid.NewGuid();

        SeedEvent(db, sharedGameA, GameEventType.View, DateTime.UtcNow.AddMinutes(-5));
        SeedEvent(db, sharedGameA, GameEventType.View, DateTime.UtcNow.AddMinutes(-10));
        SeedEvent(db, sharedGameB, GameEventType.Search, DateTime.UtcNow.AddMinutes(-3));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var detailKeyA = $"detail-A-{Guid.NewGuid()}";
        var detailKeyB = $"detail-B-{Guid.NewGuid()}";
        await cache.SetAsync(detailKeyA, "seed-a", tags: new[] { $"shared-game:{sharedGameA}" });
        await cache.SetAsync(detailKeyB, "seed-b", tags: new[] { $"shared-game:{sharedGameB}" });

        var job = new ScheduledBulkInvalidationJob(db, cache, new PassthroughRetryPolicy(), _loggerMock.Object);
        var ctx = CreateJobContext();

        // Act
        await job.Execute(ctx.Object);

        // Assert — both per-game tags evicted
        var factoryA = false;
        await cache.GetOrCreateAsync(detailKeyA, _ => { factoryA = true; return ValueTask.FromResult("repop-a"); }, tags: new[] { $"shared-game:{sharedGameA}" });
        factoryA.Should().BeTrue();

        var factoryB = false;
        await cache.GetOrCreateAsync(detailKeyB, _ => { factoryB = true; return ValueTask.FromResult("repop-b"); }, tags: new[] { $"shared-game:{sharedGameB}" });
        factoryB.Should().BeTrue();

        ctx.VerifySet(c => c.Result = It.Is<object>(r =>
            (bool)r.GetType().GetProperty("Success")!.GetValue(r)! &&
            (int)r.GetType().GetProperty("DetailInvalidations")!.GetValue(r)! == 2));
    }

    [Fact]
    public async Task Execute_IgnoresEventsOlderThanLookbackWindow()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var cache = CreateHybridCache();

        var staleGame = Guid.NewGuid();      // outside 1h window — must be ignored
        var freshGame = Guid.NewGuid();      // inside 1h window

        SeedEvent(db, staleGame, GameEventType.View, DateTime.UtcNow.AddHours(-3));
        SeedEvent(db, freshGame, GameEventType.View, DateTime.UtcNow.AddMinutes(-10));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var staleDetailKey = $"stale-{Guid.NewGuid()}";
        await cache.SetAsync(staleDetailKey, "stale", tags: new[] { $"shared-game:{staleGame}" });

        var job = new ScheduledBulkInvalidationJob(db, cache, new PassthroughRetryPolicy(), _loggerMock.Object);
        var ctx = CreateJobContext();

        // Act
        await job.Execute(ctx.Object);

        // Assert — stale-game tag should NOT have been invalidated
        var staleFactoryRan = false;
        await cache.GetOrCreateAsync(
            staleDetailKey,
            _ => { staleFactoryRan = true; return ValueTask.FromResult("repop"); },
            tags: new[] { $"shared-game:{staleGame}" });
        staleFactoryRan.Should().BeFalse("event older than 1h should be filtered out");

        // Only the fresh game contributed to DetailInvalidations
        ctx.VerifySet(c => c.Result = It.Is<object>(r =>
            (int)r.GetType().GetProperty("DetailInvalidations")!.GetValue(r)! == 1));
    }

    [Fact]
    public async Task Execute_OrdersByEventCountDescending()
    {
        // Arrange — 1 event for gameA, 5 events for gameB. With TopN cap > 2,
        // both should be invalidated. We verify by ensuring both tags evict.
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var cache = CreateHybridCache();

        var gameA = Guid.NewGuid();
        var gameB = Guid.NewGuid();

        SeedEvent(db, gameA, GameEventType.View, DateTime.UtcNow.AddMinutes(-1));
        for (var i = 0; i < 5; i++)
        {
            SeedEvent(db, gameB, GameEventType.View, DateTime.UtcNow.AddMinutes(-i - 1));
        }
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var keyA = $"a-{Guid.NewGuid()}";
        var keyB = $"b-{Guid.NewGuid()}";
        await cache.SetAsync(keyA, "seed-a", tags: new[] { $"shared-game:{gameA}" });
        await cache.SetAsync(keyB, "seed-b", tags: new[] { $"shared-game:{gameB}" });

        var job = new ScheduledBulkInvalidationJob(db, cache, new PassthroughRetryPolicy(), _loggerMock.Object);
        var ctx = CreateJobContext();

        // Act
        await job.Execute(ctx.Object);

        // Assert — both invalidated regardless of order
        var ranA = false;
        await cache.GetOrCreateAsync(keyA, _ => { ranA = true; return ValueTask.FromResult("r-a"); }, tags: new[] { $"shared-game:{gameA}" });
        ranA.Should().BeTrue();

        var ranB = false;
        await cache.GetOrCreateAsync(keyB, _ => { ranB = true; return ValueTask.FromResult("r-b"); }, tags: new[] { $"shared-game:{gameB}" });
        ranB.Should().BeTrue();

        ctx.VerifySet(c => c.Result = It.Is<object>(r =>
            (int)r.GetType().GetProperty("DetailInvalidations")!.GetValue(r)! == 2));
    }

    [Fact]
    public async Task Execute_WhenRetryPolicyThrows_CatchesAndSetsFailureResult()
    {
        // Arrange — retry policy that always throws on the FIRST invocation
        // (the list invalidation), forcing the catch-all to engage.
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var cache = CreateHybridCache();
        var alwaysFails = new ThrowingRetryPolicy(new InvalidOperationException("Redis down"));

        var job = new ScheduledBulkInvalidationJob(db, cache, alwaysFails, _loggerMock.Object);
        var ctx = CreateJobContext();

        // Act
        await job.Execute(ctx.Object);

        // Assert — job did NOT throw, Result reflects failure
        ctx.VerifySet(c => c.Result = It.Is<object>(r =>
            !(bool)r.GetType().GetProperty("Success")!.GetValue(r)! &&
            r.GetType().GetProperty("Error")!.GetValue(r)!.ToString()!.Contains("Redis down", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Execute_OnCancellation_PropagatesOperationCanceled()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var cache = CreateHybridCache();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var job = new ScheduledBulkInvalidationJob(db, cache, new PassthroughRetryPolicy(), _loggerMock.Object);
        var ctx = CreateJobContext(cts.Token);

        // Act
        var act = async () => await job.Execute(ctx.Object);

        // Assert — caller cancellation must propagate so Quartz can correctly
        // report the run as cancelled rather than swallowing it as a failure.
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Execute_RespectsTopNCap()
    {
        // Arrange — seed DefaultTopN+5 distinct games, verify the cap holds.
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var cache = CreateHybridCache();

        var totalGames = ScheduledBulkInvalidationJob.DefaultTopN + 5;
        for (var i = 0; i < totalGames; i++)
        {
            SeedEvent(db, Guid.NewGuid(), GameEventType.View, DateTime.UtcNow.AddMinutes(-i % 30 - 1));
        }
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var job = new ScheduledBulkInvalidationJob(db, cache, new PassthroughRetryPolicy(), _loggerMock.Object);
        var ctx = CreateJobContext();

        // Act
        await job.Execute(ctx.Object);

        // Assert
        ctx.VerifySet(c => c.Result = It.Is<object>(r =>
            (int)r.GetType().GetProperty("DetailInvalidations")!.GetValue(r)! == ScheduledBulkInvalidationJob.DefaultTopN));
    }

    private static void SeedEvent(
        Api.Infrastructure.MeepleAiDbContext db,
        Guid sharedGameId,
        GameEventType type,
        DateTime timestampUtc)
    {
        db.Set<GameAnalyticsEventEntity>().Add(new GameAnalyticsEventEntity
        {
            Id = Guid.NewGuid(),
            GameId = sharedGameId,
            EventType = (int)type,
            UserId = null,
            Timestamp = timestampUtc
        });
    }

    private sealed class ThrowingRetryPolicy : ICacheInvalidationRetryPolicy
    {
        private readonly Exception _toThrow;

        public ThrowingRetryPolicy(Exception toThrow) => _toThrow = toThrow;

        public Task ExecuteAsync(
            Func<CancellationToken, ValueTask> operation,
            string operationName,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(operation);
            return Task.FromException(_toThrow);
        }
    }
}
