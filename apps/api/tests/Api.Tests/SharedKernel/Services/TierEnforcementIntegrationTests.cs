using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Exceptions;
using Api.SharedKernel.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.SharedKernel.Services;

/// <summary>
/// Integration tests for tier enforcement across handlers.
/// E2-3: Game Night Improvvisata — Enforce Tier Limits on Existing Endpoints.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TierEnforcementIntegrationTests : IDisposable
{
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDb;
    private readonly IMemoryCache _memoryCache;
    private readonly MeepleAiDbContext _dbContext;
    private readonly TierEnforcementService _sut;

    public TierEnforcementIntegrationTests()
    {
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDb = new Mock<IDatabase>();
        _mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDb.Object);
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"TierEnforcementInteg_{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _sut = new TierEnforcementService(
            _mockRedis.Object,
            _dbContext,
            _memoryCache,
            new Mock<ILogger<TierEnforcementService>>().Object);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
        _dbContext.Dispose();
    }

    #region TierLimitExceededException Tests

    [Fact]
    public void TierLimitExceededException_WithCounts_FormatsMessage()
    {
        // Act
        var ex = new TierLimitExceededException("UploadPdf", 3, 3);

        // Assert
        ex.LimitType.Should().Be("UploadPdf");
        ex.Current.Should().Be(3);
        ex.Max.Should().Be(3);
        ex.UpgradeUrl.Should().Be("/pricing");
        ex.Message.Should().Be("Tier limit exceeded: UploadPdf (3/3)");
    }

    [Fact]
    public void TierLimitExceededException_WithMessage_SetsLimitType()
    {
        // Act
        var ex = new TierLimitExceededException("SaveSession", "Session save not available on Free tier.");

        // Assert
        ex.LimitType.Should().Be("SaveSession");
        ex.Message.Should().Be("Session save not available on Free tier.");
        ex.UpgradeUrl.Should().Be("/pricing");
    }

    #endregion

    #region Free User at Limit Gets Denied

    [Fact]
    public async Task FreeUser_AtPdfLimit_CanPerformReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Simulate Redis counter at the limit (3 PDFs for free tier)
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)TierLimits.FreeTier.MaxPdfUploadsPerMonth.ToString());

        // Act
        var canUpload = await _sut.CanPerformAsync(userId, TierAction.UploadPdf);

        // Assert
        canUpload.Should().BeFalse("free user has reached the monthly PDF upload limit");
    }

    [Fact]
    public async Task FreeUser_AtAgentLimit_CanPerformReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // For CreateAgent, the counter-based check is supplemented by DB count
        // Free tier MaxAgents = 1, simulate Redis counter = 1
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)TierLimits.FreeTier.MaxAgents.ToString());

        // Act
        var canCreate = await _sut.CanPerformAsync(userId, TierAction.CreateAgent);

        // Assert
        canCreate.Should().BeFalse("free user has reached the agent creation limit");
    }

    [Fact]
    public async Task FreeUser_SessionSaveDisabled_CanPerformReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        // Act
        var canSave = await _sut.CanPerformAsync(userId, TierAction.SaveSession);

        // Assert
        canSave.Should().BeFalse("free tier does not include session save");
    }

    #endregion

    #region Premium User Passes

    [Fact]
    public async Task PremiumUser_BelowPdfLimit_CanPerformReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "premium");
        await SeedTierDefinition("premium", TierLimits.PremiumTier);

        // Redis counter below limit
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"2");

        // Act
        var canUpload = await _sut.CanPerformAsync(userId, TierAction.UploadPdf);

        // Assert
        canUpload.Should().BeTrue("premium user is below the monthly PDF upload limit");
    }

    [Fact]
    public async Task PremiumUser_SessionSaveEnabled_CanPerformReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "premium");
        await SeedTierDefinition("premium", TierLimits.PremiumTier);

        // Act
        var canSave = await _sut.CanPerformAsync(userId, TierAction.SaveSession);

        // Assert
        canSave.Should().BeTrue("premium tier includes session save");
    }

    [Fact]
    public async Task PremiumUser_BelowAgentLimit_CanPerformReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "premium");
        await SeedTierDefinition("premium", TierLimits.PremiumTier);

        // Redis counter below limit
        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"5");

        // Act
        var canCreate = await _sut.CanPerformAsync(userId, TierAction.CreateAgent);

        // Assert
        canCreate.Should().BeTrue("premium user is below the agent creation limit");
    }

    #endregion

    #region Admin Bypasses All Limits

    [Fact]
    public async Task AdminUser_AlwaysBypassesPdfLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "admin", tier: "free");

        // Act — admin gets unlimited limits, no Redis check needed
        var canUpload = await _sut.CanPerformAsync(userId, TierAction.UploadPdf);

        // Assert
        canUpload.Should().BeTrue("admin users bypass all tier limits");
    }

    [Fact]
    public async Task AdminUser_AlwaysBypassesAgentLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "admin", tier: "free");

        // Act
        var canCreate = await _sut.CanPerformAsync(userId, TierAction.CreateAgent);

        // Assert
        canCreate.Should().BeTrue("admin users bypass all tier limits");
    }

    [Fact]
    public async Task AdminUser_AlwaysBypassesSessionSave()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "admin", tier: "free");

        // Act
        var canSave = await _sut.CanPerformAsync(userId, TierAction.SaveSession);

        // Assert
        canSave.Should().BeTrue("admin users bypass all tier limits");
    }

    #endregion

    #region Contributor Gets Premium Limits

    [Fact]
    public async Task ContributorUser_GetsPremiumLimits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free", isContributor: true);
        await SeedTierDefinition("premium", TierLimits.PremiumTier);

        // Act
        var limits = await _sut.GetLimitsAsync(userId);

        // Assert
        limits.Should().Be(TierLimits.PremiumTier, "contributors are resolved as premium tier");
    }

    #endregion

    #region Usage Snapshot Reflects Limits

    [Fact]
    public async Task GetUsageAsync_FreeUser_ReturnsFreeLimits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUser(userId, role: "user", tier: "free");
        await SeedTierDefinition("free", TierLimits.FreeTier);

        _mockDb.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var snapshot = await _sut.GetUsageAsync(userId);

        // Assert
        snapshot.PdfThisMonthMax.Should().Be(TierLimits.FreeTier.MaxPdfUploadsPerMonth);
        snapshot.AgentsMax.Should().Be(TierLimits.FreeTier.MaxAgents);
        snapshot.SessionSaveEnabled.Should().BeFalse();
        snapshot.AgentQueriesTodayMax.Should().Be(TierLimits.FreeTier.MaxAgentQueriesPerDay);
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
        // Avoid duplicate seeds (premium may already exist from contributor test)
        var existing = await _dbContext.TierDefinitions
            .FirstOrDefaultAsync(t => t.Name == name);
        if (existing is not null) return;

        var tier = TierDefinition.Create(name, $"{name} Tier", limits, name == "premium" ? "standard" : "free");
        _dbContext.TierDefinitions.Add(tier);
        await _dbContext.SaveChangesAsync();
    }

    #endregion
}
