using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure;

/// <summary>
/// Integration tests for TokenTrackingService (Issue #3786)
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public sealed class TokenTrackingServiceTests : IClassFixture<SharedTestcontainersFixture>, IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly Mock<IDomainEventCollector> _eventCollectorMock = new();
    private readonly string _databaseName;
    private string? _connectionString;

    public TokenTrackingServiceTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _databaseName = $"test_token_tracking_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        // Create schema from model (EnsureCreatedAsync works for isolated test databases)
        using var dbContext = _fixture.CreateDbContext(_connectionString);
        await dbContext.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (!string.IsNullOrEmpty(_databaseName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_databaseName);
        }
    }

    [Fact]
    public async Task TrackUsageAsync_ForNewUser_ShouldCreateUsageRecord()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var tierRepo = new TokenTierRepository(dbContext, _eventCollectorMock.Object);
        var usageRepo = new UserTokenUsageRepository(dbContext, _eventCollectorMock.Object);
        var mockCache = new Mock<IHybridCacheService>();
        var mockLogger = new Mock<ILogger<TokenTrackingService>>();
        var service = new TokenTrackingService(usageRepo, tierRepo, mockLogger.Object, mockCache.Object);

        // Ensure Free tier exists
        var freeTier = TokenTier.CreateFreeTier();
        await tierRepo.AddAsync(freeTier);
        await dbContext.SaveChangesAsync();

        var userId = Guid.NewGuid();

        // Act
        var (exceeded, remaining) = await service.TrackUsageAsync(userId, 1000, 0.05m);

        // Assert
        exceeded.Should().BeFalse();
        remaining.Should().Be(9000); // 10K - 1K = 9K
    }

    [Fact]
    public async Task TrackUsageAsync_At80Percent_ShouldLogWarning()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var tierRepo = new TokenTierRepository(dbContext, _eventCollectorMock.Object);
        var usageRepo = new UserTokenUsageRepository(dbContext, _eventCollectorMock.Object);
        var mockCache = new Mock<IHybridCacheService>();
        var mockLogger = new Mock<ILogger<TokenTrackingService>>();
        var service = new TokenTrackingService(usageRepo, tierRepo, mockLogger.Object, mockCache.Object);

        var freeTier = TokenTier.CreateFreeTier();
        await tierRepo.AddAsync(freeTier);
        await dbContext.SaveChangesAsync();

        var userId = Guid.NewGuid();
        var usage = UserTokenUsage.Create(userId, freeTier.Id);
        await usageRepo.AddAsync(usage);
        await dbContext.SaveChangesAsync();

        // Act - Reach 80% (8000/10000)
        await service.TrackUsageAsync(userId, 8000, 4m);

        // Assert
        var updated = await usageRepo.GetByUserIdAsync(userId);
        updated!.IsNearLimit.Should().BeTrue();
        updated.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task TrackUsageAsync_At100Percent_ShouldBlockUser()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var tierRepo = new TokenTierRepository(dbContext, _eventCollectorMock.Object);
        var usageRepo = new UserTokenUsageRepository(dbContext, _eventCollectorMock.Object);
        var mockCache = new Mock<IHybridCacheService>();
        var mockLogger = new Mock<ILogger<TokenTrackingService>>();
        var service = new TokenTrackingService(usageRepo, tierRepo, mockLogger.Object, mockCache.Object);

        var freeTier = TokenTier.CreateFreeTier();
        await tierRepo.AddAsync(freeTier);
        await dbContext.SaveChangesAsync();

        var userId = Guid.NewGuid();
        var usage = UserTokenUsage.Create(userId, freeTier.Id);
        await usageRepo.AddAsync(usage);
        await dbContext.SaveChangesAsync();

        // Act - Reach 100% (10000/10000)
        var (exceeded, remaining) = await service.TrackUsageAsync(userId, 10000, 5m);

        // Assert
        exceeded.Should().BeTrue();
        remaining.Should().Be(0);

        var updated = await usageRepo.GetByUserIdAsync(userId);
        updated!.IsBlocked.Should().BeTrue();
    }

    [Fact]
    public async Task CheckLimitsAsync_ForUserBelowLimit_ShouldReturnNotExceeded()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var tierRepo = new TokenTierRepository(dbContext, _eventCollectorMock.Object);
        var usageRepo = new UserTokenUsageRepository(dbContext, _eventCollectorMock.Object);
        var mockCache = new Mock<IHybridCacheService>();
        var mockLogger = new Mock<ILogger<TokenTrackingService>>();

        // Setup cache mock to bypass caching and execute the factory function
        mockCache
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<TokenLimitsCacheDto>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<TokenLimitsCacheDto>>, string[]?, TimeSpan?, CancellationToken>(
                async (key, factory, tags, expiration, ct) => await factory(ct));

        var service = new TokenTrackingService(usageRepo, tierRepo, mockLogger.Object, mockCache.Object);

        var freeTier = TokenTier.CreateFreeTier();
        await tierRepo.AddAsync(freeTier);

        var userId = Guid.NewGuid();
        var usage = UserTokenUsage.Create(userId, freeTier.Id);
        usage.RecordUsage(5000, 2.5m);
        await usageRepo.AddAsync(usage);

        // Act
        var (exceeded, remaining, isBlocked) = await service.CheckLimitsAsync(userId);

        // Assert
        exceeded.Should().BeFalse();
        remaining.Should().Be(5000);
        isBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task ResetMonthlyUsageAsync_ShouldClearUsageCounters()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var tierRepo = new TokenTierRepository(dbContext, _eventCollectorMock.Object);
        var usageRepo = new UserTokenUsageRepository(dbContext, _eventCollectorMock.Object);
        var mockCache = new Mock<IHybridCacheService>();
        var mockLogger = new Mock<ILogger<TokenTrackingService>>();
        var service = new TokenTrackingService(usageRepo, tierRepo, mockLogger.Object, mockCache.Object);

        var freeTier = TokenTier.CreateFreeTier();
        await tierRepo.AddAsync(freeTier);
        await dbContext.SaveChangesAsync();

        var userId = Guid.NewGuid();
        var usage = UserTokenUsage.Create(userId, freeTier.Id);
        usage.RecordUsage(8000, 4m);
        await usageRepo.AddAsync(usage);
        await dbContext.SaveChangesAsync();

        // Act
        await service.ResetMonthlyUsageAsync(userId);

        // Assert
        var reset = await usageRepo.GetByUserIdAsync(userId);
        reset!.TokensUsed.Should().Be(0);
        reset.MessagesCount.Should().Be(0);
        reset.IsBlocked.Should().BeFalse();
    }
}