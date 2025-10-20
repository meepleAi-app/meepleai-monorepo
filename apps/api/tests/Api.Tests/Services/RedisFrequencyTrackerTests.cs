using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace MeepleAI.Api.Tests.Services;

/// <summary>
/// BDD-style unit tests for RedisFrequencyTracker service.
/// Tests query access tracking, top N queries retrieval, and frequency classification.
/// </summary>
public class RedisFrequencyTrackerTests
{
    private readonly Mock<ILogger<RedisFrequencyTracker>> _mockLogger;
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<IOptions<CacheOptimizationConfiguration>> _mockConfig;

    public RedisFrequencyTrackerTests()
    {
        _mockLogger = new Mock<ILogger<RedisFrequencyTracker>>();
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockConfig = new Mock<IOptions<CacheOptimizationConfiguration>>();

        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);

        _mockConfig.Setup(c => c.Value).Returns(new CacheOptimizationConfiguration
        {
            FrequencyTrackerKeyPrefix = "meepleai:freq:",
            HotQueryThreshold = 10,
            WarmQueryThreshold = 3
        });
    }

    [Fact]
    public async Task TrackAccessAsync_ValidQuery_IncrementsRedisScore()
    {
        // Arrange (Given): Query "How to win?" accessed once
        var tracker = new RedisFrequencyTracker(
            _mockLogger.Object,
            _mockRedis.Object,
            _mockConfig.Object
        );
        var gameId = Guid.NewGuid();
        var query = "How to win?";
        var redisKey = $"meepleai:freq:{gameId}";

        _mockDatabase.Setup(db => db.SortedSetIncrementAsync(
            redisKey,
            query,
            1.0,
            CommandFlags.None
        )).ReturnsAsync(1.0);

        // Act (When): TrackAccessAsync() called
        await tracker.TrackAccessAsync(gameId, query);

        // Assert (Then): Redis ZINCRBY increments score by 1
        _mockDatabase.Verify(
            db => db.SortedSetIncrementAsync(
                redisKey,
                query,
                1.0,
                CommandFlags.None
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetTopQueriesAsync_MultipleQueries_ReturnsTop10Descending()
    {
        // Arrange (Given): 100 queries with varying access counts
        var tracker = new RedisFrequencyTracker(
            _mockLogger.Object,
            _mockRedis.Object,
            _mockConfig.Object
        );
        var gameId = Guid.NewGuid();
        var redisKey = $"meepleai:freq:{gameId}";

        var mockEntries = Enumerable.Range(1, 100)
            .Select(i => new SortedSetEntry($"Query {i}", 100 - i + 1)) // Scores 100 -> 1
            .ToArray();

        _mockDatabase.Setup(db => db.SortedSetRangeByRankWithScoresAsync(
            redisKey,
            0,
            9, // Top 10
            Order.Descending,
            CommandFlags.None
        )).ReturnsAsync(mockEntries.Take(10).ToArray());

        // Act (When): GetTopQueriesAsync(10) called
        var topQueries = await tracker.GetTopQueriesAsync(gameId, 10);

        // Assert (Then): Returns top 10 by score, ordered descending
        Assert.Equal(10, topQueries.Count());
        Assert.Equal("Query 1", topQueries.First().Query); // Highest score (100)
        Assert.Equal(100, topQueries.First().AccessCount);
        Assert.Equal("Query 10", topQueries.Last().Query); // 10th highest (91)
        Assert.Equal(91, topQueries.Last().AccessCount);
    }

    [Fact]
    public async Task GetFrequencyAsync_ExistingQuery_ReturnsHitCount()
    {
        // Arrange (Given): Query with 15 accesses
        var tracker = new RedisFrequencyTracker(
            _mockLogger.Object,
            _mockRedis.Object,
            _mockConfig.Object
        );
        var gameId = Guid.NewGuid();
        var query = "How to win?";
        var redisKey = $"meepleai:freq:{gameId}";

        _mockDatabase.Setup(db => db.SortedSetScoreAsync(
            redisKey,
            query,
            CommandFlags.None
        )).ReturnsAsync(15.0);

        // Act (When): Get frequency
        var frequency = await tracker.GetFrequencyAsync(gameId, query);

        // Assert (Then): Returns correct hit count
        Assert.Equal(15, frequency);
    }

    [Fact]
    public async Task ClassifyQueryAsync_HotQuery_ReturnsHotCategory()
    {
        // Arrange (Given): Query with 15 hits (hot threshold = 10)
        var tracker = new RedisFrequencyTracker(
            _mockLogger.Object,
            _mockRedis.Object,
            _mockConfig.Object
        );
        var gameId = Guid.NewGuid();
        var query = "How to win?";
        var redisKey = $"meepleai:freq:{gameId}";

        _mockDatabase.Setup(db => db.SortedSetScoreAsync(
            redisKey,
            query,
            CommandFlags.None
        )).ReturnsAsync(15.0);

        // Act (When): Classify query
        var category = await tracker.ClassifyQueryAsync(gameId, query);

        // Assert (Then): Returns "hot"
        Assert.Equal("hot", category);
    }

    [Theory]
    [InlineData(15, "hot")]   // >10 -> hot
    [InlineData(5, "warm")]   // 3-10 -> warm
    [InlineData(1, "cold")]   // <3 -> cold
    [InlineData(10, "hot")]   // Boundary: exactly hot threshold
    [InlineData(3, "warm")]   // Boundary: exactly warm threshold
    public async Task ClassifyQueryAsync_VariousFrequencies_ReturnsCorrectCategory(
        double frequency,
        string expectedCategory)
    {
        // Arrange (Given): Query with specific frequency
        var tracker = new RedisFrequencyTracker(
            _mockLogger.Object,
            _mockRedis.Object,
            _mockConfig.Object
        );
        var gameId = Guid.NewGuid();
        var query = "Test query";
        var redisKey = $"meepleai:freq:{gameId}";

        _mockDatabase.Setup(db => db.SortedSetScoreAsync(
            redisKey,
            query,
            CommandFlags.None
        )).ReturnsAsync(frequency);

        // Act (When): Classify query
        var category = await tracker.ClassifyQueryAsync(gameId, query);

        // Assert (Then): Returns correct category
        Assert.Equal(expectedCategory, category);
    }

    [Fact]
    public async Task TrackAccessAsync_ConcurrentAccess_UpdatesAtomically()
    {
        // Arrange (Given): 10 threads access same query simultaneously
        var tracker = new RedisFrequencyTracker(
            _mockLogger.Object,
            _mockRedis.Object,
            _mockConfig.Object
        );
        var gameId = Guid.NewGuid();
        var query = "How to win?";
        var redisKey = $"meepleai:freq:{gameId}";

        var currentScore = 0.0;
        _mockDatabase.Setup(db => db.SortedSetIncrementAsync(
            redisKey,
            query,
            1.0,
            CommandFlags.None
        )).ReturnsAsync(() => ++currentScore); // Atomic increment simulation

        // Act (When): All threads call TrackAccessAsync()
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => tracker.TrackAccessAsync(gameId, query))
            .ToArray();
        await Task.WhenAll(tasks);

        // Assert (Then): Final count = 10 (no race condition)
        _mockDatabase.Verify(
            db => db.SortedSetIncrementAsync(
                redisKey,
                query,
                1.0,
                CommandFlags.None
            ),
            Times.Exactly(10)
        );
        Assert.Equal(10.0, currentScore);
    }

    [Fact]
    public async Task GetTopQueriesAsync_EmptyTracker_ReturnsEmptyList()
    {
        // Arrange (Given): No queries tracked
        var tracker = new RedisFrequencyTracker(
            _mockLogger.Object,
            _mockRedis.Object,
            _mockConfig.Object
        );
        var gameId = Guid.NewGuid();
        var redisKey = $"meepleai:freq:{gameId}";

        _mockDatabase.Setup(db => db.SortedSetRangeByRankWithScoresAsync(
            redisKey,
            0,
            9,
            Order.Descending,
            CommandFlags.None
        )).ReturnsAsync(Array.Empty<SortedSetEntry>());

        // Act (When): GetTopQueriesAsync called
        var topQueries = await tracker.GetTopQueriesAsync(gameId, 10);

        // Assert (Then): Returns empty list
        Assert.Empty(topQueries);
    }

    [Fact]
    public async Task GetFrequencyAsync_NonExistentQuery_ReturnsZero()
    {
        // Arrange (Given): Query never accessed
        var tracker = new RedisFrequencyTracker(
            _mockLogger.Object,
            _mockRedis.Object,
            _mockConfig.Object
        );
        var gameId = Guid.NewGuid();
        var query = "Unknown query";
        var redisKey = $"meepleai:freq:{gameId}";

        _mockDatabase.Setup(db => db.SortedSetScoreAsync(
            redisKey,
            query,
            CommandFlags.None
        )).ReturnsAsync((double?)null); // Redis returns null for non-existent

        // Act (When): Get frequency
        var frequency = await tracker.GetFrequencyAsync(gameId, query);

        // Assert (Then): Returns 0
        Assert.Equal(0, frequency);
    }
}
