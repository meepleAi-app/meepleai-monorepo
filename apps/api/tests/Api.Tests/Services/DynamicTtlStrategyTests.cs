using System;
using System.Threading.Tasks;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MeepleAI.Api.Tests.Services;

/// <summary>
/// BDD-style unit tests for DynamicTtlStrategy service.
/// Tests TTL calculation based on query frequency (hot/warm/cold classification).
/// </summary>
public class DynamicTtlStrategyTests
{
    private readonly Mock<ILogger<DynamicTtlStrategy>> _mockLogger;
    private readonly Mock<IOptions<CacheOptimizationConfiguration>> _mockConfig;

    public DynamicTtlStrategyTests()
    {
        _mockLogger = new Mock<ILogger<DynamicTtlStrategy>>();
        _mockConfig = new Mock<IOptions<CacheOptimizationConfiguration>>();
    }

    [Fact]
    public async Task CalculateTtlAsync_HotQuery_Returns24Hours()
    {
        // Arrange (Given): Query has 15 cache hits (>10 threshold)
        _mockConfig.Setup(c => c.Value).Returns(new CacheOptimizationConfiguration
        {
            HotQueryThreshold = 10,
            WarmQueryThreshold = 3,
            HotQueryTtlHours = 24,
            WarmQueryTtlHours = 6,
            ColdQueryTtlHours = 1
        });

        var strategy = new DynamicTtlStrategy(_mockLogger.Object, _mockConfig.Object);
        var hitCount = 15; // Hot query (>10)

        // Act (When): Calculate TTL
        var ttl = await strategy.CalculateTtlAsync(hitCount);

        // Assert (Then): TTL = 24 hours
        Assert.Equal(TimeSpan.FromHours(24), ttl);
    }

    [Fact]
    public async Task CalculateTtlAsync_WarmQuery_Returns6Hours()
    {
        // Arrange (Given): Query has 5 cache hits (3-10 range)
        _mockConfig.Setup(c => c.Value).Returns(new CacheOptimizationConfiguration
        {
            HotQueryThreshold = 10,
            WarmQueryThreshold = 3,
            HotQueryTtlHours = 24,
            WarmQueryTtlHours = 6,
            ColdQueryTtlHours = 1
        });

        var strategy = new DynamicTtlStrategy(_mockLogger.Object, _mockConfig.Object);
        var hitCount = 5; // Warm query (3-10)

        // Act (When): Calculate TTL
        var ttl = await strategy.CalculateTtlAsync(hitCount);

        // Assert (Then): TTL = 6 hours
        Assert.Equal(TimeSpan.FromHours(6), ttl);
    }

    [Fact]
    public async Task CalculateTtlAsync_ColdQuery_Returns1Hour()
    {
        // Arrange (Given): Query has 1 cache hit
        _mockConfig.Setup(c => c.Value).Returns(new CacheOptimizationConfiguration
        {
            HotQueryThreshold = 10,
            WarmQueryThreshold = 3,
            HotQueryTtlHours = 24,
            WarmQueryTtlHours = 6,
            ColdQueryTtlHours = 1
        });

        var strategy = new DynamicTtlStrategy(_mockLogger.Object, _mockConfig.Object);
        var hitCount = 1; // Cold query (<3)

        // Act (When): Calculate TTL
        var ttl = await strategy.CalculateTtlAsync(hitCount);

        // Assert (Then): TTL = 1 hour
        Assert.Equal(TimeSpan.FromHours(1), ttl);
    }

    [Fact]
    public async Task CalculateTtlAsync_FirstTimeQuery_DefaultsToCold()
    {
        // Arrange (Given): Query has 0 cache hits
        _mockConfig.Setup(c => c.Value).Returns(new CacheOptimizationConfiguration
        {
            HotQueryThreshold = 10,
            WarmQueryThreshold = 3,
            HotQueryTtlHours = 24,
            WarmQueryTtlHours = 6,
            ColdQueryTtlHours = 1
        });

        var strategy = new DynamicTtlStrategy(_mockLogger.Object, _mockConfig.Object);
        var hitCount = 0; // First-time query

        // Act (When): Calculate TTL
        var ttl = await strategy.CalculateTtlAsync(hitCount);

        // Assert (Then): TTL = 1 hour (cold default)
        Assert.Equal(TimeSpan.FromHours(1), ttl);
    }

    [Fact]
    public async Task CalculateTtlAsync_CustomThresholds_ClassifiesCorrectly()
    {
        // Arrange (Given): Config defines HotThreshold=20, WarmThreshold=5
        _mockConfig.Setup(c => c.Value).Returns(new CacheOptimizationConfiguration
        {
            HotQueryThreshold = 20,
            WarmQueryThreshold = 5,
            HotQueryTtlHours = 48,
            WarmQueryTtlHours = 12,
            ColdQueryTtlHours = 2
        });

        var strategy = new DynamicTtlStrategy(_mockLogger.Object, _mockConfig.Object);
        var hitCount = 7; // Warm query (5-20)

        // Act (When): Calculate TTL with custom config
        var ttl = await strategy.CalculateTtlAsync(hitCount);

        // Assert (Then): Classified as warm, TTL = 12 hours
        Assert.Equal(TimeSpan.FromHours(12), ttl);
    }

    [Theory]
    [InlineData(10, 24)] // Exactly hot threshold -> hot (boundary inclusive)
    [InlineData(3, 6)]   // Exactly warm threshold -> warm (boundary inclusive)
    [InlineData(2, 1)]   // Just below warm threshold -> cold
    [InlineData(11, 24)] // Just above hot threshold -> hot
    public async Task CalculateTtlAsync_BoundaryConditions_ClassifiesCorrectly(int hitCount, int expectedHours)
    {
        // Arrange (Given): Standard thresholds (hot=10, warm=3)
        _mockConfig.Setup(c => c.Value).Returns(new CacheOptimizationConfiguration
        {
            HotQueryThreshold = 10,
            WarmQueryThreshold = 3,
            HotQueryTtlHours = 24,
            WarmQueryTtlHours = 6,
            ColdQueryTtlHours = 1
        });

        var strategy = new DynamicTtlStrategy(_mockLogger.Object, _mockConfig.Object);

        // Act (When): Calculate TTL at boundary
        var ttl = await strategy.CalculateTtlAsync(hitCount);

        // Assert (Then): Correct classification and TTL
        Assert.Equal(TimeSpan.FromHours(expectedHours), ttl);
    }

    [Fact]
    public async Task CalculateTtlAsync_NegativeHitCount_ThrowsArgumentException()
    {
        // Arrange (Given): Invalid negative hit count
        _mockConfig.Setup(c => c.Value).Returns(new CacheOptimizationConfiguration
        {
            HotQueryThreshold = 10,
            WarmQueryThreshold = 3,
            HotQueryTtlHours = 24,
            WarmQueryTtlHours = 6,
            ColdQueryTtlHours = 1
        });

        var strategy = new DynamicTtlStrategy(_mockLogger.Object, _mockConfig.Object);
        var hitCount = -5; // Invalid

        // Act (When): Calculate TTL with negative count
        Func<Task> act = async () => await strategy.CalculateTtlAsync(hitCount);

        // Assert (Then): Throws ArgumentException
        var exception = await Assert.ThrowsAsync<ArgumentException>(act);
        Assert.Contains("hitCount", exception.Message);
        Assert.Contains("cannot be negative", exception.Message);
    }
}
