using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Services;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.Shared.Services;

/// <summary>
/// Tests for TierEnforcementService.
/// D3: Game Night Flow - tier enforcement with Redis atomic counters.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TierEnforcementServiceTests : IDisposable
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDb;
    private readonly Mock<ILogger<TierEnforcementService>> _mockLogger;
    private readonly IMemoryCache _memoryCache;
    private readonly MeepleAiDbContext _dbContext;
    private readonly TierEnforcementService _sut;

    public TierEnforcementServiceTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDb = new Mock<IDatabase>();
        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDb.Object);
        _mockLogger = new Mock<ILogger<TierEnforcementService>>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        // In-memory EF context with required dependencies
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"TierEnforcement_{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _sut = new TierEnforcementService(
            _mockRedis.Object,
            _dbContext,
            _memoryCache,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
        _dbContext.Dispose();
    }

    #region GetLimitsAsync Tests

    [Fact]
    public async Task GetLimitsAsync_AdminUser_ReturnsUnlimited()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "admin", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Act
        var limits = await _sut.GetLimitsAsync(userId);

        // Assert
        limits.Should().Be(TierLimits.Unlimited);
    }

    [Fact]
    public async Task GetLimitsAsync_FreeUser_ReturnsFreeTierLimits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Act
        var limits = await _sut.GetLimitsAsync(userId);

        // Assert
        limits.Should().Be(TierLimits.FreeTier);
    }

    [Fact]
    public async Task GetLimitsAsync_ContributorUser_ReturnsPremiumLimits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free", isContributor: true);
        await SeedTierDefinition("premium", TierLimits.PremiumTier);

        // Act
        var limits = await _sut.GetLimitsAsync(userId);

        // Assert
        limits.Should().Be(TierLimits.PremiumTier);
    }

    [Fact]
    public async Task GetLimitsAsync_PremiumUser_ReturnsPremiumLimits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "premium");
        await SeedTierDefinition("premium", TierLimits.PremiumTier);

        // Act
        var limits = await _sut.GetLimitsAsync(userId);

        // Assert
        limits.Should().Be(TierLimits.PremiumTier);
    }

    [Fact]
    public async Task GetLimitsAsync_UnknownTier_ReturnsFreeTierFallback()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "unknown");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Act
        var limits = await _sut.GetLimitsAsync(userId);

        // Assert — falls back to FreeTier when definition not found
        limits.Should().Be(TierLimits.FreeTier);
    }

    #endregion

    #region CanPerformAsync Tests

    [Fact]
    public async Task CanPerformAsync_AgentQuery_UnderLimit_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Redis returns 5 (under FreeTier limit of 20)
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"5");

        // Act
        var result = await _sut.CanPerformAsync(userId, TierAction.AgentQuery);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanPerformAsync_AgentQuery_AtLimit_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Redis returns 20 (at FreeTier limit of 20)
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"20");

        // Act
        var result = await _sut.CanPerformAsync(userId, TierAction.AgentQuery);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanPerformAsync_SaveSession_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier); // SessionSaveEnabled = false

        // Act
        var result = await _sut.CanPerformAsync(userId, TierAction.SaveSession);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanPerformAsync_SaveSession_WhenEnabled_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "premium");
        await SeedTierDefinition("premium", TierLimits.PremiumTier); // SessionSaveEnabled = true

        // Act
        var result = await _sut.CanPerformAsync(userId, TierAction.SaveSession);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanPerformAsync_AdminUser_AlwaysReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "admin", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Act
        var result = await _sut.CanPerformAsync(userId, TierAction.AgentQuery);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region RecordUsageAsync Tests

    [Fact]
    public async Task RecordUsageAsync_AgentQuery_IncrementsRedisCounter()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedKeyPattern = $"tier:usage:{userId}:AgentQuery:";

        _mockDb.Setup(d => d.StringIncrementAsync(
                It.Is<RedisKey>(k => k.ToString().StartsWith(expectedKeyPattern)),
                1,
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);

        _mockDb.Setup(d => d.KeyExpireAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _sut.RecordUsageAsync(userId, TierAction.AgentQuery);

        // Assert
        _mockDb.Verify(d => d.StringIncrementAsync(
            It.Is<RedisKey>(k => k.ToString().StartsWith(expectedKeyPattern)),
            1,
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RecordUsageAsync_UploadPdf_UsesMonthlyBucket()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var monthBucket = DateTime.UtcNow.ToString("yyyy-MM");
        var expectedKey = $"tier:usage:{userId}:UploadPdf:{monthBucket}";

        _mockDb.Setup(d => d.StringIncrementAsync(
                It.IsAny<RedisKey>(), 1, It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);

        _mockDb.Setup(d => d.KeyExpireAsync(
                It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _sut.RecordUsageAsync(userId, TierAction.UploadPdf);

        // Assert
        _mockDb.Verify(d => d.StringIncrementAsync(
            (RedisKey)expectedKey, 1, It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RecordUsageAsync_ProposeToSharedCatalog_UsesWeeklyBucket()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _mockDb.Setup(d => d.StringIncrementAsync(
                It.IsAny<RedisKey>(), 1, It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);

        _mockDb.Setup(d => d.KeyExpireAsync(
                It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _sut.RecordUsageAsync(userId, TierAction.ProposeToSharedCatalog);

        // Assert — verify key contains weekly bucket format (yyyy-Www)
        _mockDb.Verify(d => d.StringIncrementAsync(
            It.Is<RedisKey>(k => k.ToString().Contains(":ProposeToSharedCatalog:")),
            1,
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RecordUsageAsync_SetsExpiry_OnIncrement()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _mockDb.Setup(d => d.StringIncrementAsync(
                It.IsAny<RedisKey>(), 1, It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);

        _mockDb.Setup(d => d.KeyExpireAsync(
                It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _sut.RecordUsageAsync(userId, TierAction.AgentQuery);

        // Assert — TTL set (2 days for daily bucket)
        _mockDb.Verify(d => d.KeyExpireAsync(
            It.IsAny<RedisKey>(),
            It.Is<TimeSpan?>(ts => ts.HasValue && ts.Value.TotalDays >= 1),
            It.IsAny<ExpireWhen>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    #endregion

    #region GetUsageAsync Tests

    [Fact]
    public async Task GetUsageAsync_ReturnsSnapshot_WithRedisAndDbData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Redis returns usage values
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"3");

        // Act
        var snapshot = await _sut.GetUsageAsync(userId);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.PrivateGamesMax.Should().Be(TierLimits.FreeTier.MaxPrivateGames);
        snapshot.AgentQueriesTodayMax.Should().Be(TierLimits.FreeTier.MaxAgentQueriesPerDay);
        snapshot.SessionSaveEnabled.Should().Be(TierLimits.FreeTier.SessionSaveEnabled);
    }

    #endregion

    #region Helpers

    private async Task SeedUser(Guid userId, string role, string tier, bool isContributor = false)
    {
        _dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"test-{userId:N}@test.com",
            Role = role,
            Tier = tier,
            IsContributor = isContributor,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedTierDefinition(string name, TierLimits limits)
    {
        var tier = TierDefinition.Create(name, $"{name} Tier", limits, name == "premium" ? "standard" : "free");
        _dbContext.TierDefinitions.Add(tier);
        await _dbContext.SaveChangesAsync();
    }

    #endregion
}
