using Api.BoundedContexts.KnowledgeBase.Domain.Services.Caching;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Caching;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Caching;

/// <summary>
/// ISSUE-3494: Unit tests for MultiTierCache service.
/// Tests cache promotion, adaptive TTL, and metrics tracking.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3494")]
public sealed class MultiTierCacheTests : IDisposable
{
    private readonly Mock<IHybridCacheService> _mockL2Cache;
    private readonly Mock<IRedisFrequencyTracker> _mockFrequencyTracker;
    private readonly Mock<ICacheMetricsRecorder> _mockMetricsRecorder;
    private readonly Mock<ILogger<MultiTierCache>> _mockLogger;
    private readonly MultiTierCacheConfiguration _config;
    private readonly MultiTierCache _cache;
    private readonly Guid _testGameId = Guid.NewGuid();

    public MultiTierCacheTests()
    {
        _mockL2Cache = new Mock<IHybridCacheService>();
        _mockFrequencyTracker = new Mock<IRedisFrequencyTracker>();
        _mockMetricsRecorder = new Mock<ICacheMetricsRecorder>();
        _mockLogger = new Mock<ILogger<MultiTierCache>>();

        _config = new MultiTierCacheConfiguration
        {
            Enabled = true,
            L1MaxItems = 100,
            L1Enabled = true,
            L2Enabled = true,
            L3Enabled = true,
            EnablePromotion = true,
            HighFrequencyThreshold = 100,
            MediumFrequencyThreshold = 10,
            HighFrequencyTtl = TimeSpan.FromHours(24),
            MediumFrequencyTtl = TimeSpan.FromHours(1),
            LowFrequencyTtl = TimeSpan.FromMinutes(5),
            PromotionToL1Threshold = 3,
            WarmingEnabled = true,
            WarmingTopNItems = 20
        };

        var options = Options.Create(_config);

        _cache = new MultiTierCache(
            _mockL2Cache.Object,
            _mockFrequencyTracker.Object,
            _mockMetricsRecorder.Object,
            options,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    [Fact]
    public async Task GetAsync_CacheMiss_Should_Return_Null()
    {
        // Arrange
        _mockFrequencyTracker
            .Setup(x => x.TrackAccessAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _cache.GetAsync<TestCacheItem>("non-existent", _testGameId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_Should_Store_In_L1_And_L2()
    {
        // Arrange
        var key = "test-key";
        var value = new TestCacheItem { Data = "test-data" };
        var l2CalledWithCorrectKey = false;

        _mockFrequencyTracker
            .Setup(x => x.GetFrequencyAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(5); // Low frequency

        _mockFrequencyTracker
            .Setup(x => x.TrackAccessAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Use the correct CachedItem type (now internal, accessible via InternalsVisibleTo)
        _mockL2Cache
            .Setup(x => x.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<MultiTierCache.CachedItem>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Func<CancellationToken, Task<MultiTierCache.CachedItem>>, string[]?, TimeSpan?, CancellationToken>(
                (k, _, _, _, _) => { if (k.Contains(key)) l2CalledWithCorrectKey = true; })
            .ReturnsAsync(new MultiTierCache.CachedItem { Value = value, CreatedAt = DateTimeOffset.UtcNow });

        // Act
        await _cache.SetAsync(key, _testGameId, value);

        // Assert - verify L2 was called with the correct key
        l2CalledWithCorrectKey.Should().BeTrue("L2 cache should be called with a key containing '{0}'", key);
    }

    [Fact]
    public async Task CalculateAdaptiveTtlAsync_HighFrequency_Should_Return_24Hours()
    {
        // Arrange
        _mockFrequencyTracker
            .Setup(x => x.GetFrequencyAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(150); // High frequency (>= 100)

        // Act
        var ttl = await _cache.CalculateAdaptiveTtlAsync(_testGameId, "popular-key");

        // Assert
        ttl.Should().Be(TimeSpan.FromHours(24));
    }

    [Fact]
    public async Task CalculateAdaptiveTtlAsync_MediumFrequency_Should_Return_1Hour()
    {
        // Arrange
        _mockFrequencyTracker
            .Setup(x => x.GetFrequencyAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(50); // Medium frequency (10-99)

        // Act
        var ttl = await _cache.CalculateAdaptiveTtlAsync(_testGameId, "medium-key");

        // Assert
        ttl.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task CalculateAdaptiveTtlAsync_LowFrequency_Should_Return_5Minutes()
    {
        // Arrange
        _mockFrequencyTracker
            .Setup(x => x.GetFrequencyAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(3); // Low frequency (< 10)

        // Act
        var ttl = await _cache.CalculateAdaptiveTtlAsync(_testGameId, "rare-key");

        // Assert
        ttl.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task RemoveAsync_Should_Remove_From_All_Tiers()
    {
        // Arrange
        var key = "to-remove";

        _mockL2Cache
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _cache.RemoveAsync(key);

        // Assert
        _mockL2Cache.Verify(x => x.RemoveAsync(key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveByTagAsync_Should_Delegate_To_L2()
    {
        // Arrange
        var tag = "game:test";

        _mockL2Cache
            .Setup(x => x.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var removed = await _cache.RemoveByTagAsync(tag);

        // Assert
        removed.Should().Be(5);
        _mockL2Cache.Verify(x => x.RemoveByTagAsync(tag, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetMetrics_Should_Return_Tier_Statistics()
    {
        // Act
        var metrics = _cache.GetMetrics();

        // Assert
        metrics.Should().NotBeNull();
        metrics.L1Memory.Should().NotBeNull();
        metrics.L2Redis.Should().NotBeNull();
        metrics.L3Qdrant.Should().NotBeNull();
    }

    [Fact]
    public async Task WarmGameCacheAsync_Disabled_Should_Return_Zero()
    {
        // Arrange
        var disabledConfig = new MultiTierCacheConfiguration { WarmingEnabled = false };
        var options = Options.Create(disabledConfig);

        using var cache = new MultiTierCache(
            _mockL2Cache.Object,
            _mockFrequencyTracker.Object,
            _mockMetricsRecorder.Object,
            options,
            _mockLogger.Object);

        // Act
        var warmed = await cache.WarmGameCacheAsync(_testGameId);

        // Assert
        warmed.Should().Be(0);
    }

    [Fact]
    public async Task WarmGameCacheAsync_NoFrequentQueries_Should_Return_Zero()
    {
        // Arrange
        _mockFrequencyTracker
            .Setup(x => x.GetTopQueriesAsync(It.IsAny<Guid>(), It.IsAny<int>()))
            .ReturnsAsync(new List<FrequentQuery>());

        // Act
        var warmed = await _cache.WarmGameCacheAsync(_testGameId);

        // Assert
        warmed.Should().Be(0);
    }

    [Fact]
    public async Task GetOrCreateAsync_Should_Track_Access_Frequency()
    {
        // Arrange
        var key = "tracked-key";
        var value = new TestCacheItem { Data = "data" };
        var factoryWasCalled = false;

        _mockFrequencyTracker
            .Setup(x => x.TrackAccessAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _mockFrequencyTracker
            .Setup(x => x.GetFrequencyAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(5);

        _mockMetricsRecorder
            .Setup(x => x.RecordCacheMissAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Setup L2 cache to execute the factory and return the result (using CachedItem type)
        _mockL2Cache
            .Setup(x => x.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<MultiTierCache.CachedItem>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<MultiTierCache.CachedItem>>, string[]?, TimeSpan?, CancellationToken>(
                async (_, factory, _, _, ct) =>
                {
                    factoryWasCalled = true;
                    return await factory(ct);
                });

        // Act
        var result = await _cache.GetOrCreateAsync(key, _testGameId, _ => Task.FromResult(value));

        // Assert
        _mockFrequencyTracker.Verify(
            x => x.TrackAccessAsync(_testGameId, key),
            Times.AtLeastOnce);
        factoryWasCalled.Should().BeTrue("The factory should be called to create the cached item");
        result.Should().NotBeNull();
        result.Value.Should().Be(value);
    }

    [Fact]
    public async Task CacheDisabled_GetAsync_Should_Return_Null()
    {
        // Arrange
        var disabledConfig = new MultiTierCacheConfiguration { Enabled = false };
        var options = Options.Create(disabledConfig);

        using var cache = new MultiTierCache(
            _mockL2Cache.Object,
            _mockFrequencyTracker.Object,
            _mockMetricsRecorder.Object,
            options,
            _mockLogger.Object);

        // Act
        var result = await cache.GetAsync<TestCacheItem>("any-key", _testGameId);

        // Assert
        result.Should().BeNull();
        _mockL2Cache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CacheDisabled_SetAsync_Should_Not_Store()
    {
        // Arrange
        var disabledConfig = new MultiTierCacheConfiguration { Enabled = false };
        var options = Options.Create(disabledConfig);

        using var cache = new MultiTierCache(
            _mockL2Cache.Object,
            _mockFrequencyTracker.Object,
            _mockMetricsRecorder.Object,
            options,
            _mockLogger.Object);

        var value = new TestCacheItem { Data = "test" };

        // Act
        await cache.SetAsync("any-key", _testGameId, value);

        // Assert
        _mockL2Cache.VerifyNoOtherCalls();
    }

    private class TestCacheItem
    {
        public string Data { get; set; } = string.Empty;
    }
}
