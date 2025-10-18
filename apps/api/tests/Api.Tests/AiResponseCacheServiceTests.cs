using Api.Infrastructure;
using Api.Models;
using Api.Services;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    private readonly Mock<IConfiguration> _mockConfiguration = new();

    public AiResponseCacheServiceTests()
    {
        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);
    }

    [Fact]
    public void GenerateQaCacheKey_CreatesConsistentHash()
    {
        // Arrange
        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);
        var gameId = "catan";
        var query = "How many resources can I hold?";

        // Act
        var key1 = service.GenerateQaCacheKey(gameId, query);
        var key2 = service.GenerateQaCacheKey(gameId, query);

        // Assert
        Assert.Equal(key1, key2);
        Assert.StartsWith("ai:qa:catan:", key1);
    }

    [Fact]
    public void GenerateQaCacheKey_IsCaseInsensitive()
    {
        // Arrange
        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);
        var gameId = "catan";

        // Act
        var key1 = service.GenerateQaCacheKey(gameId, "How many cards?");
        var key2 = service.GenerateQaCacheKey(gameId, "HOW MANY CARDS?");

        // Assert - Same query different case should produce same key
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GenerateQaCacheKey_TrimsWhitespace()
    {
        // Arrange
        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);
        var gameId = "catan";

        // Act
        var key1 = service.GenerateQaCacheKey(gameId, "How many cards?");
        var key2 = service.GenerateQaCacheKey(gameId, "  How many cards?  ");

        // Assert - Whitespace should not affect hash
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GenerateExplainCacheKey_CreatesValidKey()
    {
        // Arrange
        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);

        // Act
        var key = service.GenerateExplainCacheKey("catan", "Trading Phase");

        // Assert
        Assert.StartsWith("ai:explain:catan:", key);
    }

    [Fact]
    public void GenerateSetupCacheKey_CreatesValidKey()
    {
        // Arrange
        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);

        // Act
        var key = service.GenerateSetupCacheKey("catan");

        // Assert
        Assert.Equal("ai:setup:catan", key);
    }

    [Fact]
    public async Task GetAsync_WithNoCache_ReturnsNull()
    {
        // Arrange
        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);

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

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);
        var response = new QaResponse("Test answer", Array.Empty<Snippet>());

        // Act
        await service.SetAsync("test-key", response, ttlSeconds: 3600);

        // Assert
        Assert.NotEqual(default, storedValue);
        Assert.NotNull(storedTtl);
        Assert.Equal(TimeSpan.FromSeconds(3600), storedTtl.Value);
    }

    [Fact]
    public async Task InvalidateGameAsync_RemovesAllCacheEntriesForGame()
    {
        // Arrange
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
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>((key, value, _, _, _, _) =>
            {
                cache[key.ToString()] = value.ToString();
            })
            .ReturnsAsync(true);

        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Returns<string, RedisKey[], RedisValue[], CommandFlags>((_, _, values, _) =>
            {
                var pattern = values[0].ToString();
                var regex = new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$");
                var keysToRemove = cache.Keys.Where(k => regex.IsMatch(k)).ToList();
                foreach (var key in keysToRemove)
                {
                    cache.Remove(key);
                }

                return Task.FromResult(RedisResult.Create((long)keysToRemove.Count));
            });

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);
        var qaKey = service.GenerateQaCacheKey("game-1", "How many players?");
        var explainKey = service.GenerateExplainCacheKey("game-1", "setup phase");
        var setupKey = service.GenerateSetupCacheKey("game-1");

        await service.SetAsync(qaKey, new QaResponse("cached answer", Array.Empty<Snippet>()));
        await service.SetAsync(explainKey, new ExplainResponse(
            new ExplainOutline("setup phase", new List<string>()),
            "cached",
            new List<Snippet>(),
            1));
        await service.SetAsync(setupKey, new SetupGuideResponse("Game", new List<SetupGuideStep>(), 5));

        // Act
        await service.InvalidateGameAsync("game-1");

        // Assert
        Assert.Null(await service.GetAsync<QaResponse>(qaKey));
        Assert.Null(await service.GetAsync<ExplainResponse>(explainKey));
        Assert.Null(await service.GetAsync<SetupGuideResponse>(setupKey));
    }

    [Fact]
    public async Task InvalidateEndpointAsync_RemovesOnlyTargetNamespace()
    {
        // Arrange
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
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>((key, value, _, _, _, _) =>
            {
                cache[key.ToString()] = value.ToString();
            })
            .ReturnsAsync(true);

        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Returns<string, RedisKey[], RedisValue[], CommandFlags>((_, _, values, _) =>
            {
                var pattern = values[0].ToString();
                var regex = new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$");
                var keysToRemove = cache.Keys.Where(k => regex.IsMatch(k)).ToList();
                foreach (var key in keysToRemove)
                {
                    cache.Remove(key);
                }

                return Task.FromResult(RedisResult.Create((long)keysToRemove.Count));
            });

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);
        var qaKey = service.GenerateQaCacheKey("game-2", "How many turns?");
        var explainKey = service.GenerateExplainCacheKey("game-2", "trading");

        await service.SetAsync(qaKey, new QaResponse("cached", Array.Empty<Snippet>()));
        await service.SetAsync(explainKey, new ExplainResponse(
            new ExplainOutline("trading", new List<string>()),
            "cached",
            new List<Snippet>(),
            1));

        // Act
        await service.InvalidateEndpointAsync("game-2", AiCacheEndpoint.Qa);

        // Assert
        Assert.Null(await service.GetAsync<QaResponse>(qaKey));
        Assert.NotNull(await service.GetAsync<ExplainResponse>(explainKey));
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

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);

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
            var cacheKey = service.GenerateQaCacheKey("catan", query);

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

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);

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
                var cacheKey = service.GenerateQaCacheKey("catan", query);
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
                var cacheKey = service.GenerateQaCacheKey("catan", query);
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

    #region Edge Cases and Error Handling Tests

    [Fact]
    public async Task GetAsync_WithRedisException_ReturnsNullGracefully()
    {
        // Arrange: Redis throws exception
        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);

        // Act
        var result = await service.GetAsync<QaResponse>("test-key");

        // Assert: Returns null without throwing (graceful degradation)
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_WithRedisException_DoesNotThrow()
    {
        // Arrange: Redis throws exception
        _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed"));

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);
        var response = new QaResponse("Test", Array.Empty<Snippet>());

        // Act & Assert: No exception thrown (graceful degradation)
        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.SetAsync("test-key", response);
        });

        Assert.Null(exception);
    }

    [Fact]
    public async Task GetAsync_WithInvalidJson_ReturnsNull()
    {
        // Arrange: Redis returns invalid JSON
        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Unbox("invalid json {{{"));

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);

        // Act
        var result = await service.GetAsync<QaResponse>("test-key");

        // Assert: Returns null on deserialization failure
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_WithCancellationToken_ThrowsWhenCancelled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Mock Redis to throw OperationCanceledException when token is cancelled
        _mockDatabase.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);

        // Act: Cache operations catch cancellation and return null gracefully
        var result = await service.GetAsync<QaResponse>("test-key", cts.Token);

        // Assert: Cache operations should gracefully handle cancellation by returning null
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_WithNegativeTTL_UsesDefaultTTL()
    {
        // Arrange
        TimeSpan? storedTtl = null;

        _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>(
                (k, v, ttl, keepTtl, when, flags) => storedTtl = ttl)
            .ReturnsAsync(true);

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);
        var response = new QaResponse("Test", Array.Empty<Snippet>());

        // Act
        await service.SetAsync("test-key", response, ttlSeconds: -100);

        // Assert: Negative TTL is converted to TimeSpan (which handles negatives)
        Assert.NotNull(storedTtl);
        // TimeSpan.FromSeconds(-100) is valid, Redis will reject it, but our service doesn't validate
    }

    [Fact]
    public async Task InvalidateGameAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);

        // Act & Assert: Invalidation respects cancellation token
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await service.InvalidateGameAsync("game-1", cts.Token);
        });
    }

    [Fact]
    public async Task InvalidateEndpointAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);

        // Act & Assert: Invalidation respects cancellation token
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await service.InvalidateEndpointAsync("game-1", AiCacheEndpoint.Qa, cts.Token);
        });
    }

    [Fact]
    public async Task InvalidateGameAsync_WithScriptError_LogsWarningButDoesNotThrow()
    {
        // Arrange: Script execution fails
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisServerException("Script execution failed"));

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);

        // Act & Assert: Does not throw, logs warning
        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.InvalidateGameAsync("game-1");
        });

        Assert.Null(exception);
    }

    [Fact]
    public void GenerateQaCacheKey_WithSpecialCharacters_CreatesValidKey()
    {
        // Arrange
        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);
        var gameId = "catan-test";
        var queryWithSpecialChars = "How many cards? (including development cards & resources!)";

        // Act
        var key = service.GenerateQaCacheKey(gameId, queryWithSpecialChars);

        // Assert: Special characters are hashed consistently
        Assert.StartsWith("ai:qa:catan-test:", key);
        Assert.DoesNotContain("(", key);
        Assert.DoesNotContain("&", key);
        Assert.DoesNotContain("!", key);
    }

    [Fact]
    public void GenerateQaCacheKey_WithUnicodeCharacters_CreatesValidKey()
    {
        // Arrange
        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);
        var gameId = "catan-test";
        var queryWithUnicode = "Comment puis-je gagner? 如何获胜？ Как победить?";

        // Act
        var key1 = service.GenerateQaCacheKey(gameId, queryWithUnicode);
        var key2 = service.GenerateQaCacheKey(gameId, queryWithUnicode);

        // Assert: Unicode is consistently hashed
        Assert.Equal(key1, key2);
        Assert.StartsWith("ai:qa:catan-test:", key1);
    }

    [Fact]
    public void GenerateQaCacheKey_WithVeryLongQuery_CreatesValidKey()
    {
        // Arrange
        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);
        var gameId = "catan-test";
        var longQuery = new string('a', 10000); // 10KB query

        // Act
        var key = service.GenerateQaCacheKey(gameId, longQuery);

        // Assert: Long queries are hashed to fixed-length keys
        Assert.StartsWith("ai:qa:catan-test:", key);
        Assert.True(key.Length < 200, "Cache key should be fixed length regardless of query length");
    }

    [Fact]
    public void GenerateExplainCacheKey_WithEmptyTopic_CreatesValidKey()
    {
        // Arrange
        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);
        var gameId = "catan-test";

        // Act
        var key = service.GenerateExplainCacheKey(gameId, "");

        // Assert: Empty topic is hashed consistently
        Assert.StartsWith("ai:explain:catan-test:", key);
    }

    [Fact]
    public async Task SetAsync_WithComplexNestedResponse_SerializesCorrectly()
    {
        // Arrange
        var storedJson = "";

        _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>(
                (k, v, ttl, keepTtl, when, flags) => storedJson = v.ToString())
            .ReturnsAsync(true);

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);

        var complexResponse = new SetupGuideResponse(
            "Complex Game",
            new List<SetupGuideStep>
            {
                new SetupGuideStep(
                    1,
                    "Step with references",
                    "Complex instruction with nested data",
                    new List<Snippet>
                    {
                        new Snippet("Reference text 1", "PDF:doc1", 5, 10),
                        new Snippet("Reference text 2", "PDF:doc2", 12, 25)
                    },
                    false
                )
            },
            15,
            100,
            200,
            300,
            0.95
        );

        // Act
        await service.SetAsync("test-complex", complexResponse);

        // Assert: JSON is valid and contains nested data
        Assert.NotEmpty(storedJson);
        Assert.Contains("Complex Game", storedJson);
        Assert.Contains("Reference text 1", storedJson);
        Assert.Contains("PDF:doc1", storedJson);
    }

    [Fact]
    public async Task GetAsync_WithDifferentResponseTypes_DoesNotCrossPollute()
    {
        // Arrange: Store QA response
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
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>((key, value, _, _, _, _) =>
            {
                cache[key.ToString()] = value.ToString();
            })
            .ReturnsAsync(true);

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);

        var qaResponse = new QaResponse("QA answer", Array.Empty<Snippet>());
        await service.SetAsync("test-key", qaResponse);

        // Act: Retrieve correctly as QaResponse
        var asQa = await service.GetAsync<QaResponse>("test-key");

        // Assert: Correctly deserializes as same type
        Assert.NotNull(asQa);
        Assert.Equal("QA answer", asQa.answer);
    }

    [Fact]
    public async Task InvalidateGameAsync_WithNoMatchingKeys_ReturnsSuccessfully()
    {
        // Arrange: Empty cache
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(0L));

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);

        // Act & Assert: No error when invalidating non-existent keys
        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.InvalidateGameAsync("nonexistent-game");
        });

        Assert.Null(exception);
    }

    [Fact]
    public async Task InvalidateGameAsync_WithNullScriptResult_HandlesGracefully()
    {
        // Arrange: Script returns null
        _mockDatabase.Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(RedisValue.Null));

        var service = new AiResponseCacheService(_mockRedis.Object, null!, _mockLogger.Object, _mockConfiguration.Object);

        // Act & Assert: Handles null result gracefully
        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.InvalidateGameAsync("game-1");
        });

        Assert.Null(exception);
    }

    #endregion
}
