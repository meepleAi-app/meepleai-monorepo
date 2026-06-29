using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
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
}
