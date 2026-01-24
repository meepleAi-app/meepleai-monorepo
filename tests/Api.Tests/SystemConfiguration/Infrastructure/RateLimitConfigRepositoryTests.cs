using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SystemConfiguration;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.SystemConfiguration.Infrastructure;

/// <summary>
/// Integration tests for RateLimitConfigRepository with in-memory database.
/// Tests CRUD operations, tier-based queries, and active configuration filtering.
/// ISSUE-2809: Infrastructure - SystemConfiguration Rate Limit Entities
/// </summary>
public sealed class RateLimitConfigRepositoryTests : IAsyncLifetime
{
    private readonly DbContextOptions<MeepleAiDbContext> _options;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IDomainEventCollector> _mockEventCollector;
    private MeepleAiDbContext _dbContext = null!;
    private RateLimitConfigRepository _repository = null!;

    public RateLimitConfigRepositoryTests()
    {
        _options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"RateLimitConfigTest_{Guid.NewGuid()}")
            .Options;

        _mockMediator = new Mock<IMediator>();
        _mockEventCollector = new Mock<IDomainEventCollector>();
        _mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());
    }

    public async Task InitializeAsync()
    {
        _dbContext = new MeepleAiDbContext(_options, _mockMediator.Object, _mockEventCollector.Object);
        _repository = new RateLimitConfigRepository(_dbContext, _mockEventCollector.Object);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public void Constructor_CreatesRepository()
    {
        // Assert
        _repository.Should().NotBeNull();
    }

    [Fact]
    public void RepositoryInterface_ImplementsIRateLimitConfigRepository()
    {
        // Assert
        typeof(RateLimitConfigRepository).Should().BeAssignableTo<IRateLimitConfigRepository>();
    }

    [Fact]
    public async Task AddAsync_PersistsConfigToDatabaseSuccessfully()
    {
        // Arrange
        var config = ShareRequestLimitConfig.Create(
            UserTier.Free,
            maxPendingRequests: 2,
            maxRequestsPerMonth: 5,
            cooldownAfterRejection: TimeSpan.FromDays(7));

        // Act
        await _repository.AddAsync(config);
        await _dbContext.SaveChangesAsync();

        // Assert
        var persisted = await _dbContext.ShareRequestLimitConfigs
            .FirstOrDefaultAsync(c => c.Id == config.Id);

        persisted.Should().NotBeNull();
        persisted!.Tier.Should().Be((int)UserTier.Free);
        persisted.MaxPendingRequests.Should().Be(2);
        persisted.MaxRequestsPerMonth.Should().Be(5);
        persisted.CooldownAfterRejectionSeconds.Should().Be((long)TimeSpan.FromDays(7).TotalSeconds);
        persisted.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsConfigWhenExists()
    {
        // Arrange
        var config = ShareRequestLimitConfig.Create(
            UserTier.Premium,
            maxPendingRequests: 5,
            maxRequestsPerMonth: 15,
            cooldownAfterRejection: TimeSpan.FromDays(3));

        await _repository.AddAsync(config);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(config.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(config.Id);
        result.Tier.Should().Be(UserTier.Premium);
        result.MaxPendingRequests.Should().Be(5);
        result.MaxRequestsPerMonth.Should().Be(15);
        result.CooldownAfterRejection.Should().Be(TimeSpan.FromDays(3));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenNotExists()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByTierAsync_ReturnsActiveConfigForTier()
    {
        // Arrange
        var freeConfig = ShareRequestLimitConfig.Create(
            UserTier.Free,
            maxPendingRequests: 2,
            maxRequestsPerMonth: 5,
            cooldownAfterRejection: TimeSpan.FromDays(7));

        var premiumConfig = ShareRequestLimitConfig.Create(
            UserTier.Premium,
            maxPendingRequests: 5,
            maxRequestsPerMonth: 15,
            cooldownAfterRejection: TimeSpan.FromDays(3));

        await _repository.AddAsync(freeConfig);
        await _repository.AddAsync(premiumConfig);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTierAsync(UserTier.Free);

        // Assert
        result.Should().NotBeNull();
        result!.Tier.Should().Be(UserTier.Free);
        result.MaxPendingRequests.Should().Be(2);
    }

    [Fact]
    public async Task GetByTierAsync_ReturnsNullForInactiveTier()
    {
        // Arrange
        var config = ShareRequestLimitConfig.Create(
            UserTier.Pro,
            maxPendingRequests: 10,
            maxRequestsPerMonth: 30,
            cooldownAfterRejection: TimeSpan.FromDays(1));

        config.Deactivate(); // Deactivate the config

        await _repository.AddAsync(config);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTierAsync(UserTier.Pro);

        // Assert
        result.Should().BeNull("because inactive configs should not be returned");
    }

    [Fact]
    public async Task GetAllActiveAsync_ReturnsOnlyActiveConfigs()
    {
        // Arrange
        var activeConfig1 = ShareRequestLimitConfig.Create(
            UserTier.Free, 2, 5, TimeSpan.FromDays(7));

        var activeConfig2 = ShareRequestLimitConfig.Create(
            UserTier.Premium, 5, 15, TimeSpan.FromDays(3));

        var inactiveConfig = ShareRequestLimitConfig.Create(
            UserTier.Pro, 10, 30, TimeSpan.FromDays(1));
        inactiveConfig.Deactivate();

        await _repository.AddAsync(activeConfig1);
        await _repository.AddAsync(activeConfig2);
        await _repository.AddAsync(inactiveConfig);
        await _dbContext.SaveChangesAsync();

        // Act
        var results = await _repository.GetAllActiveAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(c => c.IsActive.Should().BeTrue());
        results.Should().Contain(c => c.Tier == UserTier.Free);
        results.Should().Contain(c => c.Tier == UserTier.Premium);
        results.Should().NotContain(c => c.Tier == UserTier.Pro);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllConfigsIncludingInactive()
    {
        // Arrange
        var activeConfig = ShareRequestLimitConfig.Create(
            UserTier.Free, 2, 5, TimeSpan.FromDays(7));

        var inactiveConfig = ShareRequestLimitConfig.Create(
            UserTier.Premium, 5, 15, TimeSpan.FromDays(3));
        inactiveConfig.Deactivate();

        await _repository.AddAsync(activeConfig);
        await _repository.AddAsync(inactiveConfig);
        await _dbContext.SaveChangesAsync();

        // Act
        var results = await _repository.GetAllAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(c => c.IsActive && c.Tier == UserTier.Free);
        results.Should().Contain(c => !c.IsActive && c.Tier == UserTier.Premium);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChangesSuccessfully()
    {
        // Arrange
        var config = ShareRequestLimitConfig.Create(
            UserTier.Free, 2, 5, TimeSpan.FromDays(7));

        await _repository.AddAsync(config);
        await _dbContext.SaveChangesAsync();

        // Act - Update the config
        config.Update(
            maxPendingRequests: 3,
            maxRequestsPerMonth: 10,
            cooldownAfterRejection: TimeSpan.FromDays(5));

        await _repository.UpdateAsync(config);
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _repository.GetByIdAsync(config.Id);
        updated.Should().NotBeNull();
        updated!.MaxPendingRequests.Should().Be(3);
        updated.MaxRequestsPerMonth.Should().Be(10);
        updated.CooldownAfterRejection.Should().Be(TimeSpan.FromDays(5));
    }

    [Fact]
    public async Task DeleteAsync_RemovesConfigFromDatabase()
    {
        // Arrange
        var config = ShareRequestLimitConfig.Create(
            UserTier.Free, 2, 5, TimeSpan.FromDays(7));

        await _repository.AddAsync(config);
        await _dbContext.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(config);
        await _dbContext.SaveChangesAsync();

        // Assert
        var deleted = await _dbContext.ShareRequestLimitConfigs
            .FirstOrDefaultAsync(c => c.Id == config.Id);
        deleted.Should().BeNull("because the config should be deleted from database");
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrueWhenConfigExists()
    {
        // Arrange
        var config = ShareRequestLimitConfig.Create(
            UserTier.Free, 2, 5, TimeSpan.FromDays(7));

        await _repository.AddAsync(config);
        await _dbContext.SaveChangesAsync();

        // Act
        var exists = await _repository.ExistsAsync(config.Id);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalseWhenConfigDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var exists = await _repository.ExistsAsync(nonExistentId);

        // Assert
        exists.Should().BeFalse();
    }
}
