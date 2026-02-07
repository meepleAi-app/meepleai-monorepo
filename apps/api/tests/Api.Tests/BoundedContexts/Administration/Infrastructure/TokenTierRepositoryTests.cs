using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure;

/// <summary>
/// Integration tests for TokenTierRepository (Issue #3786)
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public sealed class TokenTierRepositoryTests : IClassFixture<SharedTestcontainersFixture>, IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName;
    private string? _connectionString;

    public TokenTierRepositoryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _databaseName = $"test_token_tier_{Guid.NewGuid():N}";
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
    public async Task AddAsync_WithValidTier_ShouldPersistToDatabase()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new TokenTierRepository(dbContext);
        var tier = TokenTier.CreateFreeTier();

        // Act
        await repository.AddAsync(tier);
        await dbContext.SaveChangesAsync();

        // Assert
        var retrieved = await repository.GetByIdAsync(tier.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be(TierName.Free);
    }

    [Fact]
    public async Task GetAllTiersAsync_WithMultipleTiers_ShouldReturnAll()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new TokenTierRepository(dbContext);
        await repository.AddAsync(TokenTier.CreateFreeTier());
        await repository.AddAsync(TokenTier.CreateBasicTier());
        await dbContext.SaveChangesAsync();

        // Act
        var tiers = await repository.GetAllTiersAsync();

        // Assert
        tiers.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetActiveTiersAsync_WithInactiveTier_ShouldReturnOnlyActive()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new TokenTierRepository(dbContext);
        var activeTier = TokenTier.CreateFreeTier();
        var inactiveTier = TokenTier.CreateBasicTier();
        inactiveTier.Deactivate();

        await repository.AddAsync(activeTier);
        await repository.AddAsync(inactiveTier);
        await dbContext.SaveChangesAsync();

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
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new TokenTierRepository(dbContext);
        var tier = TokenTier.CreateFreeTier();
        await repository.AddAsync(tier);
        await dbContext.SaveChangesAsync();

        var newLimits = TierLimits.Create(15000, 750, 50, 30, 10, 2);
        tier.UpdateLimits(newLimits);

        // Act
        await repository.UpdateAsync(tier);
        await dbContext.SaveChangesAsync();

        // Assert
        var updated = await repository.GetByIdAsync(tier.Id);
        updated!.Limits.TokensPerMonth.Should().Be(15000);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteTier()
    {
        // Arrange
        using var dbContext = _fixture.CreateDbContext(_connectionString!);
        var repository = new TokenTierRepository(dbContext);
        var tier = TokenTier.CreateFreeTier();
        await repository.AddAsync(tier);
        await dbContext.SaveChangesAsync();

        // Act
        await repository.DeleteAsync(tier.Id);
        await dbContext.SaveChangesAsync();

        // Assert
        var allTiers = await repository.GetAllTiersAsync();
        var activeTiers = await repository.GetAllActiveAsync();

        allTiers.Should().Contain(t => t.Id == tier.Id); // Still in DB
        activeTiers.Should().NotContain(t => t.Id == tier.Id); // Not in active list
    }
}
