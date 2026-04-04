using Api.BoundedContexts.KnowledgeBase.Domain.Services.Caching;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Caching;
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
using System.Diagnostics;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Caching;

/// <summary>
/// ISSUE-3494: Performance tests for MultiTierCache.
/// Verifies latency targets: L1 &lt;1ms, L2 &lt;10ms, P95 &lt;100ms for cached reads.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Performance)]
[Trait("Issue", "3494")]
public sealed class MultiTierCachePerformanceTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly Mock<IRedisFrequencyTracker> _mockFrequencyTracker;
    private readonly Mock<ICacheMetricsRecorder> _mockMetricsRecorder;
    private readonly Mock<ILogger<MultiTierCache>> _mockLogger;
    private MultiTierCache _cache = null!;
    private IConnectionMultiplexer _redis = null!;
    private string _keyPrefix = null!;
    private readonly Guid _testGameId = Guid.NewGuid();

    public MultiTierCachePerformanceTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _mockFrequencyTracker = new Mock<IRedisFrequencyTracker>();
        _mockMetricsRecorder = new Mock<ICacheMetricsRecorder>();
        _mockLogger = new Mock<ILogger<MultiTierCache>>();
    }

    public async ValueTask InitializeAsync()
    {
        _keyPrefix = $"test:perf:{Guid.NewGuid():N}:";
        _redis = await ConnectionMultiplexer.ConnectAsync(_fixture.RedisConnectionString);

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

        var config = new MultiTierCacheConfiguration
        {
            Enabled = true,
            L1MaxItems = 1000,
            L1Enabled = true,
            L2Enabled = true,
            EnablePromotion = true,
            KeyPrefix = _keyPrefix
        };

        _mockFrequencyTracker
            .Setup(x => x.TrackAccessAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _mockFrequencyTracker
            .Setup(x => x.GetFrequencyAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(5);

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
        await _fixture.FlushRedisByPrefixAsync(_keyPrefix + "*");
        _cache.Dispose();
        await _redis.CloseAsync();
        _redis.Dispose();
    }

    [Fact]
    public async Task L1CacheHit_ShouldBeFasterThan1Millisecond()
    {
        // Arrange - Populate cache
        var key = "perf-l1-test";
        var value = new TestCacheItem { Data = "performance-test-data" };
        await _cache.SetAsync(key, _testGameId, value);

        // Warm up (exclude JIT compilation from measurement)
        await _cache.GetAsync<TestCacheItem>(key, _testGameId);

        // Act - Measure L1 cache hit latency (100 iterations for reliability)
        var latencies = new List<double>();
        for (int i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();
            var result = await _cache.GetAsync<TestCacheItem>(key, _testGameId);
            sw.Stop();

            result.Should().NotBeNull();
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Assert - P95 should be < 1ms for L1 cache hits
        var p95 = latencies.OrderBy(x => x).ElementAt((int)(latencies.Count * 0.95));
        var average = latencies.Average();

        p95.Should().BeLessThan(1.0, "P95 latency for L1 cache hits should be under 1ms");
        average.Should().BeLessThan(0.5, "average L1 hit latency should be under 0.5ms");
    }

    [Fact]
    public async Task L2CacheHit_ShouldBeFasterThan10Milliseconds()
    {
        // Arrange - Populate L2 cache
        var key = "perf-l2-test";
        var value = new TestCacheItem { Data = "l2-performance-data" };
        await _cache.SetAsync(key, _testGameId, value);

        // Clear L1 to force L2 hits
        var l1Cache = _cache.GetType()
            .GetField("_l1Cache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(_cache);

        l1Cache!.GetType().GetMethod("Clear")!.Invoke(l1Cache, null);

        // Warm up
        await _cache.GetAsync<TestCacheItem>(key, _testGameId);

        // Act - Measure L2 cache hit latency (50 iterations)
        var latencies = new List<double>();
        for (int i = 0; i < 50; i++)
        {
            // Clear L1 before each iteration to ensure L2 hit
            l1Cache.GetType().GetMethod("Clear")!.Invoke(l1Cache, null);

            var sw = Stopwatch.StartNew();
            var result = await _cache.GetAsync<TestCacheItem>(key, _testGameId);
            sw.Stop();

            result.Should().NotBeNull();
            result!.SourceTier.Should().Be(CacheTier.L2Redis);
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Assert - P95 should be < 10ms for L2 cache hits (Redis network latency)
        var p95 = latencies.OrderBy(x => x).ElementAt((int)(latencies.Count * 0.95));
        var average = latencies.Average();

        p95.Should().BeLessThan(10.0, "P95 latency for L2 cache hits should be under 10ms");
        average.Should().BeLessThan(5.0, "average L2 hit latency should be under 5ms");
    }

    [Fact]
    public async Task CachedReadWorkload_P95_ShouldBeFasterThan100Milliseconds()
    {
        // Arrange - Simulate realistic workload: 80% cache hits, 20% misses
        var keys = Enumerable.Range(0, 100).Select(i => $"workload-key-{i}").ToList();
        var latencies = new List<double>();

        // Pre-populate 80% of keys
        for (int i = 0; i < 80; i++)
        {
            await _cache.SetAsync(keys[i], _testGameId, new TestCacheItem { Data = $"data-{i}" });
        }

        // Act - Simulate 1000 requests with 80/20 hit/miss ratio
        for (int iteration = 0; iteration < 1000; iteration++)
        {
            var keyIndex = iteration % 100;
            var key = keys[keyIndex];

            var sw = Stopwatch.StartNew();

            if (keyIndex < 80)
            {
                // Cache hit
                var result = await _cache.GetAsync<TestCacheItem>(key, _testGameId);
                result.Should().NotBeNull("pre-populated key should hit cache");
            }
            else
            {
                // Cache miss - create
                await _cache.GetOrCreateAsync(key, _testGameId, _ =>
                    Task.FromResult(new TestCacheItem { Data = $"new-data-{keyIndex}" }));
            }

            sw.Stop();
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Assert - Overall P95 should be < 100ms as per DoD
        var p50 = latencies.OrderBy(x => x).ElementAt((int)(latencies.Count * 0.50));
        var p95 = latencies.OrderBy(x => x).ElementAt((int)(latencies.Count * 0.95));
        var p99 = latencies.OrderBy(x => x).ElementAt((int)(latencies.Count * 0.99));
        var average = latencies.Average();

        p95.Should().BeLessThan(100.0, "P95 latency for cached reads should be under 100ms (DoD requirement)");
        p50.Should().BeLessThan(10.0, "P50 latency should be under 10ms for typical workload");
        average.Should().BeLessThan(15.0, "average latency should be reasonable");

        // Log performance stats for monitoring
        Console.WriteLine($"Performance Stats: Avg={average:F2}ms, P50={p50:F2}ms, P95={p95:F2}ms, P99={p99:F2}ms");
    }

    private class TestCacheItem
    {
        public string Data { get; set; } = string.Empty;
    }
}
