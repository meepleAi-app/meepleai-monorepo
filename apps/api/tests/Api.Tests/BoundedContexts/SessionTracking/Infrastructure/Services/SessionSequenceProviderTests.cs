using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Infrastructure.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class SessionSequenceProviderTests
{
    [Fact]
    public async Task NextAsync_is_strictly_monotonic_per_session()
    {
        var p = new RedisSessionSequenceProvider(redis: null, NullLogger<RedisSessionSequenceProvider>.Instance);
        var sid = Guid.NewGuid();
        var a = await p.NextAsync(sid, default);
        var b = await p.NextAsync(sid, default);
        var c = await p.NextAsync(sid, default);
        Assert.True(b > a, $"Expected {b} > {a}");
        Assert.True(c > b, $"Expected {c} > {b}");
    }

    [Fact]
    public async Task NextAsync_independent_counters_per_session()
    {
        var p = new RedisSessionSequenceProvider(redis: null, NullLogger<RedisSessionSequenceProvider>.Instance);
        var sid1 = Guid.NewGuid();
        var sid2 = Guid.NewGuid();
        var a1 = await p.NextAsync(sid1, default);
        var a2 = await p.NextAsync(sid2, default);
        Assert.Equal(1L, a1);
        Assert.Equal(1L, a2);
        var b1 = await p.NextAsync(sid1, default);
        Assert.Equal(2L, b1);
        var b2 = await p.NextAsync(sid2, default);
        Assert.Equal(2L, b2);
    }

    [Fact]
    public async Task NextAsync_concurrent_calls_all_unique()
    {
        var p = new RedisSessionSequenceProvider(redis: null, NullLogger<RedisSessionSequenceProvider>.Instance);
        var sid = Guid.NewGuid();
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => p.NextAsync(sid, default))
            .ToArray();
        var results = await Task.WhenAll(tasks);
        Assert.Equal(100, results.Distinct().Count());
        Assert.Equal(1L, results.Min());
        Assert.Equal(100L, results.Max());
    }

    // ── Redis-vs-fallback decision (Issue #2563) ────────────────────────────────

    /// <summary>
    /// When Redis is connected at construction, the provider must use the Redis INCR counter
    /// (decided ONCE at the ctor), not the in-process fallback. Mirrors SessionBroadcastService.
    /// </summary>
    [Fact]
    public async Task NextAsync_WhenRedisConnectedAtCtor_UsesRedisIncr()
    {
        var mockDb = new Mock<IDatabase>();
        mockDb
            .Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(42L);
        mockDb
            .Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var mockRedis = BuildConnectedRedis(mockDb);

        var p = new RedisSessionSequenceProvider(mockRedis.Object, NullLogger<RedisSessionSequenceProvider>.Instance);

        var seq = await p.NextAsync(Guid.NewGuid(), default);

        Assert.Equal(42L, seq);
        mockDb.Verify(
            d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()),
            Times.Once);
    }

    /// <summary>
    /// Regression for #2563: if Redis drops mid-session, the provider must NOT silently fall back
    /// to the in-process counter (which restarts at 0 → emits a seq below the Redis high-watermark
    /// → corrupts ZSET eviction + Last-Event-ID resume). With the ctor-decision fix the failure
    /// propagates instead of returning a non-monotonic value.
    /// </summary>
    [Fact]
    public async Task NextAsync_WhenRedisFailsMidSession_DoesNotFallBackToZero()
    {
        var sid = Guid.NewGuid();
        var redisDown = false;
        var mockDb = new Mock<IDatabase>();
        mockDb
            .Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(() =>
            {
                if (redisDown)
                {
                    throw new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis dropped mid-session");
                }

                return 50L;
            });
        mockDb
            .Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var mockRedis = BuildConnectedRedis(mockDb);
        var p = new RedisSessionSequenceProvider(mockRedis.Object, NullLogger<RedisSessionSequenceProvider>.Instance);

        // High-watermark established at seq 50 via Redis.
        var first = await p.NextAsync(sid, default);
        Assert.Equal(50L, first);

        // Redis drops.
        redisDown = true;

        // The next call must NOT return 1 (the old fallback-to-zero bug). It propagates instead.
        await Assert.ThrowsAsync<RedisConnectionException>(() => p.NextAsync(sid, default));
    }

    private static Mock<IConnectionMultiplexer> BuildConnectedRedis(Mock<IDatabase> mockDb)
    {
        var mockRedis = new Mock<IConnectionMultiplexer>();
        mockRedis.Setup(r => r.IsConnected).Returns(true);
        mockRedis
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDb.Object);
        return mockRedis;
    }
}
