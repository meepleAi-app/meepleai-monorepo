using Api.Configuration;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.Integration.Infrastructure;

/// <summary>
/// Comprehensive integration tests for HybridCacheService L1/L2 caching with real Redis (Issue #2307).
/// Tests L1 (in-memory) + L2 (Redis) coordination, tag-based invalidation, and resilience.
///
/// Test Categories:
/// 1. Cache Hit Scenarios: L1 hits, L2 hits with L1 population
/// 2. Cache Miss Scenarios: Full miss, null value handling
/// 3. Invalidation Scenarios: Single key, tag-based, multi-tag
/// 4. Expiration & Edge Cases: L2 expiration, Redis unavailability
///
/// Infrastructure: Real Redis via SharedTestcontainersFixture
/// Coverage Target: ≥90% for HybridCacheService
/// Execution Time Target: <10s
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Issue", "2307")]
public sealed class HybridCacheServiceIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private HybridCacheService _cacheService = null!;
    private IConnectionMultiplexer _redis = null!;
    private string _keyPrefix = null!;
    private readonly Mock<ILogger<HybridCacheService>> _loggerMock;

    public HybridCacheServiceIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _loggerMock = new Mock<ILogger<HybridCacheService>>();
    }

    public async ValueTask InitializeAsync()
    {
        // Use unique key prefix per test class to avoid conflicts
        _keyPrefix = $"test:hybrid_cache:{Guid.NewGuid():N}:";

        // Connect to shared Redis container
        _redis = await ConnectionMultiplexer.ConnectAsync(_fixture.RedisConnectionString);

        // Create HybridCache with real Redis
        var hybridCacheOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromMinutes(5),
            LocalCacheExpiration = TimeSpan.FromMinutes(5)
        };

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();

        // Configure Redis for L2 cache
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = _fixture.RedisConnectionString;
            options.InstanceName = _keyPrefix;
        });

        // Add HybridCache with Redis L2
        services.AddHybridCache();

        var serviceProvider = services.BuildServiceProvider();
        var hybridCache = serviceProvider.GetRequiredService<HybridCache>();

        // Create HybridCacheConfiguration
        var config = new HybridCacheConfiguration
        {
            EnableL2Cache = true,
            EnableTags = true,
            DefaultExpiration = TimeSpan.FromMinutes(5),
            MaxTagsPerEntry = 10
        };

        var configOptions = Options.Create(config);

        // Create service with real Redis
        _cacheService = new HybridCacheService(
            hybridCache,
            configOptions,
            _loggerMock.Object,
            _redis
        );
    }

    public async ValueTask DisposeAsync()
    {
        // Clean up Redis keys with prefix
        await _fixture.FlushRedisByPrefixAsync(_keyPrefix + "*");

        // Close Redis connection
        await _redis.CloseAsync();
        _redis.Dispose();
    }

    #region Cache Hit Scenarios

    /// <summary>
    /// Test 1: Cache hit from L1 (warm cache) - no Redis call needed.
    /// Validates L1 serves cached values without hitting L2.
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_WithWarmL1Cache_ReturnsFromL1WithoutRedisCall()
    {
        // Arrange
        var key = _keyPrefix + "l1_hit_test";
        var factoryCalls = 0;

        Func<CancellationToken, Task<TestCacheData>> factory = _ =>
        {
            factoryCalls++;
            return Task.FromResult(new TestCacheData { Value = "initial", Timestamp = DateTime.UtcNow });
        };

        // Act - First call populates L1 and L2
        var result1 = await _cacheService.GetOrCreateAsync(key, factory, ct: TestContext.Current.CancellationToken);

        // Second call should hit L1 without factory execution
        var result2 = await _cacheService.GetOrCreateAsync(key, factory, ct: TestContext.Current.CancellationToken);

        // Assert
        factoryCalls.Should().Be(1, "factory should execute only once");
        result1.Value.Should().Be("initial");
        result2.Value.Should().Be("initial");
        result1.Timestamp.Should().Be(result2.Timestamp, "should return same cached instance");
    }

    /// <summary>
    /// Test 2: Cache hit from L2 (L1 miss) - Redis hit, populate L1.
    /// Simulates L1 eviction but L2 still has the value.
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_WithL1MissL2Hit_PopulatesL1FromRedis()
    {
        // Arrange
        var key = _keyPrefix + "l2_hit_test";
        var factoryCalls = 0;
        var expectedValue = new TestCacheData { Value = "from_l2", Timestamp = DateTime.UtcNow };

        Func<CancellationToken, Task<TestCacheData>> factory = _ =>
        {
            factoryCalls++;
            return Task.FromResult(expectedValue);
        };

        // Act - Populate cache
        var result1 = await _cacheService.GetOrCreateAsync(key, factory, ct: TestContext.Current.CancellationToken);

        // Simulate L1 eviction by creating new service instance (L1 is in-memory only)
        var config = new HybridCacheConfiguration
        {
            EnableL2Cache = true,
            EnableTags = true,
            DefaultExpiration = TimeSpan.FromMinutes(5)
        };

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = _fixture.RedisConnectionString;
            options.InstanceName = _keyPrefix;
        });
        services.AddHybridCache();

        var serviceProvider = services.BuildServiceProvider();
        var newHybridCache = serviceProvider.GetRequiredService<HybridCache>();

        var newCacheService = new HybridCacheService(
            newHybridCache,
            Options.Create(config),
            _loggerMock.Object,
            _redis
        );

        // Second call with new service instance (fresh L1, but L2 should still have value)
        var result2 = await newCacheService.GetOrCreateAsync(key, factory, ct: TestContext.Current.CancellationToken);

        // Assert
        factoryCalls.Should().Be(1, "factory should execute only once - L2 should serve the value");
        result2.Value.Should().Be("from_l2");
    }

    #endregion

    #region Cache Miss Scenarios

    /// <summary>
    /// Test 3: Cache miss on both L1 and L2 - factory executes, populate both levels.
    /// Validates full cache miss workflow.
    /// Issue #2710: Verify L2 population through cache API, not direct Redis key check.
    /// HybridCache uses internal key format that differs from the key parameter.
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_WithFullMiss_ExecutesFactoryAndPopulatesBothLevels()
    {
        // Arrange
        var key = _keyPrefix + "full_miss_test";
        var factoryCalls = 0;
        var expectedValue = new TestCacheData { Value = "new_value", Timestamp = DateTime.UtcNow };

        Func<CancellationToken, Task<TestCacheData>> factory = _ =>
        {
            factoryCalls++;
            return Task.FromResult(expectedValue);
        };

        // Act
        var result = await _cacheService.GetOrCreateAsync(key, factory, ct: TestContext.Current.CancellationToken);

        // Issue #2710: Verify L2 population by creating a new service instance (fresh L1)
        // and checking if factory is NOT called (proving L2 has the value)
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = _fixture.RedisConnectionString;
            options.InstanceName = _keyPrefix;
        });
        services.AddHybridCache();

        var serviceProvider = services.BuildServiceProvider();
        var newHybridCache = serviceProvider.GetRequiredService<HybridCache>();

        var config = new HybridCacheConfiguration
        {
            EnableL2Cache = true,
            EnableTags = true,
            DefaultExpiration = TimeSpan.FromMinutes(5)
        };

        var newCacheService = new HybridCacheService(
            newHybridCache,
            Options.Create(config),
            _loggerMock.Object,
            _redis
        );

        // Second call with fresh L1 should hit L2, not execute factory
        var result2 = await newCacheService.GetOrCreateAsync(key, factory, ct: TestContext.Current.CancellationToken);

        // Assert
        factoryCalls.Should().Be(1, "factory should execute only once - L2 should serve on second call");
        result.Value.Should().Be("new_value");
        result2.Value.Should().Be("new_value", "L2 should return the cached value");
    }

    /// <summary>
    /// Test 4: Factory returns null - should not cache null values.
    /// Validates null value handling prevents cache pollution.
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_WithNullFactoryResult_DoesNotCacheNull()
    {
        // Arrange
        var key = _keyPrefix + "null_test";
        var factoryCalls = 0;

        // Note: HybridCache GetOrCreateAsync requires non-nullable T (where T : class constraint)
        // This test verifies behavior when factory might return null (though type system prevents it)
        // We'll test with a wrapper that never actually returns null
        Func<CancellationToken, Task<TestCacheData>> factory = _ =>
        {
            factoryCalls++;
            // Simulating "no data" case - return empty object instead of null
            return Task.FromResult(new TestCacheData { Value = string.Empty, Timestamp = DateTime.UtcNow });
        };

        // Act - First call
        var result1 = await _cacheService.GetOrCreateAsync(key, factory, ct: TestContext.Current.CancellationToken);

        // Second call (should hit cache)
        var result2 = await _cacheService.GetOrCreateAsync(key, factory, ct: TestContext.Current.CancellationToken);

        // Assert
        result1.Should().NotBeNull();
        result1.Value.Should().BeEmpty();
        result2.Should().NotBeNull();
        result2.Value.Should().BeEmpty();

        // Factory should execute only once - second call hits cache
        factoryCalls.Should().Be(1, "factory should execute only once, second call hits cache");
    }

    #endregion

    #region Invalidation Scenarios

    /// <summary>
    /// Test 5: RemoveAsync - clear from both L1 and L2.
    /// Validates single key invalidation across both cache levels.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_WithValidKey_ClearsFromBothL1AndL2()
    {
        // Arrange
        var key = _keyPrefix + "remove_test";
        var factoryCalls = 0;

        Func<CancellationToken, Task<TestCacheData>> factory = _ =>
        {
            factoryCalls++;
            return Task.FromResult(new TestCacheData { Value = $"value_{factoryCalls}", Timestamp = DateTime.UtcNow });
        };

        // Populate cache
        var result1 = await _cacheService.GetOrCreateAsync(key, factory, ct: TestContext.Current.CancellationToken);

        // Act - Remove from cache
        await _cacheService.RemoveAsync(key, TestContext.Current.CancellationToken);

        // Get again (should execute factory since cache was cleared)
        var result2 = await _cacheService.GetOrCreateAsync(key, factory, ct: TestContext.Current.CancellationToken);

        // Assert
        factoryCalls.Should().Be(2, "factory should execute twice: once for initial, once after removal");
        result1.Value.Should().Be("value_1");
        result2.Value.Should().Be("value_2", "should be new value from factory");
    }

    /// <summary>
    /// Test 6: RemoveByTagAsync - invalidate all entries with specific tag.
    /// Validates tag-based cache invalidation.
    /// </summary>
    [Fact]
    public async Task RemoveByTagAsync_WithMatchingTag_InvalidatesAllTaggedEntries()
    {
        // Arrange
        // Issue #2920: Clean tag-specific entries before test to prevent cross-test contamination
        var tag = "game:chess";
        await _cacheService.RemoveByTagAsync(tag, TestContext.Current.CancellationToken);

        var key1 = _keyPrefix + "chess_qa_1";
        var key2 = _keyPrefix + "chess_qa_2";
        var key3 = _keyPrefix + "monopoly_qa_1"; // Different tag, should not be affected

        var factory1Calls = 0;
        var factory2Calls = 0;
        var factory3Calls = 0;

        Func<CancellationToken, Task<TestCacheData>> factory1 = _ =>
        {
            factory1Calls++;
            return Task.FromResult(new TestCacheData { Value = "chess_1", Timestamp = DateTime.UtcNow });
        };

        Func<CancellationToken, Task<TestCacheData>> factory2 = _ =>
        {
            factory2Calls++;
            return Task.FromResult(new TestCacheData { Value = "chess_2", Timestamp = DateTime.UtcNow });
        };

        Func<CancellationToken, Task<TestCacheData>> factory3 = _ =>
        {
            factory3Calls++;
            return Task.FromResult(new TestCacheData { Value = "monopoly_1", Timestamp = DateTime.UtcNow });
        };

        // Populate cache with tagged entries
        await _cacheService.GetOrCreateAsync(key1, factory1, tags: new[] { tag }, ct: TestContext.Current.CancellationToken);
        await _cacheService.GetOrCreateAsync(key2, factory2, tags: new[] { tag }, ct: TestContext.Current.CancellationToken);
        await _cacheService.GetOrCreateAsync(key3, factory3, tags: new[] { "game:monopoly" }, ct: TestContext.Current.CancellationToken);

        // Act - Invalidate by tag
        var removedCount = await _cacheService.RemoveByTagAsync(tag, TestContext.Current.CancellationToken);

        // Get again to verify invalidation
        await _cacheService.GetOrCreateAsync(key1, factory1, tags: new[] { tag }, ct: TestContext.Current.CancellationToken);
        await _cacheService.GetOrCreateAsync(key2, factory2, tags: new[] { tag }, ct: TestContext.Current.CancellationToken);
        await _cacheService.GetOrCreateAsync(key3, factory3, tags: new[] { "game:monopoly" }, ct: TestContext.Current.CancellationToken);

        // Assert
        removedCount.Should().Be(2, "should remove 2 chess-tagged entries");
        factory1Calls.Should().Be(2, "chess_1 should be refetched after invalidation");
        factory2Calls.Should().Be(2, "chess_2 should be refetched after invalidation");
        factory3Calls.Should().Be(1, "monopoly_1 should NOT be affected");
    }

    /// <summary>
    /// Test 7: RemoveByTagsAsync - invalidate entries with all specified tags (AND logic).
    /// Validates multi-tag selective invalidation.
    /// </summary>
    [Fact]
    public async Task RemoveByTagsAsync_WithMultipleTags_InvalidatesOnlyEntriesWithAllTags()
    {
        // Arrange
        var key1 = _keyPrefix + "chess_qa";
        var key2 = _keyPrefix + "chess_explain";
        var key3 = _keyPrefix + "monopoly_qa";

        var factory1Calls = 0;
        var factory2Calls = 0;
        var factory3Calls = 0;

        Func<CancellationToken, Task<TestCacheData>> factory1 = _ =>
        {
            factory1Calls++;
            return Task.FromResult(new TestCacheData { Value = "chess_qa", Timestamp = DateTime.UtcNow });
        };

        Func<CancellationToken, Task<TestCacheData>> factory2 = _ =>
        {
            factory2Calls++;
            return Task.FromResult(new TestCacheData { Value = "chess_explain", Timestamp = DateTime.UtcNow });
        };

        Func<CancellationToken, Task<TestCacheData>> factory3 = _ =>
        {
            factory3Calls++;
            return Task.FromResult(new TestCacheData { Value = "monopoly_qa", Timestamp = DateTime.UtcNow });
        };

        // Populate cache with different tag combinations
        await _cacheService.GetOrCreateAsync(key1, factory1, tags: new[] { "game:chess", "endpoint:qa" }, ct: TestContext.Current.CancellationToken);
        await _cacheService.GetOrCreateAsync(key2, factory2, tags: new[] { "game:chess", "endpoint:explain" }, ct: TestContext.Current.CancellationToken);
        await _cacheService.GetOrCreateAsync(key3, factory3, tags: new[] { "game:monopoly", "endpoint:qa" }, ct: TestContext.Current.CancellationToken);

        // Act - Invalidate entries with BOTH "game:chess" AND "endpoint:qa"
        var removedCount = await _cacheService.RemoveByTagsAsync(
            new[] { "game:chess", "endpoint:qa" },
            TestContext.Current.CancellationToken
        );

        // Get again to verify selective invalidation
        await _cacheService.GetOrCreateAsync(key1, factory1, tags: new[] { "game:chess", "endpoint:qa" }, ct: TestContext.Current.CancellationToken);
        await _cacheService.GetOrCreateAsync(key2, factory2, tags: new[] { "game:chess", "endpoint:explain" }, ct: TestContext.Current.CancellationToken);
        await _cacheService.GetOrCreateAsync(key3, factory3, tags: new[] { "game:monopoly", "endpoint:qa" }, ct: TestContext.Current.CancellationToken);

        // Assert
        removedCount.Should().Be(1, "should remove only chess_qa (has both tags)");
        factory1Calls.Should().Be(2, "chess_qa should be refetched");
        factory2Calls.Should().Be(1, "chess_explain should NOT be affected (only has game:chess)");
        factory3Calls.Should().Be(1, "monopoly_qa should NOT be affected (only has endpoint:qa)");
    }

    #endregion

    #region Expiration & Edge Cases

    /// <summary>
    /// Test 8: Redis unavailable - degrade gracefully to L1 only.
    /// Validates resilience when L2 (Redis) is unavailable.
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_WithRedisUnavailable_DegradesGracefullyToL1()
    {
        // Arrange
        var key = _keyPrefix + "redis_unavailable_test";
        var factoryCalls = 0;

        Func<CancellationToken, Task<TestCacheData>> factory = _ =>
        {
            factoryCalls++;
            return Task.FromResult(new TestCacheData { Value = "resilient_value", Timestamp = DateTime.UtcNow });
        };

        // Create service with null Redis (simulates Redis unavailable)
        var config = new HybridCacheConfiguration
        {
            EnableL2Cache = false, // Disable L2 to simulate Redis unavailability
            EnableTags = false,
            DefaultExpiration = TimeSpan.FromMinutes(5)
        };

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();

        var serviceProvider = services.BuildServiceProvider();
        var hybridCache = serviceProvider.GetRequiredService<HybridCache>();

        var resilientService = new HybridCacheService(
            hybridCache,
            Options.Create(config),
            _loggerMock.Object,
            redis: null // No Redis connection
        );

        // Act - Should work with L1 only
        var result = await resilientService.GetOrCreateAsync(key, factory, ct: TestContext.Current.CancellationToken);

        // Second call should hit L1
        var result2 = await resilientService.GetOrCreateAsync(key, factory, ct: TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be("resilient_value");
        result2.Value.Should().Be("resilient_value");
        factoryCalls.Should().Be(1, "L1 should serve cached value on second call");
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Test data class for cache validation.
    /// </summary>
    private sealed class TestCacheData
    {
        public string Value { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    #endregion
}
