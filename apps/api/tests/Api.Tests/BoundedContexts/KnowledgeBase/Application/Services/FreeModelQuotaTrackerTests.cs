using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for FreeModelQuotaTracker (Issue #5087).
/// Verifies Redis key writes for RPD exhaustion, last error type, and reset timestamp.
/// All Redis failures degrade gracefully (returns false / null).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5087")]
public sealed class FreeModelQuotaTrackerTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();
    private readonly Mock<ILogger<FreeModelQuotaTracker>> _loggerMock = new();
    private readonly FreeModelQuotaTracker _sut;

    public FreeModelQuotaTrackerTests()
    {
        _redisMock
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        _sut = new FreeModelQuotaTracker(_redisMock.Object, _loggerMock.Object);
    }

    // ─── RecordRateLimitErrorAsync ────────────────────────────────────────────

    [Fact]
    public async Task RecordRateLimitErrorAsync_Rpd_WritesExhaustedKeyAndResetKey()
    {
        // SetReturnsDefault bypasses Moq expression-tree overload resolution issues with
        // StackExchange.Redis 2.10+ optional parameters (see RedisBackgroundTaskOrchestratorTests).
        _dbMock.SetReturnsDefault<Task<bool>>(Task.FromResult(true));

        await _sut.RecordRateLimitErrorAsync(
            "meta-llama/llama-3.3-70b-instruct:free",
            RateLimitErrorType.Rpd,
            resetTimestampMs: 1741305600000L);

        // Should set rpd_exhausted, rpd_reset_at, and last_error (3 keys)
        var writeCount = _dbMock.Invocations.Count(i => i.Method.Name == "StringSetAsync");
        writeCount.Should().Be(3);
    }

    [Fact]
    public async Task RecordRateLimitErrorAsync_RpdNoReset_WritesExhaustedAndLastError()
    {
        _dbMock.SetReturnsDefault<Task<bool>>(Task.FromResult(true));

        await _sut.RecordRateLimitErrorAsync(
            "meta-llama/llama-3.3-70b-instruct:free",
            RateLimitErrorType.Rpd,
            resetTimestampMs: null);

        // RPD without reset: rpd_exhausted + last_error = 2 keys
        var writeCount = _dbMock.Invocations.Count(i => i.Method.Name == "StringSetAsync");
        writeCount.Should().Be(2);
    }

    [Fact]
    public async Task RecordRateLimitErrorAsync_Rpm_WritesOnlyLastError()
    {
        _dbMock.SetReturnsDefault<Task<bool>>(Task.FromResult(true));

        await _sut.RecordRateLimitErrorAsync(
            "meta-llama/llama-3.3-70b-instruct:free",
            RateLimitErrorType.Rpm,
            resetTimestampMs: 1741305660000L);

        // RPM: only last_error key (no rpd_exhausted, no rpd_reset_at)
        var writeCount = _dbMock.Invocations.Count(i => i.Method.Name == "StringSetAsync");
        writeCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordRateLimitErrorAsync_RedisThrows_DoesNotPropagate()
    {
        // Make all Task<bool> methods throw asynchronously to exercise the catch block
        _dbMock.SetReturnsDefault<Task<bool>>(
            Task.FromException<bool>(new RedisException("Connection refused")));

        // Must not throw
        await _sut.RecordRateLimitErrorAsync(
            "model:free", RateLimitErrorType.Rpd, resetTimestampMs: null);
    }

    // ─── IsRpdExhaustedAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task IsRpdExhaustedAsync_KeyTrue_ReturnsTrue()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("true"));

        var result = await _sut.IsRpdExhaustedAsync("model:free");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsRpdExhaustedAsync_KeyMissing_ReturnsFalse()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _sut.IsRpdExhaustedAsync("model:free");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsRpdExhaustedAsync_KeyOtherValue_ReturnsFalse()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("false"));

        var result = await _sut.IsRpdExhaustedAsync("model:free");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsRpdExhaustedAsync_RedisThrows_ReturnsFalse()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Connection refused"));

        var result = await _sut.IsRpdExhaustedAsync("model:free");

        result.Should().BeFalse();
    }

    // ─── GetLastErrorTypeAsync ───────────────────────────────────────────────

    [Theory]
    [InlineData("rpd", RateLimitErrorType.Rpd)]
    [InlineData("rpm", RateLimitErrorType.Rpm)]
    [InlineData("paymentrequired", RateLimitErrorType.PaymentRequired)]
    [InlineData("unknown", RateLimitErrorType.Unknown)]
    public async Task GetLastErrorTypeAsync_KnownValue_ReturnsParsedType(string stored, RateLimitErrorType expected)
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(stored));

        var result = await _sut.GetLastErrorTypeAsync();

        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetLastErrorTypeAsync_NoKey_ReturnsNull()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _sut.GetLastErrorTypeAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLastErrorTypeAsync_UnknownString_ReturnsNull()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("garbage"));

        var result = await _sut.GetLastErrorTypeAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLastErrorTypeAsync_RedisThrows_ReturnsNull()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Connection refused"));

        var result = await _sut.GetLastErrorTypeAsync();

        result.Should().BeNull();
    }

    // ─── GetRpdResetTimeAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetRpdResetTimeAsync_ValidTimestamp_ReturnsUtcDateTime()
    {
        const long ms = 1741305600000L; // 2025-03-07 00:00:00 UTC
        var expectedUtc = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;

        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(ms.ToString()));

        var result = await _sut.GetRpdResetTimeAsync();

        result.Should().Be(expectedUtc);
    }

    [Fact]
    public async Task GetRpdResetTimeAsync_NoKey_ReturnsNull()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _sut.GetRpdResetTimeAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRpdResetTimeAsync_InvalidValue_ReturnsNull()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("not-a-number"));

        var result = await _sut.GetRpdResetTimeAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRpdResetTimeAsync_RedisThrows_ReturnsNull()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Connection refused"));

        var result = await _sut.GetRpdResetTimeAsync();

        result.Should().BeNull();
    }
}
