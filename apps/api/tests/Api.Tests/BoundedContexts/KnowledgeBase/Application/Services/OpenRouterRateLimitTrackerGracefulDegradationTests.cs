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
/// Focused test suite for Redis failure graceful degradation in OpenRouterRateLimitTracker.
/// Validates that all public methods degrade safely when Redis is unavailable or throws,
/// ensuring the application continues to function without rate-limit tracking.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5075")]
public sealed class OpenRouterRateLimitTrackerGracefulDegradationTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();
    private readonly Mock<IBatch> _batchMock = new();
    private readonly Mock<IOpenRouterUsageService> _usageServiceMock = new();
    private readonly ILogger<OpenRouterRateLimitTracker> _logger;

    public OpenRouterRateLimitTrackerGracefulDegradationTests()
    {
        _redisMock
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        _dbMock
            .Setup(d => d.CreateBatch(It.IsAny<object>()))
            .Returns(_batchMock.Object);

        _logger = new LoggerFactory().CreateLogger<OpenRouterRateLimitTracker>();
    }

    private OpenRouterRateLimitTracker CreateSut() => new(
        _redisMock.Object, _usageServiceMock.Object, _logger);

    // ─── RecordRequestAsync — Redis failure scenarios ──────────────────────────

    [Fact]
    public async Task RecordRequest_RedisWriteFails_DoesNotThrow()
    {
        // Arrange — batch.Execute() throws, simulating Redis connection loss mid-batch
        SetupDefaultBatchTasks();

        _batchMock
            .Setup(b => b.Execute())
            .Throws(new RedisConnectionException(
                ConnectionFailureType.UnableToConnect, "Connection refused"));

        var sut = CreateSut();

        // Act — must not propagate the exception
        var exception = await Record.ExceptionAsync(() =>
            sut.RecordRequestAsync("openrouter", "gpt-4o", totalTokens: 500));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public async Task RecordRequest_GetDatabaseThrows_DoesNotThrow()
    {
        // Arrange — Redis multiplexer itself is broken
        _redisMock
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Throws(new RedisConnectionException(
                ConnectionFailureType.InternalFailure, "Multiplexer disposed"));

        var sut = CreateSut();

        // Act
        var exception = await Record.ExceptionAsync(() =>
            sut.RecordRequestAsync("openrouter", "gpt-4o", totalTokens: 250));

        // Assert
        exception.Should().BeNull();
    }

    // ─── GetCurrentStatusAsync — Redis failure scenarios ───────────────────────

    [Fact]
    public async Task GetCurrentStatus_RedisReadFails_ReturnsEmptyStatus()
    {
        // Arrange — first Redis call (SortedSetRemoveRangeByScoreAsync) throws
        _dbMock
            .Setup(d => d.SortedSetRemoveRangeByScoreAsync(
                It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(
                ConnectionFailureType.UnableToConnect, "Connection refused"));

        var sut = CreateSut();

        // Act
        var status = await sut.GetCurrentStatusAsync("openrouter");

        // Assert — all fields should be zero/default (graceful degradation)
        status.CurrentRpm.Should().Be(0);
        status.LimitRpm.Should().Be(0);
        status.CurrentTpm.Should().Be(0);
        status.LimitTpm.Should().Be(0);
        status.UtilizationPercent.Should().Be(0.0);
        status.IsThrottled.Should().BeFalse();
    }

    [Fact]
    public async Task GetCurrentStatus_SortedSetLengthThrows_ReturnsEmptyStatus()
    {
        // Arrange — eviction succeeds but count query fails
        _dbMock
            .Setup(d => d.SortedSetRemoveRangeByScoreAsync(
                It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(0L);

        _dbMock
            .Setup(d => d.SortedSetLengthAsync(
                It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisTimeoutException("Timeout performing ZCARD", CommandStatus.Unknown));

        var sut = CreateSut();

        // Act
        var status = await sut.GetCurrentStatusAsync("openrouter");

        // Assert
        status.CurrentRpm.Should().Be(0);
        status.LimitRpm.Should().Be(0);
        status.IsThrottled.Should().BeFalse();
    }

    [Fact]
    public async Task GetCurrentStatus_RedisAvailable_ReturnsActualCounts()
    {
        // Arrange — Redis is healthy and returns real data
        _dbMock
            .Setup(d => d.SortedSetRemoveRangeByScoreAsync(
                It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(2L); // evicted 2 stale entries

        _dbMock
            .Setup(d => d.SortedSetLengthAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("rpm:")),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(42L);

        _dbMock
            .Setup(d => d.SortedSetRangeByScoreAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("tpm:")),
                It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<Exclude>(), It.IsAny<Order>(),
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue[] { "abc123:300", "def456:700" });

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

        // Assert — real data flows through when Redis is healthy
        status.CurrentRpm.Should().Be(42);
        (status.CurrentRpm > 0).Should().BeTrue();
        status.LimitRpm.Should().Be(200);
        status.CurrentTpm.Should().Be(1000); // 300 + 700
        status.UtilizationPercent.Should().BeApproximately(0.21, 0.00001); // 42/200
        status.IsThrottled.Should().BeFalse();
    }

    // ─── IsApproachingLimitAsync — Redis failure scenarios ─────────────────────

    [Fact]
    public async Task IsApproachingLimit_RedisDown_ReturnsFalse()
    {
        // Arrange — Redis is completely unavailable
        _dbMock
            .Setup(d => d.SortedSetRemoveRangeByScoreAsync(
                It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(
                ConnectionFailureType.UnableToConnect, "Connection refused"));

        var sut = CreateSut();

        // Act — should return safe default (false) rather than throwing
        var approaching = await sut.IsApproachingLimitAsync("openrouter", thresholdPercent: 80);

        // Assert — false is the safe default: we don't block requests when we can't measure
        approaching.Should().BeFalse();
    }

    [Fact]
    public async Task IsApproachingLimit_RedisDown_DoesNotCallUsageService()
    {
        // Arrange — Redis fails on first call inside GetCurrentStatusAsync
        _dbMock
            .Setup(d => d.SortedSetRemoveRangeByScoreAsync(
                It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(
                ConnectionFailureType.UnableToConnect, "Connection refused"));

        var sut = CreateSut();

        // Act
        await sut.IsApproachingLimitAsync("openrouter");

        // Assert — when Redis fails early, the catch returns empty status
        // so LimitRpm=0 and the method returns false before needing usage service.
        // But since the exception is caught inside GetCurrentStatusAsync, the usage
        // service call (which is also inside the try block) is never reached.
        _usageServiceMock.Verify(
            u => u.GetAccountStatusAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── RecordRequestAsync — happy path batch verification ───────────────────

    [Fact]
    public async Task RecordRequest_WithTokens_CallsRedisInBatch()
    {
        // Arrange — set up batch operations to complete successfully
        SetupDefaultBatchTasks();

        var sut = CreateSut();

        // Act
        await sut.RecordRequestAsync("openrouter", "gpt-4o", totalTokens: 1200);

        // Assert — batch was created and executed
        _dbMock.Verify(d => d.CreateBatch(It.IsAny<object>()), Times.Once);
        _batchMock.Verify(b => b.Execute(), Times.Once);

        // RPM: ZADD + ZREMRANGEBYSCORE + EXPIRE
        _batchMock.Verify(b => b.SortedSetAddAsync(
            It.Is<RedisKey>(k => k.ToString() == "openrouter:rpm:openrouter"),
            It.IsAny<RedisValue>(), It.IsAny<double>(),
            It.IsAny<SortedSetWhen>(), It.IsAny<CommandFlags>()), Times.Once);

        _batchMock.Verify(b => b.SortedSetRemoveRangeByScoreAsync(
            It.Is<RedisKey>(k => k.ToString() == "openrouter:rpm:openrouter"),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<Exclude>(), It.IsAny<CommandFlags>()), Times.Once);

        _batchMock.Verify(b => b.KeyExpireAsync(
            It.Is<RedisKey>(k => k.ToString() == "openrouter:rpm:openrouter"),
            It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()), Times.Once);

        // TPM: ZADD + ZREMRANGEBYSCORE + EXPIRE (because totalTokens > 0)
        _batchMock.Verify(b => b.SortedSetAddAsync(
            It.Is<RedisKey>(k => k.ToString() == "openrouter:tpm:openrouter"),
            It.IsAny<RedisValue>(), It.IsAny<double>(),
            It.IsAny<SortedSetWhen>(), It.IsAny<CommandFlags>()), Times.Once);

        _batchMock.Verify(b => b.SortedSetRemoveRangeByScoreAsync(
            It.Is<RedisKey>(k => k.ToString() == "openrouter:tpm:openrouter"),
            It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<Exclude>(), It.IsAny<CommandFlags>()), Times.Once);

        _batchMock.Verify(b => b.KeyExpireAsync(
            It.Is<RedisKey>(k => k.ToString() == "openrouter:tpm:openrouter"),
            It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RecordRequest_ZeroTokens_SkipsTpmBatch()
    {
        // Arrange
        SetupDefaultBatchTasks();

        var sut = CreateSut();

        // Act
        await sut.RecordRequestAsync("openrouter", "gpt-4o", totalTokens: 0);

        // Assert — RPM operations still happen
        _batchMock.Verify(b => b.SortedSetAddAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("rpm:")),
            It.IsAny<RedisValue>(), It.IsAny<double>(),
            It.IsAny<SortedSetWhen>(), It.IsAny<CommandFlags>()), Times.Once);

        // TPM operations should NOT happen
        _batchMock.Verify(b => b.SortedSetAddAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("tpm:")),
            It.IsAny<RedisValue>(), It.IsAny<double>(),
            It.IsAny<SortedSetWhen>(), It.IsAny<CommandFlags>()), Times.Never);

        _batchMock.Verify(b => b.Execute(), Times.Once);
    }

    // ─── Helper ────────────────────────────────────────────────────────────────

    private void SetupDefaultBatchTasks()
    {
        _batchMock
            .Setup(b => b.SortedSetAddAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(),
                It.IsAny<SortedSetWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _batchMock
            .Setup(b => b.SortedSetRemoveRangeByScoreAsync(
                It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<Exclude>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(0L);

        _batchMock
            .Setup(b => b.KeyExpireAsync(
                It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(),
                It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
    }
}
