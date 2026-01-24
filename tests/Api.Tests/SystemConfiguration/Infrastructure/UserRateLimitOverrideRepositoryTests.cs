using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
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
/// Integration tests for UserRateLimitOverrideRepository with in-memory database.
/// Tests CRUD operations, user-specific queries, expiration logic, and active override filtering.
/// ISSUE-2809: Infrastructure - SystemConfiguration Rate Limit Entities
/// </summary>
public sealed class UserRateLimitOverrideRepositoryTests : IAsyncLifetime
{
    private readonly DbContextOptions<MeepleAiDbContext> _options;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IDomainEventCollector> _mockEventCollector;
    private MeepleAiDbContext _dbContext = null!;
    private UserRateLimitOverrideRepository _repository = null!;

    public UserRateLimitOverrideRepositoryTests()
    {
        _options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"UserRateLimitOverrideTest_{Guid.NewGuid()}")
            .Options;

        _mockMediator = new Mock<IMediator>();
        _mockEventCollector = new Mock<IDomainEventCollector>();
        _mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());
    }

    public async Task InitializeAsync()
    {
        _dbContext = new MeepleAiDbContext(_options, _mockMediator.Object, _mockEventCollector.Object);
        _repository = new UserRateLimitOverrideRepository(_dbContext, _mockEventCollector.Object);
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
    public void RepositoryInterface_ImplementsIUserRateLimitOverrideRepository()
    {
        // Assert
        typeof(UserRateLimitOverrideRepository).Should().BeAssignableTo<IUserRateLimitOverrideRepository>();
    }

    [Fact]
    public async Task AddAsync_PersistsOverrideToDatabaseSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var userOverride = UserRateLimitOverride.Create(
            userId: userId,
            maxPendingRequests: 20,
            maxRequestsPerMonth: 100,
            cooldownAfterRejection: TimeSpan.FromHours(12),
            expiresAt: DateTime.UtcNow.AddMonths(1),
            reason: "VIP customer",
            createdByAdminId: adminId);

        // Act
        await _repository.AddAsync(userOverride);
        await _dbContext.SaveChangesAsync();

        // Assert
        var persisted = await _dbContext.UserRateLimitOverrides
            .FirstOrDefaultAsync(o => o.Id == userOverride.Id);

        persisted.Should().NotBeNull();
        persisted!.UserId.Should().Be(userId);
        persisted.MaxPendingRequests.Should().Be(20);
        persisted.MaxRequestsPerMonth.Should().Be(100);
        persisted.CooldownAfterRejectionSeconds.Should().Be((long)TimeSpan.FromHours(12).TotalSeconds);
        persisted.Reason.Should().Be("VIP customer");
        persisted.CreatedByAdminId.Should().Be(adminId);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsOverrideWhenExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var userOverride = UserRateLimitOverride.Create(
            userId, 20, 100, TimeSpan.FromHours(12),
            DateTime.UtcNow.AddMonths(1), "VIP", adminId);

        await _repository.AddAsync(userOverride);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(userOverride.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(userOverride.Id);
        result.UserId.Should().Be(userId);
        result.MaxPendingRequests.Should().Be(20);
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
    public async Task GetByUserIdAsync_ReturnsActiveOverrideOnly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var activeOverride = UserRateLimitOverride.Create(
            userId, 20, 100, null,
            expiresAt: DateTime.UtcNow.AddMonths(1), // Future expiration
            reason: "Active", adminId);

        await _repository.AddAsync(activeOverride);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserIdAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.Reason.Should().Be("Active");
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsNullForExpiredOverride()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var expiredOverride = UserRateLimitOverride.Create(
            userId, 20, 100, null,
            expiresAt: DateTime.UtcNow.AddDays(-1), // Expired
            reason: "Expired", adminId);

        await _repository.AddAsync(expiredOverride);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserIdAsync(userId);

        // Assert
        result.Should().BeNull("because the override has expired");
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsOverrideWithNullExpiration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var permanentOverride = UserRateLimitOverride.Create(
            userId, 50, 500, null,
            expiresAt: null, // Never expires
            reason: "Permanent VIP", adminId);

        await _repository.AddAsync(permanentOverride);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserIdAsync(userId);

        // Assert
        result.Should().NotBeNull("because null expiration means permanent override");
        result!.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task HasActiveOverrideAsync_ReturnsTrueForActiveOverride()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var override1 = UserRateLimitOverride.Create(
            userId, 20, 100, null,
            DateTime.UtcNow.AddMonths(1), "Active", adminId);

        await _repository.AddAsync(override1);
        await _dbContext.SaveChangesAsync();

        // Act
        var hasActive = await _repository.HasActiveOverrideAsync(userId);

        // Assert
        hasActive.Should().BeTrue();
    }

    [Fact]
    public async Task HasActiveOverrideAsync_ReturnsFalseForExpiredOverride()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var expiredOverride = UserRateLimitOverride.Create(
            userId, 20, 100, null,
            DateTime.UtcNow.AddDays(-1), "Expired", adminId);

        await _repository.AddAsync(expiredOverride);
        await _dbContext.SaveChangesAsync();

        // Act
        var hasActive = await _repository.HasActiveOverrideAsync(userId);

        // Assert
        hasActive.Should().BeFalse("because the override has expired");
    }

    [Fact]
    public async Task GetAllActiveAsync_ReturnsOnlyNonExpiredOverrides()
    {
        // Arrange
        var adminId = Guid.NewGuid();

        var activeOverride1 = UserRateLimitOverride.Create(
            Guid.NewGuid(), 20, 100, null,
            DateTime.UtcNow.AddMonths(1), "Active 1", adminId);

        var activeOverride2 = UserRateLimitOverride.Create(
            Guid.NewGuid(), 30, 150, null,
            null, "Permanent", adminId); // null = never expires

        var expiredOverride = UserRateLimitOverride.Create(
            Guid.NewGuid(), 10, 50, null,
            DateTime.UtcNow.AddDays(-5), "Expired", adminId);

        await _repository.AddAsync(activeOverride1);
        await _repository.AddAsync(activeOverride2);
        await _repository.AddAsync(expiredOverride);
        await _dbContext.SaveChangesAsync();

        // Act
        var results = await _repository.GetAllActiveAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(o => o.Reason == "Active 1");
        results.Should().Contain(o => o.Reason == "Permanent");
        results.Should().NotContain(o => o.Reason == "Expired");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllOverridesIncludingExpired()
    {
        // Arrange
        var adminId = Guid.NewGuid();

        var activeOverride = UserRateLimitOverride.Create(
            Guid.NewGuid(), 20, 100, null,
            DateTime.UtcNow.AddMonths(1), "Active", adminId);

        var expiredOverride = UserRateLimitOverride.Create(
            Guid.NewGuid(), 10, 50, null,
            DateTime.UtcNow.AddDays(-5), "Expired", adminId);

        await _repository.AddAsync(activeOverride);
        await _repository.AddAsync(expiredOverride);
        await _dbContext.SaveChangesAsync();

        // Act
        var results = await _repository.GetAllAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(o => o.Reason == "Active");
        results.Should().Contain(o => o.Reason == "Expired");
    }

    [Fact]
    public async Task GetAllByUserIdAsync_ReturnsAllUserOverridesRegardlessOfExpiration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var override1 = UserRateLimitOverride.Create(
            userId, 20, 100, null,
            DateTime.UtcNow.AddMonths(1), "Override 1", adminId);

        var override2 = UserRateLimitOverride.Create(
            userId, 30, 150, null,
            DateTime.UtcNow.AddDays(-5), "Override 2 (expired)", adminId);

        var otherUserOverride = UserRateLimitOverride.Create(
            Guid.NewGuid(), 10, 50, null,
            DateTime.UtcNow.AddMonths(1), "Other user", adminId);

        await _repository.AddAsync(override1);
        await _repository.AddAsync(override2);
        await _repository.AddAsync(otherUserOverride);
        await _dbContext.SaveChangesAsync();

        // Act
        var results = await _repository.GetAllByUserIdAsync(userId);

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(o => o.UserId.Should().Be(userId));
        results.Should().Contain(o => o.Reason == "Override 1");
        results.Should().Contain(o => o.Reason == "Override 2 (expired)");
    }

    [Fact]
    public async Task UpdateAsync_PersistsChangesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var userOverride = UserRateLimitOverride.Create(
            userId, 20, 100, null,
            DateTime.UtcNow.AddMonths(1), "Original", adminId);

        await _repository.AddAsync(userOverride);
        await _dbContext.SaveChangesAsync();

        // Act - Update via domain method
        userOverride.Update(
            maxPendingRequests: 50,
            maxRequestsPerMonth: 200,
            cooldownAfterRejection: TimeSpan.FromHours(6),
            expiresAt: DateTime.UtcNow.AddMonths(2),
            reason: "Updated reason");

        await _repository.UpdateAsync(userOverride);
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _repository.GetByIdAsync(userOverride.Id);
        updated.Should().NotBeNull();
        updated!.MaxPendingRequests.Should().Be(50);
        updated.MaxRequestsPerMonth.Should().Be(200);
        updated.CooldownAfterRejection.Should().Be(TimeSpan.FromHours(6));
        updated.Reason.Should().Be("Updated reason");
    }

    [Fact]
    public async Task DeleteAsync_RemovesOverrideFromDatabase()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var userOverride = UserRateLimitOverride.Create(
            userId, 20, 100, null,
            DateTime.UtcNow.AddMonths(1), "To be deleted", adminId);

        await _repository.AddAsync(userOverride);
        await _dbContext.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(userOverride);
        await _dbContext.SaveChangesAsync();

        // Assert
        var deleted = await _dbContext.UserRateLimitOverrides
            .FirstOrDefaultAsync(o => o.Id == userOverride.Id);
        deleted.Should().BeNull("because the override should be deleted from database");
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrueWhenOverrideExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var userOverride = UserRateLimitOverride.Create(
            userId, 20, 100, null,
            DateTime.UtcNow.AddMonths(1), "Exists", adminId);

        await _repository.AddAsync(userOverride);
        await _dbContext.SaveChangesAsync();

        // Act
        var exists = await _repository.ExistsAsync(userOverride.Id);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalseWhenOverrideDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var exists = await _repository.ExistsAsync(nonExistentId);

        // Assert
        exists.Should().BeFalse();
    }
}
