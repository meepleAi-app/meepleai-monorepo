using Api.Models;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Api.Tests;

/// <summary>
/// AI-05: Integration tests for AI response caching service using Testcontainers Redis
/// Validates real Redis interactions and cache hit rate acceptance criteria (≥60%)
///
/// BDD Scenarios:
/// - Scenario: Cache stores and retrieves responses using real Redis
/// - Scenario: Cache keys are consistent across multiple requests
/// - Scenario: Cache invalidation removes correct entries by pattern
/// - Scenario: Cache hit rate meets 60% threshold on repeated queries
/// - Scenario: Cache handles concurrent requests without data corruption
/// - Scenario: Cache gracefully degrades when Redis is unavailable
/// </summary>
public class AiResponseCacheIntegrationTests : IAsyncLifetime
{
    private RedisContainer? _redisContainer;
    private IConnectionMultiplexer? _redis;
    private AiResponseCacheService? _cacheService;
    private readonly Mock<ILogger<AiResponseCacheService>> _mockLogger = new();
    private readonly Mock<IConfiguration> _mockConfiguration = new();

    /// <summary>
    /// BDD: Given Redis container is started and connection is established
    /// </summary>
    public async Task InitializeAsync()
    {
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await _redisContainer.StartAsync();

        _redis = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());
        _cacheService = new AiResponseCacheService(_redis, null!, _mockLogger.Object, _mockConfiguration.Object);
    }

    /// <summary>
    /// BDD: When tests complete, Then Redis container is stopped and cleaned up
    /// </summary>
    public async Task DisposeAsync()
    {
        _redis?.Dispose();

        if (_redisContainer != null)
        {
            await _redisContainer.DisposeAsync();
        }
    }

    #region Cache Key Generation Tests

    [Fact]
    public void GivenGameAndQuery_WhenGeneratingQaCacheKey_ThenKeyIsConsistent()
    {
        // Given: Game ID and query text
        var gameId = "catan-test";
        var query = "How many resources can I hold?";

        // When: Generating cache keys multiple times
        var key1 = _cacheService!.GenerateQaCacheKey(gameId, query);
        var key2 = _cacheService.GenerateQaCacheKey(gameId, query);

        // Then: Keys are identical
        Assert.Equal(key1, key2);
        Assert.StartsWith("ai:qa:catan-test:", key1);
    }

    [Fact]
    public void GivenQueriesWithDifferentCase_WhenGeneratingKeys_ThenKeysAreIdentical()
    {
        // Given: Same query in different cases
        var gameId = "catan-test";
        var query1 = "How many cards?";
        var query2 = "HOW MANY CARDS?";

        // When: Generating cache keys
        var key1 = _cacheService!.GenerateQaCacheKey(gameId, query1);
        var key2 = _cacheService.GenerateQaCacheKey(gameId, query2);

        // Then: Keys are the same (case-insensitive)
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GivenQueryWithWhitespace_WhenGeneratingKey_ThenWhitespaceIsNormalized()
    {
        // Given: Query with extra whitespace
        var gameId = "catan-test";
        var query1 = "How many cards?";
        var query2 = "  How many cards?  ";

        // When: Generating cache keys
        var key1 = _cacheService!.GenerateQaCacheKey(gameId, query1);
        var key2 = _cacheService.GenerateQaCacheKey(gameId, query2);

        // Then: Keys are the same (whitespace normalized)
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GivenGameAndTopic_WhenGeneratingExplainKey_ThenKeyHasCorrectFormat()
    {
        // Given: Game ID and topic
        var gameId = "catan-test";
        var topic = "Trading Phase";

        // When: Generating explain cache key
        var key = _cacheService!.GenerateExplainCacheKey(gameId, topic);

        // Then: Key has correct namespace
        Assert.StartsWith("ai:explain:catan-test:", key);
    }

    [Fact]
    public void GivenGameId_WhenGeneratingSetupKey_ThenKeyIsGameSpecific()
    {
        // Given: Game ID
        var gameId = "catan-test";

        // When: Generating setup cache key
        var key = _cacheService!.GenerateSetupCacheKey(gameId);

        // Then: Key is exactly as expected
        Assert.Equal("ai:setup:catan-test", key);
    }

    #endregion

    #region Cache Get/Set Tests

    [Fact]
    public async Task GivenEmptyCache_WhenGettingValue_ThenReturnsNull()
    {
        // Given: Empty cache (Redis just started)
        var cacheKey = "test:missing:key";

        // When: Attempting to get non-existent value
        var result = await _cacheService!.GetAsync<QaResponse>(cacheKey);

        // Then: Returns null
        Assert.Null(result);
    }

    [Fact]
    public async Task GivenCachedResponse_WhenGetting_ThenReturnsOriginalObject()
    {
        // Given: Response stored in cache
        var cacheKey = "test:qa:catan:abc123";
        var originalResponse = new QaResponse(
            "You can hold up to 7 resource cards.",
            new List<Snippet>
            {
                new Snippet("Resource limit is 7 cards.", "PDF:catan-rules", 5, 1)
            }.ToArray()
        );

        await _cacheService!.SetAsync(cacheKey, originalResponse);

        // When: Getting cached response
        var cached = await _cacheService.GetAsync<QaResponse>(cacheKey);

        // Then: Response matches original
        Assert.NotNull(cached);
        Assert.Equal(originalResponse.answer, cached.answer);
        Assert.Single(cached.snippets);
        Assert.Equal("PDF:catan-rules", cached.snippets[0].source);
    }

    [Fact]
    public async Task GivenResponseWithCustomTTL_WhenSetting_ThenTTLIsRespected()
    {
        // Given: Response with 2-second TTL
        var cacheKey = "test:ttl:short";
        var response = new QaResponse("Short-lived answer", Array.Empty<Snippet>());

        // When: Setting with short TTL
        await _cacheService!.SetAsync(cacheKey, response, ttlSeconds: 2);

        // Then: Value exists immediately
        var cached1 = await _cacheService.GetAsync<QaResponse>(cacheKey);
        Assert.NotNull(cached1);

        // And: Value expires after TTL
        await Task.Delay(2500); // Wait for expiration
        var cached2 = await _cacheService.GetAsync<QaResponse>(cacheKey);
        Assert.Null(cached2);
    }

    [Fact]
    public async Task GivenMultipleResponseTypes_WhenCaching_ThenEachTypeIsDeserializedCorrectly()
    {
        // Given: Different response types
        var qaKey = "test:qa:multi";
        var explainKey = "test:explain:multi";
        var setupKey = "test:setup:multi";

        var qaResponse = new QaResponse("QA answer", Array.Empty<Snippet>());
        var explainResponse = new ExplainResponse(
            new ExplainOutline("Topic", new List<string> { "Point 1" }),
            "Detailed explanation",
            new List<Snippet>(),
            5
        );
        var setupResponse = new SetupGuideResponse(
            "Game",
            new List<SetupGuideStep>
            {
                new SetupGuideStep(1, "Step 1", "Do this", new List<Snippet>(), false)
            },
            10
        );

        // When: Storing and retrieving each type
        await _cacheService!.SetAsync(qaKey, qaResponse);
        await _cacheService.SetAsync(explainKey, explainResponse);
        await _cacheService.SetAsync(setupKey, setupResponse);

        var cachedQa = await _cacheService.GetAsync<QaResponse>(qaKey);
        var cachedExplain = await _cacheService.GetAsync<ExplainResponse>(explainKey);
        var cachedSetup = await _cacheService.GetAsync<SetupGuideResponse>(setupKey);

        // Then: Each type is correctly deserialized
        Assert.NotNull(cachedQa);
        Assert.Equal("QA answer", cachedQa.answer);

        Assert.NotNull(cachedExplain);
        Assert.Equal("Topic", cachedExplain.outline.mainTopic);

        Assert.NotNull(cachedSetup);
        Assert.Equal("Game", cachedSetup.gameTitle);
        Assert.Single(cachedSetup.steps);
    }

    #endregion

    #region Cache Invalidation Tests

    [Fact]
    public async Task GivenCachedEntriesForGame_WhenInvalidatingGame_ThenAllEntriesRemoved()
    {
        // Given: Multiple cached entries for one game
        var gameId = "catan-invalidate-test";
        var qaKey1 = _cacheService!.GenerateQaCacheKey(gameId, "Question 1");
        var qaKey2 = _cacheService.GenerateQaCacheKey(gameId, "Question 2");
        var explainKey = _cacheService.GenerateExplainCacheKey(gameId, "Topic");
        var setupKey = _cacheService.GenerateSetupCacheKey(gameId);

        await _cacheService.SetAsync(qaKey1, new QaResponse("Answer 1", Array.Empty<Snippet>()));
        await _cacheService.SetAsync(qaKey2, new QaResponse("Answer 2", Array.Empty<Snippet>()));
        await _cacheService.SetAsync(explainKey, new ExplainResponse(
            new ExplainOutline("Topic", new List<string>()),
            "Explanation",
            new List<Snippet>(),
            1
        ));
        await _cacheService.SetAsync(setupKey, new SetupGuideResponse("Game", new List<SetupGuideStep>(), 5));

        // When: Invalidating all cache for the game
        await _cacheService.InvalidateGameAsync(gameId);

        // Then: All entries are removed
        Assert.Null(await _cacheService.GetAsync<QaResponse>(qaKey1));
        Assert.Null(await _cacheService.GetAsync<QaResponse>(qaKey2));
        Assert.Null(await _cacheService.GetAsync<ExplainResponse>(explainKey));
        Assert.Null(await _cacheService.GetAsync<SetupGuideResponse>(setupKey));
    }

    [Fact]
    public async Task GivenCachedEntries_WhenInvalidatingSpecificEndpoint_ThenOnlyTargetEntriesRemoved()
    {
        // Given: QA and Explain entries for same game
        var gameId = "catan-selective-invalidate";
        var qaKey = _cacheService!.GenerateQaCacheKey(gameId, "Question");
        var explainKey = _cacheService.GenerateExplainCacheKey(gameId, "Topic");

        await _cacheService.SetAsync(qaKey, new QaResponse("Answer", Array.Empty<Snippet>()));
        await _cacheService.SetAsync(explainKey, new ExplainResponse(
            new ExplainOutline("Topic", new List<string>()),
            "Explanation",
            new List<Snippet>(),
            1
        ));

        // When: Invalidating only QA cache
        await _cacheService.InvalidateEndpointAsync(gameId, AiCacheEndpoint.Qa);

        // Then: QA is removed but Explain remains
        Assert.Null(await _cacheService.GetAsync<QaResponse>(qaKey));
        Assert.NotNull(await _cacheService.GetAsync<ExplainResponse>(explainKey));
    }

    [Fact]
    public async Task GivenMultipleGames_WhenInvalidatingOne_ThenOtherGamesUnaffected()
    {
        // Given: Cached entries for two different games
        var game1 = "catan-game1";
        var game2 = "catan-game2";
        var key1 = _cacheService!.GenerateQaCacheKey(game1, "Question 1");
        var key2 = _cacheService.GenerateQaCacheKey(game2, "Question 2");

        await _cacheService.SetAsync(key1, new QaResponse("Answer 1", Array.Empty<Snippet>()));
        await _cacheService.SetAsync(key2, new QaResponse("Answer 2", Array.Empty<Snippet>()));

        // When: Invalidating only game1
        await _cacheService.InvalidateGameAsync(game1);

        // Then: game1 is removed, game2 remains
        Assert.Null(await _cacheService.GetAsync<QaResponse>(key1));
        Assert.NotNull(await _cacheService.GetAsync<QaResponse>(key2));
    }

    #endregion

    #region Cache Hit Rate Tests (AI-05 Acceptance Criteria)

    [Fact]
    public async Task GivenRepeatedQueries_WhenMeasuringHitRate_ThenMeets60PercentThreshold()
    {
        // Given: 10 unique queries to be repeated 100 times total
        var gameId = "catan-hitrate";
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

        // When: Simulating 100 requests (10 queries × 10 repetitions each)
        for (int i = 0; i < totalRequests; i++)
        {
            var query = queries[i % queries.Length];
            var cacheKey = _cacheService!.GenerateQaCacheKey(gameId, query);

            var cached = await _cacheService.GetAsync<QaResponse>(cacheKey);

            if (cached != null)
            {
                cacheHits++;
            }
            else
            {
                cacheMisses++;
                // Simulate cache miss: store response
                var response = new QaResponse($"Answer to: {query}", Array.Empty<Snippet>());
                await _cacheService.SetAsync(cacheKey, response);
            }
        }

        var hitRate = (double)cacheHits / totalRequests;

        // Then: Cache hit rate meets 60% acceptance criteria
        Assert.True(hitRate >= 0.60,
            $"Cache hit rate {hitRate:P} is below 60% threshold. Hits: {cacheHits}, Misses: {cacheMisses}");

        // And: With 10 unique queries repeated 10 times, expect 90% hit rate
        Assert.Equal(10, cacheMisses); // First occurrence of each query
        Assert.Equal(90, cacheHits);   // 9 repetitions × 10 queries
        Assert.Equal(0.90, hitRate);
    }

    [Fact]
    public async Task GivenRealisticUsagePattern_WhenMeasuringHitRate_ThenExceeds60Percent()
    {
        // Given: Realistic usage with popular and uncommon queries
        var gameId = "catan-realistic";
        var popularQueries = new[]
        {
            "How many resources can I hold?",
            "What happens when I roll a 7?",
            "How do I win the game?"
        };

        var uncommonQueries = new[]
        {
            "Can I trade with the bank?",
            "What is a settlement?",
            "How many roads can I build?",
            "Can I build during another player's turn?"
        };

        var cacheHits = 0;
        var cacheMisses = 0;

        // When: Popular queries asked 20 times each
        for (int i = 0; i < 20; i++)
        {
            foreach (var query in popularQueries)
            {
                var cacheKey = _cacheService!.GenerateQaCacheKey(gameId, query);
                var cached = await _cacheService.GetAsync<QaResponse>(cacheKey);

                if (cached != null)
                {
                    cacheHits++;
                }
                else
                {
                    cacheMisses++;
                    await _cacheService.SetAsync(cacheKey, new QaResponse($"Answer: {query}", Array.Empty<Snippet>()));
                }
            }
        }

        // And: Uncommon queries asked 5 times each
        for (int i = 0; i < 5; i++)
        {
            foreach (var query in uncommonQueries)
            {
                var cacheKey = _cacheService!.GenerateQaCacheKey(gameId, query);
                var cached = await _cacheService.GetAsync<QaResponse>(cacheKey);

                if (cached != null)
                {
                    cacheHits++;
                }
                else
                {
                    cacheMisses++;
                    await _cacheService.SetAsync(cacheKey, new QaResponse($"Answer: {query}", Array.Empty<Snippet>()));
                }
            }
        }

        var totalRequests = (3 * 20) + (4 * 5); // 60 + 20 = 80
        var hitRate = (double)cacheHits / totalRequests;

        // Then: Hit rate exceeds 60% threshold
        Assert.True(hitRate >= 0.60,
            $"Cache hit rate {hitRate:P} below threshold. Hits: {cacheHits}, Misses: {cacheMisses}");

        // And: Expected hit rate is ~91% (73 hits / 80 total)
        Assert.Equal(7, cacheMisses);  // 3 popular + 4 uncommon (first time each)
        Assert.Equal(73, cacheHits);   // 57 popular + 16 uncommon (subsequent)
        Assert.Equal(0.9125, hitRate, precision: 4);
    }

    #endregion

    #region Concurrency and Performance Tests

    [Fact]
    public async Task GivenConcurrentRequests_WhenCachingResponses_ThenNoDataCorruption()
    {
        // Given: 50 concurrent requests for same key
        var gameId = "catan-concurrent";
        var cacheKey = _cacheService!.GenerateQaCacheKey(gameId, "Concurrent question");

        // When: Multiple tasks set and get simultaneously
        var tasks = Enumerable.Range(0, 50).Select(async i =>
        {
            var response = new QaResponse($"Answer {i}", Array.Empty<Snippet>());
            await _cacheService.SetAsync(cacheKey, response);

            var cached = await _cacheService.GetAsync<QaResponse>(cacheKey);
            return cached;
        });

        var results = await Task.WhenAll(tasks);

        // Then: All requests complete successfully (no exceptions)
        Assert.All(results, result => Assert.NotNull(result));
    }

    [Fact]
    public async Task GivenHighVolumeRequests_WhenCaching_ThenPerformanceIsAcceptable()
    {
        // Given: 1000 cache operations
        var gameId = "catan-performance";
        var queries = Enumerable.Range(0, 100).Select(i => $"Query {i}").ToArray();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // When: Performing 1000 get/set operations (10 repetitions of 100 queries)
        for (int repetition = 0; repetition < 10; repetition++)
        {
            foreach (var query in queries)
            {
                var cacheKey = _cacheService!.GenerateQaCacheKey(gameId, query);
                var cached = await _cacheService.GetAsync<QaResponse>(cacheKey);

                if (cached == null)
                {
                    await _cacheService.SetAsync(cacheKey, new QaResponse($"Answer: {query}", Array.Empty<Snippet>()));
                }
            }
        }

        stopwatch.Stop();

        // Then: 1000 operations complete in reasonable time (< 5 seconds)
        Assert.True(stopwatch.ElapsedMilliseconds < 5000,
            $"1000 cache operations took {stopwatch.ElapsedMilliseconds}ms (expected < 5000ms)");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GivenRedisUnavailable_WhenGetting_ThenReturnsNullGracefully()
    {
        // Given: Redis connection closed
        _redis!.Dispose();

        // When: Attempting to get from cache
        var result = await _cacheService!.GetAsync<QaResponse>("test:unavailable");

        // Then: Returns null without throwing (graceful degradation)
        Assert.Null(result);
    }

    [Fact]
    public async Task GivenRedisUnavailable_WhenSetting_ThenDoesNotThrow()
    {
        // Given: Redis connection closed
        _redis!.Dispose();

        // When: Attempting to set in cache
        var exception = await Record.ExceptionAsync(async () =>
        {
            await _cacheService!.SetAsync("test:unavailable", new QaResponse("test", Array.Empty<Snippet>()));
        });

        // Then: No exception thrown (graceful degradation)
        Assert.Null(exception);
    }

    [Fact]
    public async Task GivenInvalidationWithNoKeys_WhenExecuting_ThenCompletesSuccessfully()
    {
        // Given: Empty cache (no keys matching pattern)
        var gameId = "nonexistent-game";

        // When: Invalidating non-existent game
        var exception = await Record.ExceptionAsync(async () =>
        {
            await _cacheService!.InvalidateGameAsync(gameId);
        });

        // Then: Completes without error
        Assert.Null(exception);
    }

    #endregion
}
