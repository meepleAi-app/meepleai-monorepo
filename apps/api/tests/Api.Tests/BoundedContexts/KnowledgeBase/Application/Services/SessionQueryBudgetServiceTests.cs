using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for SessionQueryBudgetService (E4-3: Session Query Budget).
/// Verifies per-session AI query tracking and tier-based budget enforcement.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class SessionQueryBudgetServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();
    private readonly Mock<ITierEnforcementService> _tierServiceMock = new();
    private readonly Mock<ILogger<SessionQueryBudgetService>> _loggerMock = new();
    private readonly SessionQueryBudgetService _sut;

    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public SessionQueryBudgetServiceTests()
    {
        _redisMock
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        _sut = new SessionQueryBudgetService(
            _redisMock.Object,
            _tierServiceMock.Object,
            _loggerMock.Object);
    }

    private void SetupTierLimits(TierLimits limits)
    {
        _tierServiceMock
            .Setup(t => t.GetLimitsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(limits);
    }

    private void SetupRedisValue(int value)
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)value.ToString());
    }

    #region CanQueryAsync Tests

    [Fact]
    public async Task CanQuery_UnderBudget_ReturnsTrue()
    {
        // Arrange — 5 queries used, free tier max 30
        SetupTierLimits(TierLimits.FreeTier);
        SetupRedisValue(5);

        // Act
        var result = await _sut.CanQueryAsync(SessionId, UserId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanQuery_AtBudget_ReturnsFalse()
    {
        // Arrange — 30 queries used, free tier max 30
        SetupTierLimits(TierLimits.FreeTier);
        SetupRedisValue(30);

        // Act
        var result = await _sut.CanQueryAsync(SessionId, UserId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanQuery_OverBudget_ReturnsFalse()
    {
        // Arrange — 31 queries used, free tier max 30
        SetupTierLimits(TierLimits.FreeTier);
        SetupRedisValue(31);

        // Act
        var result = await _sut.CanQueryAsync(SessionId, UserId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanQuery_FreeTier_UsesLowerLimit()
    {
        // Arrange — 29 queries used, free tier max 30 → still under
        SetupTierLimits(TierLimits.FreeTier);
        SetupRedisValue(29);

        // Act
        var result = await _sut.CanQueryAsync(SessionId, UserId);

        // Assert
        result.Should().BeTrue();
        TierLimits.FreeTier.MaxSessionQueries.Should().Be(30);
    }

    [Fact]
    public async Task CanQuery_PremiumTier_UsesHigherLimit()
    {
        // Arrange — 100 queries used, premium tier max 150 → still under
        SetupTierLimits(TierLimits.PremiumTier);
        SetupRedisValue(100);

        // Act
        var result = await _sut.CanQueryAsync(SessionId, UserId);

        // Assert
        result.Should().BeTrue();
        TierLimits.PremiumTier.MaxSessionQueries.Should().Be(150);
    }

    [Fact]
    public async Task CanQuery_UsesCorrectRedisKey()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        SetupTierLimits(TierLimits.FreeTier);
        SetupRedisValue(0);

        // Act
        await _sut.CanQueryAsync(sessionId, UserId);

        // Assert — verify the correct key format was used
        _dbMock.Verify(d => d.StringGetAsync(
            (RedisKey)$"session:queries:{sessionId}",
            It.IsAny<CommandFlags>()), Times.Once);
    }

    #endregion

    #region RecordQueryAsync Tests

    [Fact]
    public async Task RecordQuery_IncrementsCounter()
    {
        // Arrange
        _dbMock
            .Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), 1, It.IsAny<CommandFlags>()))
            .ReturnsAsync(5);

        // Act
        await _sut.RecordQueryAsync(SessionId);

        // Assert
        _dbMock.Verify(d => d.StringIncrementAsync(
            (RedisKey)$"session:queries:{SessionId}",
            1,
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RecordQuery_FirstQuery_SetsTtl()
    {
        // Arrange — increment returns 1 (first query)
        _dbMock
            .Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), 1, It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);

        _dbMock
            .Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _sut.RecordQueryAsync(SessionId);

        // Assert — TTL should be set to 24 hours
        _dbMock.Verify(d => d.KeyExpireAsync(
            (RedisKey)$"session:queries:{SessionId}",
            TimeSpan.FromHours(24),
            It.IsAny<ExpireWhen>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RecordQuery_SubsequentQuery_DoesNotResetTtl()
    {
        // Arrange — increment returns 5 (not first query)
        _dbMock
            .Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), 1, It.IsAny<CommandFlags>()))
            .ReturnsAsync(5);

        // Act
        await _sut.RecordQueryAsync(SessionId);

        // Assert — KeyExpireAsync should NOT be called
        _dbMock.Verify(d => d.KeyExpireAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CommandFlags>()), Times.Never);
    }

    #endregion

    #region GetUsageAsync Tests

    [Fact]
    public async Task GetUsage_ReturnsCorrectSnapshot()
    {
        // Arrange — 10 queries used, free tier max 30
        SetupTierLimits(TierLimits.FreeTier);
        SetupRedisValue(10);

        // Act
        var usage = await _sut.GetUsageAsync(SessionId, UserId);

        // Assert
        usage.QueriesUsed.Should().Be(10);
        usage.QueriesMax.Should().Be(30);
        usage.CanQuery.Should().BeTrue();
    }

    [Fact]
    public async Task GetUsage_AtLimit_CanQueryIsFalse()
    {
        // Arrange — 30 queries used, free tier max 30
        SetupTierLimits(TierLimits.FreeTier);
        SetupRedisValue(30);

        // Act
        var usage = await _sut.GetUsageAsync(SessionId, UserId);

        // Assert
        usage.QueriesUsed.Should().Be(30);
        usage.QueriesMax.Should().Be(30);
        usage.CanQuery.Should().BeFalse();
    }

    [Fact]
    public async Task GetUsage_ZeroQueries_ReturnsEmptyUsage()
    {
        // Arrange — no queries yet
        SetupTierLimits(TierLimits.PremiumTier);
        SetupRedisValue(0);

        // Act
        var usage = await _sut.GetUsageAsync(SessionId, UserId);

        // Assert
        usage.QueriesUsed.Should().Be(0);
        usage.QueriesMax.Should().Be(150);
        usage.CanQuery.Should().BeTrue();
    }

    #endregion
}
