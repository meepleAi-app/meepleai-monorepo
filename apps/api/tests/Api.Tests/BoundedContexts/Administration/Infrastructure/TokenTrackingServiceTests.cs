using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure;

/// <summary>
/// Integration tests for TokenTrackingService (Issue #3786)
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public sealed class TokenTrackingServiceTests : IClassFixture<SharedTestcontainersFixture>
{
    private readonly SharedTestcontainersFixture _fixture;

    public TokenTrackingServiceTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TrackUsageAsync_ForNewUser_ShouldCreateUsageRecord()
    {
        // Arrange
        await using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITokenTrackingService>();
        var tierRepo = scope.ServiceProvider.GetRequiredService<ITokenTierRepository>();

        // Ensure Free tier exists
        var freeTier = TokenTier.CreateFreeTier();
        await tierRepo.AddAsync(freeTier);

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
        await using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITokenTrackingService>();
        var usageRepo = scope.ServiceProvider.GetRequiredService<IUserTokenUsageRepository>();
        var tierRepo = scope.ServiceProvider.GetRequiredService<ITokenTierRepository>();

        var freeTier = TokenTier.CreateFreeTier();
        await tierRepo.AddAsync(freeTier);

        var userId = Guid.NewGuid();
        var usage = UserTokenUsage.Create(userId, freeTier.Id);
        await usageRepo.AddAsync(usage);

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
        await using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITokenTrackingService>();
        var usageRepo = scope.ServiceProvider.GetRequiredService<IUserTokenUsageRepository>();
        var tierRepo = scope.ServiceProvider.GetRequiredService<ITokenTierRepository>();

        var freeTier = TokenTier.CreateFreeTier();
        await tierRepo.AddAsync(freeTier);

        var userId = Guid.NewGuid();
        var usage = UserTokenUsage.Create(userId, freeTier.Id);
        await usageRepo.AddAsync(usage);

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
        await using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITokenTrackingService>();
        var usageRepo = scope.ServiceProvider.GetRequiredService<IUserTokenUsageRepository>();
        var tierRepo = scope.ServiceProvider.GetRequiredService<ITokenTierRepository>();

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
        await using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITokenTrackingService>();
        var usageRepo = scope.ServiceProvider.GetRequiredService<IUserTokenUsageRepository>();
        var tierRepo = scope.ServiceProvider.GetRequiredService<ITokenTierRepository>();

        var freeTier = TokenTier.CreateFreeTier();
        await tierRepo.AddAsync(freeTier);

        var userId = Guid.NewGuid();
        var usage = UserTokenUsage.Create(userId, freeTier.Id);
        usage.RecordUsage(8000, 4m);
        await usageRepo.AddAsync(usage);

        // Act
        await service.ResetMonthlyUsageAsync(userId);

        // Assert
        var reset = await usageRepo.GetByUserIdAsync(userId);
        reset!.TokensUsed.Should().Be(0);
        reset.MessagesCount.Should().Be(0);
        reset.IsBlocked.Should().BeFalse();
    }
}
