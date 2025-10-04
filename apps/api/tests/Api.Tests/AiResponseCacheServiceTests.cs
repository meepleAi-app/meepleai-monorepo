using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests;

/// <summary>
/// AI-05: Tests for AI response caching service
/// Verifies cache hit rate ≥60% on test dataset
/// </summary>
public class AiResponseCacheServiceTests
{
    private readonly Mock<ILogger<AiResponseCacheService>> _mockLogger = new();
    private readonly Mock<IConnectionMultiplexer> _mockRedis = new();
    private readonly Mock<IDatabase> _mockDatabase = new();

    public AiResponseCacheServiceTests()
    {
        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);
    }

    [Fact]
    public void GenerateQaCacheKey_CreatesConsistentHash()
    {
        // Arrange
        var service = new AiResponseCacheService(_mockRedis.Object, _mockLogger.Object);
        var tenantId = "tenant1";
        var gameId = "catan";
        var query = "How many resources can I hold?";

        // Act
        var key1 = service.GenerateQaCacheKey(tenantId, gameId, query);
        var key2 = service.GenerateQaCacheKey(tenantId, gameId, query);

        // Assert
        Assert.Equal(key1, key2);
        Assert.StartsWith("ai:qa:tenant1:catan:", key1);
    }

    [Fact]
    public void GenerateQaCacheKey_IsCaseInsensitive()
    {
        // Arrange
        var service = new AiResponseCacheService(_mockRedis.Object, _mockLogger.Object);
        var tenantId = "tenant1";
        var gameId = "catan";

        // Act
        var key1 = service.GenerateQaCacheKey(tenantId, gameId, "How many cards?");
        var key2 = service.GenerateQaCacheKey(tenantId, gameId, "HOW MANY CARDS?");

        // Assert - Same query different case should produce same key
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GenerateQaCacheKey_TrimsWhitespace()
    {
        // Arrange
        var service = new AiResponseCacheService(_mockRedis.Object, _mockLogger.Object);
        var tenantId = "tenant1";
        var gameId = "catan";

        // Act
        var key1 = service.GenerateQaCacheKey(tenantId, gameId, "How many cards?");
        var key2 = service.GenerateQaCacheKey(tenantId, gameId, "  How many cards?  ");

        // Assert - Whitespace should not affect hash
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GenerateExplainCacheKey_CreatesValidKey()
    {
        // Arrange
        var service = new AiResponseCacheService(_mockRedis.Object, _mockLogger.Object);

        // Act
        var key = service.GenerateExplainCacheKey("tenant1", "catan", "Trading Phase");

        // Assert
        Assert.StartsWith("ai:explain:tenant1:catan:", key);
    }

    [Fact]
    public void GenerateSetupCacheKey_CreatesValidKey()
    {
        // Arrange
        var service = new AiResponseCacheService(_mockRedis.Object, _mockLogger.Object);

        // Act
        var key = service.GenerateSetupCacheKey("tenant1", "catan");

        // Assert
        Assert.Equal("ai:setup:tenant1:catan", key);
    }

    [Fact]
    public async Task GetAsync_WithNoCache_ReturnsNull()
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var service = new AiResponseCacheService(_mockRedis.Object, _mockLogger.Object);

        // Act
        var result = await service.GetAsync<QaResponse>("test-key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_StoresValue_WithCorrectTTL()
    {
        // Arrange
        RedisValue storedValue = default;
        TimeSpan? storedTtl = null;

        _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>(
                (k, v, ttl, keepTtl, when, flags) =>
                {
                    storedValue = v;
                    storedTtl = ttl;
                })
            .ReturnsAsync(true);

        var service = new AiResponseCacheService(_mockRedis.Object, _mockLogger.Object);
        var response = new QaResponse("Test answer", Array.Empty<Snippet>());

        // Act
        await service.SetAsync("test-key", response, ttlSeconds: 3600);

        // Assert
        Assert.NotEqual(default, storedValue);
        Assert.NotNull(storedTtl);
        Assert.Equal(TimeSpan.FromSeconds(3600), storedTtl.Value);
    }

    [Fact]
    public async Task CacheHitRate_OnRepeatedQueries_MeetsTarget()
    {
        // Arrange: Simulate a real Redis scenario with cache storage
        var cache = new Dictionary<string, string>();

        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
            {
                return cache.TryGetValue(key.ToString(), out var value)
                    ? RedisValue.Unbox(value)
                    : RedisValue.Null;
            });

        _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>(
                (key, value, ttl, keepTtl, when, flags) =>
                {
                    cache[key.ToString()] = value.ToString();
                })
            .ReturnsAsync(true);

        var service = new AiResponseCacheService(_mockRedis.Object, _mockLogger.Object);

        // Test dataset: 10 unique queries, repeated 100 times total (90 should be cache hits)
        var queries = new[]
        {
            "How many resources can I hold?",
            "What happens when I roll a 7?",
            "Can I trade with other players?",
            "How do I win the game?",
            "What are development cards?",
            "Can I move the robber?",
            "How many victory points do I need?",
            "What is the longest road?",
            "Can I build on my turn?",
            "How do I get resources?"
        };

        var totalRequests = 100;
        var cacheHits = 0;
        var cacheMisses = 0;

        // Act: Simulate 100 requests with 10 unique queries (each query repeated ~10 times)
        for (int i = 0; i < totalRequests; i++)
        {
            var query = queries[i % queries.Length];
            var cacheKey = service.GenerateQaCacheKey("tenant1", "catan", query);

            // Try to get from cache
            var cached = await service.GetAsync<QaResponse>(cacheKey);

            if (cached != null)
            {
                cacheHits++;
            }
            else
            {
                cacheMisses++;
                // Simulate cache miss: store response in cache
                var response = new QaResponse($"Answer to: {query}", Array.Empty<Snippet>());
                await service.SetAsync(cacheKey, response);
            }
        }

        var cacheHitRate = (double)cacheHits / totalRequests;

        // Assert: Cache hit rate should be ≥60% (AI-05 acceptance criteria)
        Assert.True(cacheHitRate >= 0.60,
            $"Cache hit rate {cacheHitRate:P} is below target of 60%. Hits: {cacheHits}, Misses: {cacheMisses}, Total: {totalRequests}");

        // With 10 unique queries repeated 10 times each, we expect:
        // - First occurrence of each query: cache miss (10 misses)
        // - Subsequent occurrences: cache hit (90 hits)
        // - Expected hit rate: 90/100 = 90%
        Assert.Equal(10, cacheMisses); // 10 unique queries
        Assert.Equal(90, cacheHits);   // 90 repeated queries
        Assert.Equal(0.90, cacheHitRate); // 90% hit rate
    }

    [Fact]
    public async Task CacheHitRate_WithVariedQueries_MeetsTarget()
    {
        // Arrange: More realistic scenario with varied question frequency
        var cache = new Dictionary<string, string>();

        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
            {
                return cache.TryGetValue(key.ToString(), out var value)
                    ? RedisValue.Unbox(value)
                    : RedisValue.Null;
            });

        _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>(
                (key, value, ttl, keepTtl, when, flags) =>
                {
                    cache[key.ToString()] = value.ToString();
                })
            .ReturnsAsync(true);

        var service = new AiResponseCacheService(_mockRedis.Object, _mockLogger.Object);

        // Popular questions (asked frequently)
        var popularQueries = new[]
        {
            "How many resources can I hold?",
            "What happens when I roll a 7?",
            "How do I win the game?"
        };

        // Less common questions
        var uncommonQueries = new[]
        {
            "Can I trade with the bank?",
            "What is a settlement?",
            "How many roads can I build?",
            "Can I build during another player's turn?"
        };

        var cacheHits = 0;
        var cacheMisses = 0;

        // Simulate realistic usage: popular queries asked multiple times
        // Popular: 3 queries × 20 times each = 60 requests
        for (int i = 0; i < 20; i++)
        {
            foreach (var query in popularQueries)
            {
                var cacheKey = service.GenerateQaCacheKey("tenant1", "catan", query);
                var cached = await service.GetAsync<QaResponse>(cacheKey);

                if (cached != null)
                {
                    cacheHits++;
                }
                else
                {
                    cacheMisses++;
                    var response = new QaResponse($"Answer to: {query}", Array.Empty<Snippet>());
                    await service.SetAsync(cacheKey, response);
                }
            }
        }

        // Uncommon: 4 queries × 5 times each = 20 requests
        for (int i = 0; i < 5; i++)
        {
            foreach (var query in uncommonQueries)
            {
                var cacheKey = service.GenerateQaCacheKey("tenant1", "catan", query);
                var cached = await service.GetAsync<QaResponse>(cacheKey);

                if (cached != null)
                {
                    cacheHits++;
                }
                else
                {
                    cacheMisses++;
                    var response = new QaResponse($"Answer to: {query}", Array.Empty<Snippet>());
                    await service.SetAsync(cacheKey, response);
                }
            }
        }

        var totalRequests = 80; // 60 + 20
        var cacheHitRate = (double)cacheHits / totalRequests;

        // Assert: Cache hit rate should be ≥60%
        Assert.True(cacheHitRate >= 0.60,
            $"Cache hit rate {cacheHitRate:P} is below target of 60%. Hits: {cacheHits}, Misses: {cacheMisses}, Total: {totalRequests}");

        // Expected:
        // - Popular: 3 misses (first time) + 57 hits (19 repeats × 3) = 57/60 = 95%
        // - Uncommon: 4 misses (first time) + 16 hits (4 repeats × 4) = 16/20 = 80%
        // - Overall: 73 hits / 80 total = 91.25%
        Assert.Equal(7, cacheMisses);  // 3 popular + 4 uncommon
        Assert.Equal(73, cacheHits);   // 57 popular + 16 uncommon
        Assert.Equal(0.9125, cacheHitRate, precision: 4);
    }
}
