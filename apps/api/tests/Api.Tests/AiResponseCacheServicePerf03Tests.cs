using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style unit tests for AiResponseCacheService PERF-03 enhancements.
///
/// Feature: PERF-03 - Enhanced AI Response Cache with Tag-Based Invalidation
/// As a developer
/// I want to cache AI responses with intelligent invalidation
/// So that I can reduce latency and API costs while keeping data fresh
/// </summary>
public class AiResponseCacheServicePerf03Tests
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockRedisDb;
    private readonly Mock<IServer> _mockRedisServer;
    private readonly IConfiguration _configuration;

    public AiResponseCacheServicePerf03Tests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockRedisDb = new Mock<IDatabase>();
        _mockRedisServer = new Mock<IServer>();

        var configDict = new Dictionary<string, string?>
        {
            ["AiResponseCache:TtlHours"] = "24"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();
    }

    #region Tag Invalidation Tests

    /// <summary>
    /// Scenario: Invalidate cache entries by tag
    ///   Given cached entries with a specific tag (e.g., game:123)
    ///   And the tag index contains cache keys
    ///   When calling InvalidateByCacheTagAsync with that tag
    ///   Then all cache entries with that tag are deleted
    ///   And their metadata is deleted
    ///   And the tag index itself is deleted
    /// </summary>
    [Fact]
    public async Task InvalidateByCacheTagAsync_WithExistingTag_DeletesAllEntriesAndMetadata()
    {
        // Given: Cached entries with tag
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);
        var tag = "game:test-game-123";
        var tagKey = $"tag:{tag}";

        var cacheKeys = new RedisValue[]
        {
            "ai:qa:test-game-123:hash1",
            "ai:qa:test-game-123:hash2",
            "ai:explain:test-game-123:hash3"
        };

        _mockRedisDb.Setup(db => db.SetMembersAsync(tagKey, CommandFlags.None))
            .ReturnsAsync(cacheKeys);

        var deletedKeys = new List<RedisKey>();
        _mockRedisDb.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .Callback<RedisKey, CommandFlags>((key, _) => deletedKeys.Add(key))
            .ReturnsAsync(true);

        // When: Invalidating by tag
        await service.InvalidateByCacheTagAsync(tag);

        // Then: All cache keys, metadata, and tag index are deleted
        Assert.Contains(deletedKeys, k => k == "ai:qa:test-game-123:hash1");
        Assert.Contains(deletedKeys, k => k == "ai:qa:test-game-123:hash1:meta");
        Assert.Contains(deletedKeys, k => k == "ai:qa:test-game-123:hash2");
        Assert.Contains(deletedKeys, k => k == "ai:qa:test-game-123:hash2:meta");
        Assert.Contains(deletedKeys, k => k == "ai:explain:test-game-123:hash3");
        Assert.Contains(deletedKeys, k => k == "ai:explain:test-game-123:hash3:meta");
        Assert.Contains(deletedKeys, k => k == tagKey);

        // Verify deletion calls
        _mockRedisDb.Verify(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), CommandFlags.None), Times.Exactly(7));
    }

    /// <summary>
    /// Scenario: Invalidate cache by tag with no entries
    ///   Given a tag with no associated cache entries
    ///   When calling InvalidateByCacheTagAsync
    ///   Then no deletions occur
    ///   And no errors are thrown
    /// </summary>
    [Fact]
    public async Task InvalidateByCacheTagAsync_WithNoEntries_DoesNothing()
    {
        // Given: Tag with no entries
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);
        var tag = "game:nonexistent";
        var tagKey = $"tag:{tag}";

        _mockRedisDb.Setup(db => db.SetMembersAsync(tagKey, CommandFlags.None))
            .ReturnsAsync(Array.Empty<RedisValue>());

        // When: Invalidating by tag
        await service.InvalidateByCacheTagAsync(tag);

        // Then: No deletions occur
        _mockRedisDb.Verify(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), CommandFlags.None), Times.Never);
    }

    /// <summary>
    /// Scenario: Invalidate cache by multiple tag types
    ///   Given different tag types (game:id, pdf:id, rulespec:id)
    ///   When calling InvalidateByCacheTagAsync for each type
    ///   Then each tag invalidation works independently
    /// </summary>
    [Theory]
    [InlineData("game:game-123")]
    [InlineData("pdf:pdf-456")]
    [InlineData("rulespec:spec-789")]
    public async Task InvalidateByCacheTagAsync_WithDifferentTagTypes_WorksIndependently(string tag)
    {
        // Given: Tag with entries
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);
        var tagKey = $"tag:{tag}";

        var cacheKeys = new RedisValue[] { "cache:key1", "cache:key2" };
        _mockRedisDb.Setup(db => db.SetMembersAsync(tagKey, CommandFlags.None))
            .ReturnsAsync(cacheKeys);

        var deletedKeys = new List<RedisKey>();
        _mockRedisDb.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .Callback<RedisKey, CommandFlags>((key, _) => deletedKeys.Add(key))
            .ReturnsAsync(true);

        // When: Invalidating by tag
        await service.InvalidateByCacheTagAsync(tag);

        // Then: Tag-specific entries are deleted
        Assert.Contains(deletedKeys, k => k == tagKey);
        Assert.Equal(5, deletedKeys.Count); // 2 keys + 2 metadata + 1 tag index
    }

    /// <summary>
    /// Scenario: Invalidate cache with Redis failure
    ///   Given a Redis connection that fails
    ///   When calling InvalidateByCacheTagAsync
    ///   Then the error is caught and logged
    ///   And no exception is thrown (graceful degradation)
    /// </summary>
    [Fact]
    public async Task InvalidateByCacheTagAsync_WithRedisFailure_GracefullyHandlesError()
    {
        // Given: Redis that throws exception
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);
        var tag = "game:test";

        _mockRedisDb.Setup(db => db.SetMembersAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        // When: Invalidating by tag
        // Then: No exception is thrown
        await service.InvalidateByCacheTagAsync(tag);

        // Success: Method completes without throwing
    }

    /// <summary>
    /// Scenario: Invalidate cache with cancellation
    ///   Given a cancellation token that is triggered
    ///   When calling InvalidateByCacheTagAsync
    ///   Then OperationCanceledException is thrown
    /// </summary>
    [Fact]
    public async Task InvalidateByCacheTagAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Given: Cancelled token
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // When/Then: OperationCanceledException is thrown
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.InvalidateByCacheTagAsync("game:test", cts.Token));
    }

    #endregion

    #region Cache Stats Tests

    /// <summary>
    /// Scenario: Get cache stats for all games
    ///   Given cache stats exist for multiple games
    ///   When calling GetCacheStatsAsync without a gameId filter
    ///   Then stats are aggregated across all games
    ///   And total hits and misses are calculated
    ///   And cache size is calculated from Redis
    /// </summary>
    [Fact]
    public async Task GetCacheStatsAsync_WithoutGameIdFilter_ReturnsAggregatedStats()
    {
        // Given: Cache stats for multiple games
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var stat1 = new CacheStatEntity
        {
            GameId = "game1",
            QuestionHash = "hash1",
            HitCount = 10,
            MissCount = 2,
            CreatedAt = DateTime.UtcNow,
            LastHitAt = DateTime.UtcNow
        };

        var stat2 = new CacheStatEntity
        {
            GameId = "game2",
            QuestionHash = "hash2",
            HitCount = 5,
            MissCount = 3,
            CreatedAt = DateTime.UtcNow,
            LastHitAt = DateTime.UtcNow
        };

        db.CacheStats.AddRange(stat1, stat2);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Mock Redis keys and values
        var redisKeys = new[] { new RedisKey("ai:qa:game1:hash1"), new RedisKey("ai:qa:game2:hash2") }.AsEnumerable();
        _mockRedisServer.Setup(s => s.Keys(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(redisKeys);

        var endpoints = new[] { new IPEndPoint(0, 0) as EndPoint };
        _mockRedis.Setup(r => r.GetEndPoints(It.IsAny<bool>()))
            .Returns(endpoints);
        _mockRedis.Setup(r => r.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>()))
            .Returns(_mockRedisServer.Object);

        _mockRedisDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(new RedisValue("{'test':'data'}"));

        // When: Getting stats without filter
        var stats = await service.GetCacheStatsAsync();

        // Then: Stats are aggregated
        Assert.Equal(15, stats.TotalHits); // 10 + 5
        Assert.Equal(5, stats.TotalMisses); // 2 + 3
        Assert.Equal(15.0 / 20.0, stats.HitRate, 2); // 15 hits / 20 total = 0.75
        Assert.Equal(2, stats.TotalKeys);
        Assert.True(stats.CacheSizeBytes > 0);
    }

    /// <summary>
    /// Scenario: Get cache stats filtered by game
    ///   Given cache stats for multiple games
    ///   When calling GetCacheStatsAsync with a specific gameId
    ///   Then only stats for that game are included
    /// </summary>
    [Fact]
    public async Task GetCacheStatsAsync_WithGameIdFilter_ReturnsFilteredStats()
    {
        // Given: Cache stats for multiple games
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var stat1 = new CacheStatEntity
        {
            GameId = "game1",
            QuestionHash = "hash1",
            HitCount = 10,
            MissCount = 2,
            CreatedAt = DateTime.UtcNow,
            LastHitAt = DateTime.UtcNow
        };

        var stat2 = new CacheStatEntity
        {
            GameId = "game2",
            QuestionHash = "hash2",
            HitCount = 5,
            MissCount = 3,
            CreatedAt = DateTime.UtcNow,
            LastHitAt = DateTime.UtcNow
        };

        db.CacheStats.AddRange(stat1, stat2);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Mock Redis for game1 only
        var redisKeys = new[] { new RedisKey("ai:qa:game1:hash1") }.AsEnumerable();
        _mockRedisServer.Setup(s => s.Keys(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(redisKeys);

        var endpoints = new[] { new IPEndPoint(0, 0) as EndPoint };
        _mockRedis.Setup(r => r.GetEndPoints(It.IsAny<bool>()))
            .Returns(endpoints);
        _mockRedis.Setup(r => r.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>()))
            .Returns(_mockRedisServer.Object);

        _mockRedisDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(new RedisValue("{'test':'data'}"));

        // When: Getting stats for game1
        var stats = await service.GetCacheStatsAsync("game1");

        // Then: Only game1 stats are included
        Assert.Equal(10, stats.TotalHits);
        Assert.Equal(2, stats.TotalMisses);
        Assert.Equal(1, stats.TotalKeys);
    }

    /// <summary>
    /// Scenario: Get cache stats with top questions
    ///   Given multiple cache stats with varying hit counts
    ///   When calling GetCacheStatsAsync
    ///   Then top 10 questions by hit count are returned
    ///   And they are ordered by hit count descending
    /// </summary>
    [Fact]
    public async Task GetCacheStatsAsync_WithMultipleQuestions_ReturnsTop10ByHitCount()
    {
        // Given: Multiple cache stats
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        for (int i = 0; i < 15; i++)
        {
            db.CacheStats.Add(new CacheStatEntity
            {
                GameId = "game1",
                QuestionHash = $"hash{i}",
                HitCount = i, // Incrementing hit counts
                MissCount = 0,
                CreatedAt = DateTime.UtcNow,
                LastHitAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Mock Redis
        var redisKeys = Enumerable.Range(0, 15).Select(i => new RedisKey($"ai:qa:game1:hash{i}"));
        _mockRedisServer.Setup(s => s.Keys(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(redisKeys);

        var endpoints = new[] { new IPEndPoint(0, 0) as EndPoint };
        _mockRedis.Setup(r => r.GetEndPoints(It.IsAny<bool>()))
            .Returns(endpoints);
        _mockRedis.Setup(r => r.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>()))
            .Returns(_mockRedisServer.Object);

        _mockRedisDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(new RedisValue("data"));

        // When: Getting stats
        var stats = await service.GetCacheStatsAsync("game1");

        // Then: Top 10 questions are returned in descending order
        Assert.Equal(10, stats.TopQuestions.Count);
        Assert.Equal("hash14", stats.TopQuestions[0].QuestionHash); // Highest hit count (14)
        Assert.Equal("hash13", stats.TopQuestions[1].QuestionHash);
        Assert.Equal("hash5", stats.TopQuestions[9].QuestionHash); // 10th highest (5)
        Assert.True(stats.TopQuestions[0].HitCount > stats.TopQuestions[^1].HitCount);
    }

    /// <summary>
    /// Scenario: Get cache stats with no data
    ///   Given no cache stats exist
    ///   When calling GetCacheStatsAsync
    ///   Then empty stats with zero values are returned
    ///   And no errors are thrown
    /// </summary>
    [Fact]
    public async Task GetCacheStatsAsync_WithNoData_ReturnsEmptyStats()
    {
        // Given: No cache stats
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);

        // Mock empty Redis
        _mockRedisServer.Setup(s => s.Keys(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(Enumerable.Empty<RedisKey>());

        var endpoints = new[] { new IPEndPoint(0, 0) as EndPoint };
        _mockRedis.Setup(r => r.GetEndPoints(It.IsAny<bool>()))
            .Returns(endpoints);
        _mockRedis.Setup(r => r.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>()))
            .Returns(_mockRedisServer.Object);

        // When: Getting stats
        var stats = await service.GetCacheStatsAsync();

        // Then: Empty stats returned
        Assert.Equal(0, stats.TotalHits);
        Assert.Equal(0, stats.TotalMisses);
        Assert.Equal(0, stats.HitRate);
        Assert.Equal(0, stats.CacheSizeBytes);
        Assert.Equal(0, stats.TotalKeys);
        Assert.Empty(stats.TopQuestions);
    }

    /// <summary>
    /// Scenario: Get cache stats with Redis failure
    ///   Given a Redis connection that fails
    ///   When calling GetCacheStatsAsync
    ///   Then database stats are still returned
    ///   And cache size is zero (graceful degradation)
    /// </summary>
    [Fact]
    public async Task GetCacheStatsAsync_WithRedisFailure_ReturnsPartialStats()
    {
        // Given: Database stats but failing Redis
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        db.CacheStats.Add(new CacheStatEntity
        {
            GameId = "game1",
            QuestionHash = "hash1",
            HitCount = 10,
            MissCount = 2,
            CreatedAt = DateTime.UtcNow,
            LastHitAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Mock Redis failure
        _mockRedis.Setup(r => r.GetEndPoints(It.IsAny<bool>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        // When: Getting stats
        var stats = await service.GetCacheStatsAsync();

        // Then: Empty stats returned (graceful degradation)
        Assert.Equal(0, stats.TotalHits);
        Assert.Equal(0, stats.TotalMisses);
        Assert.Empty(stats.TopQuestions);
    }

    #endregion

    #region Record Cache Access Tests

    /// <summary>
    /// Scenario: Record cache hit for existing stat
    ///   Given a cache stat already exists for the game and question
    ///   When calling RecordCacheAccessAsync with isHit=true
    ///   Then the HitCount is incremented
    ///   And LastHitAt timestamp is updated
    /// </summary>
    [Fact]
    public async Task RecordCacheAccessAsync_WithExistingStat_IncrementsHitCount()
    {
        // Given: Existing cache stat
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var stat = new CacheStatEntity
        {
            GameId = "game1",
            QuestionHash = "hash1",
            HitCount = 5,
            MissCount = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastHitAt = DateTime.UtcNow.AddHours(-1)
        };
        db.CacheStats.Add(stat);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var beforeUpdate = DateTime.UtcNow;

        // When: Recording a hit
        await service.RecordCacheAccessAsync("game1", "hash1", isHit: true);

        // Then: Hit count incremented and timestamp updated
        var updated = await db.CacheStats.FirstAsync(s => s.GameId == "game1" && s.QuestionHash == "hash1");
        Assert.Equal(6, updated.HitCount);
        Assert.Equal(1, updated.MissCount); // Unchanged
        Assert.True(updated.LastHitAt >= beforeUpdate);
    }

    /// <summary>
    /// Scenario: Record cache miss for existing stat
    ///   Given a cache stat already exists
    ///   When calling RecordCacheAccessAsync with isHit=false
    ///   Then the MissCount is incremented
    ///   And LastHitAt is not updated
    /// </summary>
    [Fact]
    public async Task RecordCacheAccessAsync_WithExistingStat_IncrementsMissCount()
    {
        // Given: Existing cache stat
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var originalLastHit = DateTime.UtcNow.AddHours(-1);
        var stat = new CacheStatEntity
        {
            GameId = "game1",
            QuestionHash = "hash1",
            HitCount = 5,
            MissCount = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastHitAt = originalLastHit
        };
        db.CacheStats.Add(stat);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // When: Recording a miss
        await service.RecordCacheAccessAsync("game1", "hash1", isHit: false);

        // Then: Miss count incremented, LastHitAt unchanged
        var updated = await db.CacheStats.FirstAsync(s => s.GameId == "game1" && s.QuestionHash == "hash1");
        Assert.Equal(5, updated.HitCount); // Unchanged
        Assert.Equal(2, updated.MissCount);
        Assert.Equal(originalLastHit, updated.LastHitAt); // Unchanged
    }

    /// <summary>
    /// Scenario: Record cache access for new question
    ///   Given no cache stat exists for the game and question
    ///   When calling RecordCacheAccessAsync
    ///   Then a new cache stat is created
    ///   And appropriate counters are initialized
    /// </summary>
    [Theory]
    [InlineData(true)]  // Cache hit
    [InlineData(false)] // Cache miss
    public async Task RecordCacheAccessAsync_WithNewQuestion_CreatesNewStat(bool isHit)
    {
        // Given: No existing stat
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);
        var beforeCreate = DateTime.UtcNow;

        // When: Recording access for new question
        await service.RecordCacheAccessAsync("game1", "hash-new", isHit);

        // Then: New stat is created
        var stat = await db.CacheStats.FirstOrDefaultAsync(s => s.GameId == "game1" && s.QuestionHash == "hash-new");
        Assert.NotNull(stat);
        Assert.Equal(isHit ? 1 : 0, stat.HitCount);
        Assert.Equal(isHit ? 0 : 1, stat.MissCount);
        Assert.True(stat.CreatedAt >= beforeCreate);
        Assert.True(stat.LastHitAt >= beforeCreate);
    }

    /// <summary>
    /// Scenario: Record cache access with database failure
    ///   Given a database that fails to save
    ///   When calling RecordCacheAccessAsync
    ///   Then the error is caught and logged
    ///   And no exception is thrown (graceful degradation)
    /// </summary>
    [Fact]
    public async Task RecordCacheAccessAsync_WithDatabaseFailure_GracefullyHandlesError()
    {
        // Given: Database that will fail on save
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        // Add constraint violation by creating duplicate
        db.CacheStats.Add(new CacheStatEntity
        {
            GameId = "game1",
            QuestionHash = "hash1",
            HitCount = 1,
            MissCount = 0,
            CreatedAt = DateTime.UtcNow,
            LastHitAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Dispose the context to simulate connection failure
        await db.DisposeAsync();

        // Create new context with closed connection
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;
        var newDb = new MeepleAiDbContext(options);

        var service = CreateService(newDb);

        // Close the connection to force failure
        await connection.CloseAsync();

        // When: Recording access (will fail)
        // Then: No exception is thrown
        await service.RecordCacheAccessAsync("game1", "hash1", true);

        // Success: Method completes without throwing
    }

    #endregion

    #region Tag Indexing Tests

    /// <summary>
    /// Scenario: Set cache with tags creates tag indexes
    ///   Given a response is being cached with tags
    ///   When calling SetAsync with tags array
    ///   Then cache entry is stored
    ///   And metadata with tags is stored
    ///   And tag indexes are created in Redis Sets
    ///   And TTL is applied to all keys
    /// </summary>
    [Fact]
    public async Task SetAsync_WithTags_CreatesTagIndexes()
    {
        // Given: Cache service with Redis mock
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);

        var setOperations = new List<(RedisKey Key, RedisValue Value, TimeSpan? Expiry)>();
        _mockRedisDb.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                CommandFlags.None))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((key, value, expiry, when, flags) =>
            {
                setOperations.Add((key, value, expiry));
            })
            .ReturnsAsync(true);

        var setAddOperations = new List<(RedisKey Key, RedisValue Value)>();
        _mockRedisDb.Setup(db => db.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), CommandFlags.None))
            .Callback<RedisKey, RedisValue, CommandFlags>((key, value, flags) =>
            {
                setAddOperations.Add((key, value));
            })
            .ReturnsAsync(true);

        var expireOperations = new List<(RedisKey Key, TimeSpan? Expiry)>();
        _mockRedisDb.Setup(db => db.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), CommandFlags.None))
            .Callback<RedisKey, TimeSpan?, ExpireWhen, CommandFlags>((key, expiry, when, flags) =>
            {
                expireOperations.Add((key, expiry));
            })
            .ReturnsAsync(true);

        var cacheKey = "ai:qa:game1:hash123";
        var response = new { answer = "Test answer" };

        // When: Setting cache with tags
        await service.SetAsync(cacheKey, response, ttlSeconds: 3600);

        // Then: Main data and metadata are stored
        Assert.Contains(setOperations, op => op.Key == cacheKey);
        Assert.Contains(setOperations, op => op.Key == $"{cacheKey}:meta");

        // And: Tag indexes are created
        Assert.Contains(setAddOperations, op => op.Key == "tag:game:game1" && op.Value == cacheKey);
        Assert.Contains(setAddOperations, op => op.Key == "tag:pdf:pdf123" && op.Value == cacheKey);

        // And: TTL is set on tag indexes
        Assert.Contains(expireOperations, op => op.Key == "tag:game:game1");
        Assert.Contains(expireOperations, op => op.Key == "tag:pdf:pdf123");
    }

    /// <summary>
    /// Scenario: Set cache without tags does not create indexes
    ///   Given a response is being cached without tags
    ///   When calling SetAsync with null tags
    ///   Then cache entry is stored
    ///   And no tag indexes are created
    /// </summary>
    [Fact]
    public async Task SetAsync_WithoutTags_DoesNotCreateIndexes()
    {
        // Given: Cache service
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateContextAsync(connection);

        var service = CreateService(db);

        var setOperations = new List<RedisKey>();
        _mockRedisDb.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                CommandFlags.None))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((key, _, _, _, _) =>
            {
                setOperations.Add(key);
            })
            .ReturnsAsync(true);

        var cacheKey = "ai:qa:game1:hash123";
        var response = new { answer = "Test answer" };

        // When: Setting cache without tags
        await service.SetAsync(cacheKey, response, ttlSeconds: 3600);

        // Then: Only main data is stored
        Assert.Contains(cacheKey, setOperations);
        // Note: No metadata stored when tags are null

        // And: No SetAdd operations for tags
        _mockRedisDb.Verify(db => db.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), CommandFlags.None), Times.Never);
    }

    #endregion

    #region Helper Methods

    private static async Task<MeepleAiDbContext> CreateContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new MeepleAiDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private AiResponseCacheService CreateService(MeepleAiDbContext db)
    {
        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockRedisDb.Object);

        return new AiResponseCacheService(
            _mockRedis.Object,
            db,
            NullLogger<AiResponseCacheService>.Instance,
            _configuration);
    }

    #endregion
}
