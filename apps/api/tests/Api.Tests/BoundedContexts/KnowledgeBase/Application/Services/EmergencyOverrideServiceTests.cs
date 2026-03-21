using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for EmergencyOverrideService (Issue #5476).
/// Verifies Redis-backed override activation/deactivation, L1 cache behavior,
/// and fail-open pattern when Redis is unavailable.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5476")]
public sealed class EmergencyOverrideServiceTests : IDisposable
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly Mock<ILogger<EmergencyOverrideService>> _loggerMock = new();
    private readonly EmergencyOverrideService _sut;

    public EmergencyOverrideServiceTests()
    {
        _redisMock
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        _sut = new EmergencyOverrideService(
            _redisMock.Object,
            _memoryCache,
            _mediatorMock.Object,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }

    // ─── ActivateOverrideAsync ──────────────────────────────────────────────

    [Fact]
    public async Task ActivateOverrideAsync_WritesRedisKeyWithTtl()
    {
        // SetReturnsDefault bypasses Moq expression-tree overload resolution issues with
        // StackExchange.Redis 2.10+ optional parameters (see FreeModelQuotaTrackerTests).
        _dbMock.SetReturnsDefault<Task<bool>>(Task.FromResult(true));

        await _sut.ActivateOverrideAsync(
            "force-ollama-only", 30, "Test reason", Guid.NewGuid());

        // Verify StringSetAsync was called (key + payload)
        var stringSetCalls = _dbMock.Invocations
            .Count(i => i.Method.Name == "StringSetAsync");
        stringSetCalls.Should().Be(1);

        // Verify the key contains the action name
        var call = _dbMock.Invocations
            .First(i => i.Method.Name == "StringSetAsync");
        call.Arguments[0]!.ToString()!.Should().Contain("llm:emergency:force-ollama-only");
    }

    [Fact]
    public async Task ActivateOverrideAsync_AddsToActiveSet()
    {
        _dbMock.SetReturnsDefault<Task<bool>>(Task.FromResult(true));

        await _sut.ActivateOverrideAsync(
            "flush-quota-cache", 15, "Cache issue", Guid.NewGuid());

        _dbMock.Verify(
            d => d.SetAddAsync(
                It.Is<RedisKey>(k => k.ToString() == "llm:emergency:active_set"),
                It.Is<RedisValue>(v => v.ToString() == "flush-quota-cache"),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task ActivateOverrideAsync_InvalidatesL1Cache()
    {
        _dbMock.SetReturnsDefault<Task<bool>>(Task.FromResult(true));

        // Pre-populate L1 cache
        _memoryCache.Set("emergency:force-ollama-only", true);

        await _sut.ActivateOverrideAsync(
            "force-ollama-only", 30, "Test", Guid.NewGuid());

        // L1 cache should be invalidated
        _memoryCache.TryGetValue("emergency:force-ollama-only", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ActivateOverrideAsync_RedisThrows_Propagates()
    {
        // Use SetReturnsDefault to make ALL Task<bool> methods throw (overload-safe)
        _dbMock.SetReturnsDefault<Task<bool>>(
            Task.FromException<bool>(new RedisException("Connection refused")));

        Func<Task> act = () =>
            _sut.ActivateOverrideAsync("force-ollama-only", 30, "Test", Guid.NewGuid());
        await act.Should().ThrowAsync<RedisException>();
    }

    // ─── DeactivateOverrideAsync ────────────────────────────────────────────

    [Fact]
    public async Task DeactivateOverrideAsync_DeletesKeyAndRemovesFromSet()
    {
        _dbMock.SetReturnsDefault<Task<bool>>(Task.FromResult(true));

        await _sut.DeactivateOverrideAsync("force-ollama-only", Guid.NewGuid());

        _dbMock.Verify(
            d => d.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == "llm:emergency:force-ollama-only"),
                It.IsAny<CommandFlags>()),
            Times.Once);

        _dbMock.Verify(
            d => d.SetRemoveAsync(
                It.Is<RedisKey>(k => k.ToString() == "llm:emergency:active_set"),
                It.Is<RedisValue>(v => v.ToString() == "force-ollama-only"),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task DeactivateOverrideAsync_InvalidatesL1Cache()
    {
        _dbMock.SetReturnsDefault<Task<bool>>(Task.FromResult(true));

        _memoryCache.Set("emergency:reset-circuit-breaker", true);

        await _sut.DeactivateOverrideAsync("reset-circuit-breaker", Guid.NewGuid());

        _memoryCache.TryGetValue("emergency:reset-circuit-breaker", out _).Should().BeFalse();
    }

    // ─── IsOverrideActiveAsync ──────────────────────────────────────────────

    [Fact]
    public async Task IsOverrideActiveAsync_KeyExists_ReturnsTrue()
    {
        _dbMock
            .Setup(d => d.KeyExistsAsync(
                It.Is<RedisKey>(k => k.ToString() == "llm:emergency:force-ollama-only"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _sut.IsOverrideActiveAsync("force-ollama-only");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsOverrideActiveAsync_KeyMissing_ReturnsFalse()
    {
        _dbMock
            .Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        var result = await _sut.IsOverrideActiveAsync("force-ollama-only");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsOverrideActiveAsync_UsesL1Cache()
    {
        // Pre-populate L1 cache
        _memoryCache.Set("emergency:force-ollama-only", true);

        var result = await _sut.IsOverrideActiveAsync("force-ollama-only");

        result.Should().BeTrue();
        // Redis should NOT be called because L1 cache hit
        _dbMock.Verify(
            d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public async Task IsOverrideActiveAsync_CacheMiss_QueriesRedisAndCaches()
    {
        _dbMock
            .Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // First call: cache miss → Redis
        var result1 = await _sut.IsOverrideActiveAsync("force-ollama-only");
        result1.Should().BeTrue();

        // Second call: should use cache
        var result2 = await _sut.IsOverrideActiveAsync("force-ollama-only");
        result2.Should().BeTrue();

        // Redis should be called only once
        _dbMock.Verify(
            d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task IsOverrideActiveAsync_RedisThrows_ReturnsFalse_FailOpen()
    {
        _dbMock
            .Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Connection refused"));

        var result = await _sut.IsOverrideActiveAsync("force-ollama-only");

        // Fail-open: Redis down → overrides inactive (safe default)
        result.Should().BeFalse();
    }

    // ─── IsForceOllamaOnlyAsync ─────────────────────────────────────────────

    [Fact]
    public async Task IsForceOllamaOnlyAsync_DelegatesToIsOverrideActive()
    {
        _dbMock
            .Setup(d => d.KeyExistsAsync(
                It.Is<RedisKey>(k => k.ToString() == "llm:emergency:force-ollama-only"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _sut.IsForceOllamaOnlyAsync();

        result.Should().BeTrue();
    }

    // ─── GetActiveOverridesAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetActiveOverridesAsync_NoActiveOverrides_ReturnsEmptyList()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _sut.GetActiveOverridesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveOverridesAsync_RedisThrows_ReturnsEmptyList()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Connection refused"));

        var result = await _sut.GetActiveOverridesAsync();

        result.Should().BeEmpty();
    }
}
