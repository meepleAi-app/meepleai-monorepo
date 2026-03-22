using Api.BoundedContexts.Administration.Application.Queries.Resources;
using FluentAssertions;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.ResourcesTests;

/// <summary>
/// Integration tests for cache metrics query.
/// Issue #3695: Resources Monitoring - Cache metrics
/// </summary>
[Collection("Sequential")]
[Trait("Category", "Integration")]
[Trait("BoundedContext", "Administration")]
[Trait("Epic", "3685")]
public class CacheMetricsQueryTests : IAsyncLifetime
{
    private RedisContainer? _redis;
    private IConnectionMultiplexer? _connection;

    public async ValueTask InitializeAsync()
    {
        _redis = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await _redis.StartAsync().ConfigureAwait(false);

        _connection = await ConnectionMultiplexer.ConnectAsync($"{_redis.GetConnectionString()},allowAdmin=true")
            .ConfigureAwait(false);

        // Add some test data
        var db = _connection.GetDatabase();
        await db.StringSetAsync("test:key1", "value1").ConfigureAwait(false);
        await db.StringSetAsync("test:key2", "value2").ConfigureAwait(false);
        await db.StringSetAsync("test:key3", "value3").ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        if (_redis != null)
        {
            await _redis.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Handle_ReturnsValidCacheMetrics()
    {
        // Arrange
        var handler = new GetCacheMetricsQueryHandler(_connection!);
        var query = new GetCacheMetricsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.UsedMemoryBytes.Should().BeGreaterThan(0, "memory usage should be positive");
        result.UsedMemoryFormatted.Should().NotBeNullOrWhiteSpace();
        result.TotalKeys.Should().BeGreaterThanOrEqualTo(3, "should have at least our test keys");
        result.HitRate.Should().BeInRange(0, 1, "hit rate should be between 0 and 1");
        result.MemoryUsagePercent.Should().BeGreaterThanOrEqualTo(0);
        result.MeasuredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_FormatsBytesCorrectly()
    {
        // Arrange
        var handler = new GetCacheMetricsQueryHandler(_connection!);
        var query = new GetCacheMetricsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.UsedMemoryFormatted.Should().MatchRegex(@"^\d+\.?\d* (B|KB|MB|GB|TB)$",
            "formatted size should match byte format pattern");
    }

    [Fact]
    public async Task Handle_WithCacheActivity_TracksHitRate()
    {
        // Arrange
        var handler = new GetCacheMetricsQueryHandler(_connection!);
        var query = new GetCacheMetricsQuery();
        var db = _connection!.GetDatabase();

        // Perform some cache operations to generate hits/misses
        await db.StringGetAsync("test:key1"); // Hit
        await db.StringGetAsync("test:key1"); // Hit
        await db.StringGetAsync("nonexistent"); // Miss

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.KeyspaceHits.Should().BeGreaterThanOrEqualTo(2, "should have at least our test hits");
        result.KeyspaceMisses.Should().BeGreaterThanOrEqualTo(1, "should have at least our test miss");
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new GetCacheMetricsQueryHandler(_connection!);

        // Act
        var act = () => handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullRedis_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GetCacheMetricsQueryHandler(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
