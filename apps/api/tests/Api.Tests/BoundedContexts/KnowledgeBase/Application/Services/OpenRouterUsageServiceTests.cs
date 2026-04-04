using System.Net;
using System.Text;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for OpenRouterUsageService (Issue #5074).
/// Issue #5076: Phase 1 test suite — covers Redis read/write paths and graceful degradation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5076")]
public sealed class OpenRouterUsageServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _dbMock;
    private readonly Mock<IHttpClientFactory> _httpFactoryMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ILogger<OpenRouterUsageService>> _loggerMock;

    public OpenRouterUsageServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _dbMock = new Mock<IDatabase>();
        _httpFactoryMock = new Mock<IHttpClientFactory>();
        _configMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<OpenRouterUsageService>>();

        _redisMock
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);
    }

    private OpenRouterUsageService CreateSut()
        => new(_httpFactoryMock.Object, _redisMock.Object, _configMock.Object, _loggerMock.Object);

    // ─── Constructor ────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullHttpFactory_ThrowsArgumentNullException()
    {
        Action act = () =>
            new OpenRouterUsageService(null!, _redisMock.Object, _configMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullRedis_ThrowsArgumentNullException()
    {
        Action act = () =>
            new OpenRouterUsageService(_httpFactoryMock.Object, null!, _configMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Action act = () =>
            new OpenRouterUsageService(_httpFactoryMock.Object, _redisMock.Object, _configMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidDependencies_DoesNotThrow()
    {
        var sut = CreateSut();
        sut.Should().NotBeNull();
    }

    // ─── GetAccountStatusAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetAccountStatusAsync_CacheHit_DeserializesAndReturnsStatus()
    {
        // Arrange
        var json = """
            {
                "balance_usd": 4.50,
                "limit_usd": 5.00,
                "usage_usd": 0.50,
                "is_free_tier": false,
                "rate_limit_requests": 200,
                "rate_limit_interval": "minute",
                "last_updated": "2026-02-22T10:00:00Z"
            }
            """;

        _dbMock
            .Setup(d => d.StringGetAsync(It.Is<RedisKey>(k => k.ToString().Contains("account:status")), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)json);

        var sut = CreateSut();

        // Act
        var result = await sut.GetAccountStatusAsync();

        // Assert
        result.Should().NotBeNull();
        result.BalanceUsd.Should().Be(4.50m);
        result.LimitUsd.Should().Be(5.00m);
        result.UsageUsd.Should().Be(0.50m);
        result.IsFreeTier.Should().BeFalse();
        result.RateLimitRequests.Should().Be(200);
        result.RateLimitInterval.Should().Be("minute");
        result.LastUpdated.Should().Be(new DateTime(2026, 2, 22, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetAccountStatusAsync_CacheMiss_ReturnsNull()
    {
        // Arrange
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetAccountStatusAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAccountStatusAsync_RedisThrows_ReturnsNullGracefully()
    {
        // Arrange
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis down"));

        var sut = CreateSut();

        // Act — should not throw
        var result = await sut.GetAccountStatusAsync();

        // Assert
        result.Should().BeNull();
    }

    // ─── GetDailySpendAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetDailySpendAsync_ValueInRedis_ReturnsParsedDecimal()
    {
        // Arrange
        _dbMock
            .Setup(d => d.StringGetAsync(It.Is<RedisKey>(k => k.ToString().Contains("daily_spend")), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"2.3456");

        var sut = CreateSut();

        // Act
        var result = await sut.GetDailySpendAsync();

        // Assert
        result.Should().Be(2.3456m);
    }

    [Fact]
    public async Task GetDailySpendAsync_KeyMissing_ReturnsZero()
    {
        // Arrange
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetDailySpendAsync();

        // Assert
        result.Should().Be(0m);
    }

    [Fact]
    public async Task GetDailySpendAsync_RedisThrows_ReturnsZeroGracefully()
    {
        // Arrange
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis down"));

        var sut = CreateSut();

        // Act — should not throw
        var result = await sut.GetDailySpendAsync();

        // Assert
        result.Should().Be(0m);
    }

    // ─── RecordRequestCostAsync ──────────────────────────────────────────────

    [Fact]
    public async Task RecordRequestCostAsync_PositiveCost_CallsIncrementAndExpire()
    {
        // Arrange
        _dbMock
            .Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(0.01);
        _dbMock
            .Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var sut = CreateSut();

        // Act
        await sut.RecordRequestCostAsync(0.0025m);

        // Assert
        _dbMock.Verify(d => d.StringIncrementAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("daily_spend")),
            It.IsInRange(0.0024, 0.0026, Moq.Range.Inclusive),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RecordRequestCostAsync_ZeroCost_DoesNotCallRedis()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.RecordRequestCostAsync(0m);

        // Assert — Redis should NOT be called for zero-cost requests
        _dbMock.Verify(d => d.StringIncrementAsync(
            It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task RecordRequestCostAsync_NegativeCost_DoesNotCallRedis()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.RecordRequestCostAsync(-0.01m);

        // Assert
        _dbMock.Verify(d => d.StringIncrementAsync(
            It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task RecordRequestCostAsync_RedisThrows_DoesNotPropagate()
    {
        // Arrange
        _dbMock
            .Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis down"));

        var sut = CreateSut();

        // Act — must not throw (graceful degradation)
        var ex = await Record.ExceptionAsync(() => sut.RecordRequestCostAsync(0.01m));

        // Assert
        ex.Should().BeNull();
    }
}
