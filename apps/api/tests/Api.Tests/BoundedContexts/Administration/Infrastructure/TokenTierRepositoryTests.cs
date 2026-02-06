using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure;

/// <summary>
/// Integration tests for TokenTierRepository (Issue #3786)
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public sealed class TokenTierRepositoryTests : IClassFixture<SharedTestcontainersFixture>
{
    private readonly SharedTestcontainersFixture _fixture;

    public TokenTierRepositoryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddAsync_WithValidTier_ShouldPersistToDatabase()
    {
        // Arrange
        await using var scope = _fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITokenTierRepository>();
        var tier = TokenTier.CreateFreeTier();

        // Act
        await repository.AddAsync(tier);

        // Assert
        var retrieved = await repository.GetByIdAsync(tier.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be(TierName.Free);
    }

    [Fact]
    public async Task GetAllTiersAsync_WithMultipleTiers_ShouldReturnAll()
    {
        // Arrange
        await using var scope = _fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITokenTierRepository>();
        await repository.AddAsync(TokenTier.CreateFreeTier());
        await repository.AddAsync(TokenTier.CreateBasicTier());

        // Act
        var tiers = await repository.GetAllTiersAsync();

        // Assert
        tiers.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task GetActiveTiersAsync_WithInactiveTier_ShouldReturnOnlyActive()
    {
        // Arrange
        await using var scope = _fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITokenTierRepository>();
        var activeTier = TokenTier.CreateFreeTier();
        var inactiveTier = TokenTier.CreateBasicTier();
        inactiveTier.Deactivate();

        await repository.AddAsync(activeTier);
        await repository.AddAsync(inactiveTier);

        // Act
        var activeTiers = await repository.GetAllActiveAsync();

        // Assert
        activeTiers.Should().Contain(t => t.Id == activeTier.Id);
        activeTiers.Should().NotContain(t => t.Id == inactiveTier.Id);
    }

    [Fact]
    public async Task UpdateAsync_WithModifiedLimits_ShouldPersistChanges()
    {
        // Arrange
        await using var scope = _fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITokenTierRepository>();
        var tier = TokenTier.CreateFreeTier();
        await repository.AddAsync(tier);

        var newLimits = TierLimits.Create(15000, 750, 50, 30, 10, 2);
        tier.UpdateLimits(newLimits);

        // Act
        await repository.UpdateAsync(tier);

        // Assert
        var updated = await repository.GetByIdAsync(tier.Id);
        updated!.Limits.TokensPerMonth.Should().Be(15000);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteTier()
    {
        // Arrange
        await using var scope = _fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITokenTierRepository>();
        var tier = TokenTier.CreateFreeTier();
        await repository.AddAsync(tier);

        // Act
        await repository.DeleteAsync(tier.Id);

        // Assert
        var allTiers = await repository.GetAllTiersAsync();
        var activeTiers = await repository.GetAllActiveAsync();

        allTiers.Should().Contain(t => t.Id == tier.Id); // Still in DB
        activeTiers.Should().NotContain(t => t.Id == tier.Id); // Not in active list
    }
}
