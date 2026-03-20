using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for OpenRouterRateLimitTracker (Issue #5075).
/// Issue #5076: Phase 1 test suite — covers sliding window logic, threshold detection,
/// and graceful Redis degradation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5076")]
public sealed class OpenRouterRateLimitTrackerTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _dbMock;
    private readonly Mock<IBatch> _batchMock;
    private readonly Mock<IOpenRouterUsageService> _usageServiceMock;
    private readonly Mock<ILogger<OpenRouterRateLimitTracker>> _loggerMock;

    public OpenRouterRateLimitTrackerTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _dbMock = new Mock<IDatabase>();
        _batchMock = new Mock<IBatch>();
        _usageServiceMock = new Mock<IOpenRouterUsageService>();
        _loggerMock = new Mock<ILogger<OpenRouterRateLimitTracker>>();

        _redisMock
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        // Default batch setup — batch.Execute() completes all pending tasks
        _dbMock
            .Setup(d => d.CreateBatch(It.IsAny<object>()))
            .Returns(_batchMock.Object);

        SetupDefaultBatchCalls();
    }

    private void SetupDefaultBatchCalls()
    {
        _batchMock
            .Setup(b => b.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(), It.IsAny<SortedSetWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _batchMock
            .Setup(b => b.SortedSetRemoveRangeByScoreAsync(It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(0L);
        _batchMock
            .Setup(b => b.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _batchMock
            .Setup(b => b.Execute())
            .Verifiable();
    }

    private OpenRouterRateLimitTracker CreateSut()
        => new(_redisMock.Object, _usageServiceMock.Object, _loggerMock.Object);

    // ─── Constructor ────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullRedis_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OpenRouterRateLimitTracker(null!, _usageServiceMock.Object, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_NullUsageService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OpenRouterRateLimitTracker(_redisMock.Object, null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OpenRouterRateLimitTracker(_redisMock.Object, _usageServiceMock.Object, null!));
    }

    // ─── RecordRequestAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task RecordRequestAsync_WithTokens_CreatesBatchWithRpmAndTpmEntries()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.RecordRequestAsync("openrouter", "gpt-4o", totalTokens: 650);

        // Assert — batch was executed
        _batchMock.Verify(b => b.Execute(), Times.Once);

        // RPM ZADD called
        _batchMock.Verify(b => b.SortedSetAddAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("rpm:openrouter")),
            It.IsAny<RedisValue>(),
            It.IsAny<double>(),
            It.IsAny<SortedSetWhen>(),
            It.IsAny<CommandFlags>()), Times.Once);

        // TPM ZADD called
        _batchMock.Verify(b => b.SortedSetAddAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("tpm:openrouter")),
            It.IsAny<RedisValue>(),
            It.IsAny<double>(),
            It.IsAny<SortedSetWhen>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RecordRequestAsync_ZeroTokens_OnlyRecordsRpm()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.RecordRequestAsync("openrouter", "gpt-4o", totalTokens: 0);

        // Assert — RPM entry added, TPM entry NOT added
        _batchMock.Verify(b => b.SortedSetAddAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("rpm:openrouter")),
            It.IsAny<RedisValue>(),
            It.IsAny<double>(),
            It.IsAny<SortedSetWhen>(),
            It.IsAny<CommandFlags>()), Times.Once);

        _batchMock.Verify(b => b.SortedSetAddAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("tpm:openrouter")),
            It.IsAny<RedisValue>(),
            It.IsAny<double>(),
            It.IsAny<SortedSetWhen>(),
            It.IsAny<CommandFlags>()), Times.Never);

        // Batch must still be executed even when no TPM entry is added
        _batchMock.Verify(b => b.Execute(), Times.Once);
    }

    [Fact]
    public async Task RecordRequestAsync_RedisThrows_DoesNotPropagate()
    {
        // Arrange
        _dbMock
            .Setup(d => d.CreateBatch(It.IsAny<object>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis down"));

        var sut = CreateSut();

        // Act — must not throw
        var ex = await Record.ExceptionAsync(() =>
            sut.RecordRequestAsync("openrouter", "gpt-4o", 100));

        Assert.Null(ex);
    }

    // ─── GetCurrentStatusAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentStatusAsync_WithKnownLimit_ReturnsUtilization()
    {
        // Arrange
        _dbMock
            .Setup(d => d.SortedSetRemoveRangeByScoreAsync(It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(0L);

        // RPM = 80 requests
        _dbMock
            .Setup(d => d.SortedSetLengthAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("rpm:")),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(80L);

        // TPM entries: two entries each with 500 tokens
        var tpmEntries = new RedisValue[]
        {
            "req1:500",
            "req2:500"
        };
        _dbMock
            .Setup(d => d.SortedSetRangeByScoreAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("tpm:")),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<Order>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(tpmEntries);

        _usageServiceMock
            .Setup(u => u.GetAccountStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenRouterAccountStatus
            {
                RateLimitRequests = 200,
                RateLimitInterval = "minute"
            });

        var sut = CreateSut();

        // Act
        var status = await sut.GetCurrentStatusAsync("openrouter");

        // Assert
        status.CurrentRpm.Should().Be(80);
        status.LimitRpm.Should().Be(200);
        status.CurrentTpm.Should().Be(1000);
        Assert.Equal(0.4, status.UtilizationPercent, precision: 5); // 80/200 = 0.4
        Assert.False(status.IsThrottled);
    }

    [Fact]
    public async Task GetCurrentStatusAsync_AtLimit_IsThrottledTrue()
    {
        // Arrange
        _dbMock
            .Setup(d => d.SortedSetRemoveRangeByScoreAsync(It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(0L);

        _dbMock
            .Setup(d => d.SortedSetLengthAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("rpm:")),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(200L);

        _dbMock
            .Setup(d => d.SortedSetRangeByScoreAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("tpm:")),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<Order>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<RedisValue>());

        _usageServiceMock
            .Setup(u => u.GetAccountStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenRouterAccountStatus { RateLimitRequests = 200 });

        var sut = CreateSut();

        // Act
        var status = await sut.GetCurrentStatusAsync("openrouter");

        // Assert
        Assert.True(status.IsThrottled);
        status.UtilizationPercent.Should().BeApproximately(1.0, precision: 5);
    }

    [Fact]
    public async Task GetCurrentStatusAsync_UnknownLimit_UtilizationZero()
    {
        // Arrange — no account status cached
        _dbMock
            .Setup(d => d.SortedSetRemoveRangeByScoreAsync(It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(0L);
        _dbMock
            .Setup(d => d.SortedSetLengthAsync(It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(50L);
        _dbMock
            .Setup(d => d.SortedSetRangeByScoreAsync(It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<Order>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<RedisValue>());

        _usageServiceMock
            .Setup(u => u.GetAccountStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((OpenRouterAccountStatus?)null);

        var sut = CreateSut();

        // Act
        var status = await sut.GetCurrentStatusAsync("openrouter");

        // Assert — limit unknown → utilization = 0, not throttled
        status.UtilizationPercent.Should().Be(0.0);
        Assert.False(status.IsThrottled);
    }

    [Fact]
    public async Task GetCurrentStatusAsync_RedisThrows_ReturnsEmptyStatus()
    {
        // Arrange
        _dbMock
            .Setup(d => d.SortedSetRemoveRangeByScoreAsync(It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis down"));

        var sut = CreateSut();

        // Act
        var status = await sut.GetCurrentStatusAsync("openrouter");

        // Assert — graceful degradation returns zero-valued status
        status.CurrentRpm.Should().Be(0);
        status.LimitRpm.Should().Be(0);
        status.UtilizationPercent.Should().Be(0.0);
        Assert.False(status.IsThrottled);
    }

    // ─── IsApproachingLimitAsync ─────────────────────────────────────────────

    [Theory]
    [InlineData(79, 200, 80, false)]   // 39.5% < 80% threshold
    [InlineData(160, 200, 80, true)]   // 80% = threshold (>= triggers)
    [InlineData(181, 200, 80, true)]   // 90.5% > threshold
    [InlineData(200, 200, 80, true)]   // 100% = fully throttled
    public async Task IsApproachingLimitAsync_ThresholdBehavior(
        int currentRpm, int limitRpm, int thresholdPercent, bool expectedApproaching)
    {
        // Arrange
        _dbMock
            .Setup(d => d.SortedSetRemoveRangeByScoreAsync(It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(0L);
        _dbMock
            .Setup(d => d.SortedSetLengthAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("rpm:")),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((long)currentRpm);
        _dbMock
            .Setup(d => d.SortedSetRangeByScoreAsync(It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<Order>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<RedisValue>());
        _usageServiceMock
            .Setup(u => u.GetAccountStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenRouterAccountStatus { RateLimitRequests = limitRpm });

        var sut = CreateSut();

        // Act
        var result = await sut.IsApproachingLimitAsync("openrouter", thresholdPercent);

        // Assert
        result.Should().Be(expectedApproaching);
    }

    [Fact]
    public async Task IsApproachingLimitAsync_UnknownLimit_ReturnsFalse()
    {
        // Arrange — limit = 0 (unknown)
        _dbMock
            .Setup(d => d.SortedSetRemoveRangeByScoreAsync(It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(0L);
        _dbMock
            .Setup(d => d.SortedSetLengthAsync(It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(9999L);
        _dbMock
            .Setup(d => d.SortedSetRangeByScoreAsync(It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(), It.IsAny<Order>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<RedisValue>());
        _usageServiceMock
            .Setup(u => u.GetAccountStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenRouterAccountStatus { RateLimitRequests = 0 }); // 0 = unknown

        var sut = CreateSut();

        // Act
        var result = await sut.IsApproachingLimitAsync("openrouter");

        // Assert
        Assert.False(result);
    }
}
