using Api.Infrastructure.Translation;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Text.Json;
using Xunit;

namespace Api.Tests.Infrastructure.Translation;

[Trait("Category", TestCategories.Unit)]
public class RedisTranslationCacheTests
{
    private readonly IDistributedCache _distributedCache = Substitute.For<IDistributedCache>();

    [Fact]
    public async Task TryGetAsync_WhenCached_ReturnsCachedResult()
    {
        var gameId = Guid.NewGuid();
        var cached = TranslationResult.CreateSuccess("Sei arrivato ad Avalon.", "en", "it", 0.001m);
        var cache = new RedisTranslationCache(_distributedCache, NullLogger<RedisTranslationCache>.Instance);

        var json = JsonSerializer.SerializeToUtf8Bytes(cached);
        _distributedCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(json);

        var result = await cache.TryGetAsync(gameId, "You have arrived at Avalon.", "en", "it", CancellationToken.None);

        result.Should().NotBeNull();
        result!.TranslatedText.Should().Be("Sei arrivato ad Avalon.");
    }

    [Fact]
    public async Task TryGetAsync_WhenCacheMiss_ReturnsNull()
    {
        var cache = new RedisTranslationCache(_distributedCache, NullLogger<RedisTranslationCache>.Instance);
        _distributedCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var result = await cache.TryGetAsync(Guid.NewGuid(), "text", "en", "it", CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task TryGetAsync_WhenRedisThrows_ReturnsNullGracefully()
    {
        var cache = new RedisTranslationCache(_distributedCache, NullLogger<RedisTranslationCache>.Instance);
        _distributedCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<byte[]?>(_ => throw new InvalidOperationException("Redis down"));

        var result = await cache.TryGetAsync(Guid.NewGuid(), "text", "en", "it", CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task InvalidateGameAsync_DoesNotThrow()
    {
        var cache = new RedisTranslationCache(_distributedCache, NullLogger<RedisTranslationCache>.Instance);
        var act = () => cache.InvalidateGameAsync(Guid.NewGuid(), CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SetAsync_WritesWithDefaultTtl()
    {
        var cache = new RedisTranslationCache(_distributedCache, NullLogger<RedisTranslationCache>.Instance);
        var result = TranslationResult.CreateSuccess("traduzione", "en", "it", 0.001m);

        await cache.SetAsync(Guid.NewGuid(), "text", "en", "it", result, ttl: null, CancellationToken.None);

        await _distributedCache.Received(1).SetAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromDays(7)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryGetAsync_DifferentGameIds_UseDifferentKeys()
    {
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        var cache = new RedisTranslationCache(_distributedCache, NullLogger<RedisTranslationCache>.Instance);

        _distributedCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        await cache.TryGetAsync(gameId1, "text", "en", "it", CancellationToken.None);
        await cache.TryGetAsync(gameId2, "text", "en", "it", CancellationToken.None);

        await _distributedCache.Received(2).GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        var calls = _distributedCache.ReceivedCalls().ToList();
        calls.Should().HaveCount(2);
        var key1 = calls[0].GetArguments()[0] as string;
        var key2 = calls[1].GetArguments()[0] as string;
        key1.Should().NotBe(key2);
    }
}
