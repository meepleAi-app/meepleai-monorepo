using Api.BoundedContexts.KnowledgeBase.Domain.Services.Caching;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Caching;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Configuration;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Caching;

/// <summary>
/// ISSUE-3494: Integration tests for MultiTierCache with real Redis.
/// Tests L1+L2 integration, cache promotion, adaptive TTL, and warming.
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Issue", "3494")]
public sealed class MultiTierCacheIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly Mock<IRedisFrequencyTracker> _mockFrequencyTracker;
    private readonly Mock<ICacheMetricsRecorder> _mockMetricsRecorder;
    private readonly Mock<ILogger<MultiTierCache>> _mockLogger;
    private MultiTierCache _cache = null!;
    private IConnectionMultiplexer _redis = null!;
    private string _keyPrefix = null!;
    private readonly Guid _testGameId = Guid.NewGuid();

    public MultiTierCacheIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _mockFrequencyTracker = new Mock<IRedisFrequencyTracker>();
        _mockMetricsRecorder = new Mock<ICacheMetricsRecorder>();
        _mockLogger = new Mock<ILogger<MultiTierCache>>();
    }

    public async ValueTask InitializeAsync()
    {
        // Use unique key prefix per test class
        _keyPrefix = $"test:mtc:{Guid.NewGuid():N}:";

        // Connect to shared Redis container
        _redis = await ConnectionMultiplexer.ConnectAsync(_fixture.RedisConnectionString);

        // Create HybridCacheService with real Redis
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = _fixture.RedisConnectionString;
            options.InstanceName = _keyPrefix;
        });
        services.AddHybridCache();

        var serviceProvider = services.BuildServiceProvider();
        var hybridCache = serviceProvider.GetRequiredService<HybridCache>();

        var hybridCacheConfig = new HybridCacheConfiguration
        {
            EnableL2Cache = true,
            EnableTags = true,
            DefaultExpiration = TimeSpan.FromHours(1)
        };

        var hybridCacheService = new HybridCacheService(
            hybridCache,
            Options.Create(hybridCacheConfig),
            new Mock<ILogger<HybridCacheService>>().Object,
            _redis
        );

        // Configure MultiTierCache
        var config = new MultiTierCacheConfiguration
        {
            Enabled = true,
            L1MaxItems = 100,
            L1Enabled = true,
            L2Enabled = true,
            EnablePromotion = true,
            HighFrequencyThreshold = 100,
            MediumFrequencyThreshold = 10,
            HighFrequencyTtl = TimeSpan.FromHours(24),
            MediumFrequencyTtl = TimeSpan.FromHours(1),
            LowFrequencyTtl = TimeSpan.FromMinutes(5),
            PromotionToL1Threshold = 3,
            WarmingEnabled = true,
            WarmingTopNItems = 20,
            KeyPrefix = _keyPrefix
        };

        // Setup mocks
        _mockFrequencyTracker
            .Setup(x => x.TrackAccessAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _mockFrequencyTracker
            .Setup(x => x.GetFrequencyAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(5); // Default: low frequency

        _mockMetricsRecorder
            .Setup(x => x.RecordCacheHitAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _mockMetricsRecorder
            .Setup(x => x.RecordCacheMissAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _cache = new MultiTierCache(
            hybridCacheService,
            _mockFrequencyTracker.Object,
            _mockMetricsRecorder.Object,
            Options.Create(config),
            _mockLogger.Object
        );
    }

    public async ValueTask DisposeAsync()
    {
        // Clean up Redis keys with prefix
        await _fixture.FlushRedisByPrefixAsync(_keyPrefix + "*");

        // Dispose resources
        _cache.Dispose();
        await _redis.CloseAsync();
        _redis.Dispose();
    }

    [Fact]
    public async Task GetOrCreateAsync_WithRealRedis_ShouldPopulateL1AndL2()
    {
        // Arrange
        var key = "integration-test-key";
        var factoryCalls = 0;
        var expectedValue = new TestCacheItem { Data = "test-data" };

        // Act - First call should execute factory and populate both caches
        var result1 = await _cache.GetOrCreateAsync(
            key,
            _testGameId,
            _ =>
            {
                factoryCalls++;
                return Task.FromResult(expectedValue);
            },
            tags: new[] { "game:test" }
        );

        // Second call should hit cache (L1)
        var result2 = await _cache.GetOrCreateAsync(
            key,
            _testGameId,
            _ =>
            {
                factoryCalls++;
                return Task.FromResult(new TestCacheItem { Data = "should-not-be-called" });
            }
        );

        // Assert
        factoryCalls.Should().Be(1, "factory should execute only once");
        result1.Value.Data.Should().Be("test-data");
        result2.Value.Data.Should().Be("test-data");
        result1.SourceTier.Should().Be(CacheTier.Factory);
        result2.SourceTier.Should().Be(CacheTier.L1Memory);
    }

    [Fact]
    public async Task SetAsync_WithRealRedis_ShouldStoreInBothTiers()
    {
        // Arrange
        var key = "set-test-key";
        var value = new TestCacheItem { Data = "stored-data" };

        // Act - Set in cache
        await _cache.SetAsync(key, _testGameId, value, tags: new[] { "game:test" });

        // Verify - Should be retrievable from cache
        var result = await _cache.GetAsync<TestCacheItem>(key, _testGameId);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Data.Should().Be("stored-data");
        result.SourceTier.Should().Be(CacheTier.L1Memory);
    }

    [Fact]
    public async Task L2Hit_WithL1Miss_ShouldPromoteToL1()
    {
        // Arrange
        var key = "promotion-test-key";
        var value = new TestCacheItem { Data = "promote-me" };

        // Setup frequency high enough for promotion
        _mockFrequencyTracker
            .Setup(x => x.GetFrequencyAsync(_testGameId, key))
            .ReturnsAsync(5); // Above PromotionToL1Threshold (3)

        // Populate L2 only
        await _cache.SetAsync(key, _testGameId, value);

        // Create new cache instance to clear L1
        var newCache = new MultiTierCache(
            _cache.GetType().GetField("_l2Cache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(_cache) as IHybridCacheService
                ?? throw new InvalidOperationException(),
            _mockFrequencyTracker.Object,
            _mockMetricsRecorder.Object,
            Options.Create(new MultiTierCacheConfiguration
            {
                Enabled = true,
                L1Enabled = true,
                L2Enabled = true,
                EnablePromotion = true,
                PromotionToL1Threshold = 3,
                KeyPrefix = _keyPrefix
            }),
            _mockLogger.Object
        );

        // Act - Get should promote to L1
        var result1 = await newCache.GetAsync<TestCacheItem>(key, _testGameId);

        // Second get should hit L1
        var result2 = await newCache.GetAsync<TestCacheItem>(key, _testGameId);

        // Assert
        result1.Should().NotBeNull();
        result1!.SourceTier.Should().Be(CacheTier.L2Redis);
        result1.WasPromoted.Should().BeTrue();

        result2.Should().NotBeNull();
        result2!.SourceTier.Should().Be(CacheTier.L1Memory);

        newCache.Dispose();
    }

    [Fact]
    public async Task RemoveAsync_WithRealRedis_ShouldClearFromBothTiers()
    {
        // Arrange
        var key = "remove-test-key";
        var value = new TestCacheItem { Data = "to-remove" };

        await _cache.SetAsync(key, _testGameId, value);

        // Verify it's cached
        var beforeRemove = await _cache.GetAsync<TestCacheItem>(key, _testGameId);
        beforeRemove.Should().NotBeNull();

        // Act - Remove from cache
        await _cache.RemoveAsync(_keyPrefix + _testGameId + ":" + key);

        // Assert - Should be gone from both tiers
        var afterRemove = await _cache.GetAsync<TestCacheItem>(key, _testGameId);
        afterRemove.Should().BeNull();
    }

    [Fact]
    public async Task RemoveByTagAsync_WithRealRedis_ShouldInvalidateTaggedEntries()
    {
        // Arrange
        var key1 = "tagged-key-1";
        var key2 = "tagged-key-2";
        var key3 = "different-tag-key";
        var tag = "game:chess";

        await _cache.SetAsync(key1, _testGameId, new TestCacheItem { Data = "chess1" }, tags: new[] { tag });
        await _cache.SetAsync(key2, _testGameId, new TestCacheItem { Data = "chess2" }, tags: new[] { tag });
        await _cache.SetAsync(key3, _testGameId, new TestCacheItem { Data = "monopoly" }, tags: new[] { "game:monopoly" });

        // Act - Remove by tag
        var removed = await _cache.RemoveByTagAsync(tag);

        // Assert - Tagged entries should be gone
        removed.Should().BeGreaterThanOrEqualTo(2);

        // L1 is cleared conservatively, so all items are gone from L1
        // But we can verify the tag-based removal worked by checking L2 doesn't return them
        var result1 = await _cache.GetAsync<TestCacheItem>(key1, _testGameId);
        var result2 = await _cache.GetAsync<TestCacheItem>(key2, _testGameId);
        var result3 = await _cache.GetAsync<TestCacheItem>(key3, _testGameId);

        result1.Should().BeNull("chess1 should be removed by tag");
        result2.Should().BeNull("chess2 should be removed by tag");
        result3.Should().NotBeNull("monopoly should still be in L2 (different tag)");
        result3!.SourceTier.Should().Be(CacheTier.L2Redis, "monopoly retrieved from L2 after L1 clear");
    }

    [Fact]
    public async Task AdaptiveTtl_WithHighFrequency_ShouldReturn24Hours()
    {
        // Arrange
        var key = "high-freq-key";

        _mockFrequencyTracker
            .Setup(x => x.GetFrequencyAsync(_testGameId, key))
            .ReturnsAsync(150); // High frequency (>= 100)

        // Act
        var ttl = await _cache.CalculateAdaptiveTtlAsync(_testGameId, key);

        // Assert
        ttl.Should().Be(TimeSpan.FromHours(24));
    }

    [Fact]
    public async Task Metrics_AfterOperations_ShouldTrackHitsAndMisses()
    {
        // Arrange
        var key = "metrics-test-key";
        var value = new TestCacheItem { Data = "metrics-data" };

        // Act - Cache miss, then hit
        await _cache.SetAsync(key, _testGameId, value);
        await _cache.GetAsync<TestCacheItem>(key, _testGameId); // L1 hit
        await _cache.GetAsync<TestCacheItem>("non-existent", _testGameId); // Miss

        var metrics = _cache.GetMetrics();

        // Assert
        metrics.L1Memory.Should().NotBeNull();
        metrics.L1Memory.Hits.Should().BeGreaterThan(0);
        metrics.L1Memory.Misses.Should().BeGreaterThan(0);
        metrics.L2Redis.Should().NotBeNull();
    }

    [Fact]
    public async Task WarmGameCache_WithFrequentQueries_ShouldPopulateL1()
    {
        // Arrange
        var queries = new List<FrequentQuery>
        {
            new() { Query = "warm-key-1", AccessCount = 50 },
            new() { Query = "warm-key-2", AccessCount = 30 },
            new() { Query = "warm-key-3", AccessCount = 20 }
        };

        _mockFrequencyTracker
            .Setup(x => x.GetTopQueriesAsync(_testGameId, 20))
            .ReturnsAsync(queries);

        // Pre-populate L2 with these keys
        await _cache.SetAsync("warm-key-1", _testGameId, new TestCacheItem { Data = "data1" });
        await _cache.SetAsync("warm-key-2", _testGameId, new TestCacheItem { Data = "data2" });
        await _cache.SetAsync("warm-key-3", _testGameId, new TestCacheItem { Data = "data3" });

        // Clear L1 to simulate cold start
        _cache.GetType()
            .GetField("_l1Cache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(_cache)
            .GetType()
            .GetMethod("Clear")!
            .Invoke(_cache.GetType().GetField("_l1Cache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(_cache), null);

        // Act - Warm cache
        var warmed = await _cache.WarmGameCacheAsync(_testGameId, topN: 20);

        // Assert
        warmed.Should().BeGreaterThan(0, "should have warmed some entries");
        _mockFrequencyTracker.Verify(
            x => x.GetTopQueriesAsync(_testGameId, 20),
            Times.Once);
    }

    private class TestCacheItem
    {
        public string Data { get; set; } = string.Empty;
    }
}
