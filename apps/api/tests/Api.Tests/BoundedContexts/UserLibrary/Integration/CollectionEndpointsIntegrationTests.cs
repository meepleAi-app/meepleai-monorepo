using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Integration;

/// <summary>
/// Integration tests for UserCollectionRepository with InMemory database.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class CollectionEndpointsIntegrationTests : IAsyncLifetime
{
    private MeepleAiDbContext _dbContext = null!;
    private UserCollectionRepository _repository = null!;
    private readonly Mock<IDomainEventCollector> _eventCollector;
    private Guid _testUserId;

    public CollectionEndpointsIntegrationTests()
    {
        _eventCollector = TestDbContextFactory.CreateMockEventCollector();
    }

    public async ValueTask InitializeAsync()
    {
        // Create unique database for test isolation
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext($"CollectionTest_{Guid.NewGuid()}");
        _repository = new UserCollectionRepository(
            _dbContext,
            _eventCollector.Object,
            NullLogger<UserCollectionRepository>.Instance);

        // Seed test user
        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    private async Task SeedTestDataAsync()
    {
        _testUserId = Guid.NewGuid();

        var user = new UserEntity
        {
            Id = _testUserId,
            Email = $"test_{Guid.NewGuid()}@example.com",
            DisplayName = "Test User",
            PasswordHash = "hashedpassword123",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    #region AddAsync Tests

    [Theory]
    [InlineData(EntityType.Player)]
    [InlineData(EntityType.Event)]
    [InlineData(EntityType.Session)]
    [InlineData(EntityType.Agent)]
    [InlineData(EntityType.Document)]
    [InlineData(EntityType.ChatSession)]
    public async Task AddAsync_WithValidEntity_AddsToCollection(EntityType entityType)
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var entry = new UserCollectionEntry(Guid.NewGuid(), _testUserId, entityType, entityId);

        // Act
        await _repository.AddAsync(entry);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Assert
        var result = await _repository.GetByUserAndEntityAsync(_testUserId, entityType, entityId);
        result.Should().NotBeNull();
        result!.EntityType.Should().Be(entityType);
        result.EntityId.Should().Be(entityId);
    }

    [Fact]
    public async Task ExistsAsync_WhenAlreadyExists_ReturnsTrue()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var entry = new UserCollectionEntry(Guid.NewGuid(), _testUserId, EntityType.Player, entityId);
        await _repository.AddAsync(entry);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var exists = await _repository.ExistsAsync(_testUserId, EntityType.Player, entityId);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenNotExists_ReturnsFalse()
    {
        // Arrange
        var entityId = Guid.NewGuid();

        // Act
        var exists = await _repository.ExistsAsync(_testUserId, EntityType.Player, entityId);

        // Assert
        exists.Should().BeFalse();
    }

    #endregion

    #region DeleteAsync Tests

    [Theory]
    [InlineData(EntityType.Player)]
    [InlineData(EntityType.Event)]
    [InlineData(EntityType.Session)]
    [InlineData(EntityType.Agent)]
    [InlineData(EntityType.Document)]
    [InlineData(EntityType.ChatSession)]
    public async Task DeleteAsync_WithValidEntity_RemovesFromCollection(EntityType entityType)
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var entry = new UserCollectionEntry(Guid.NewGuid(), _testUserId, entityType, entityId);
        await _repository.AddAsync(entry);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act - Fetch, prepare, delete pattern
        var entryToDelete = await _repository.GetByUserAndEntityAsync(_testUserId, entityType, entityId);
        entryToDelete.Should().NotBeNull();
        entryToDelete!.PrepareForRemoval();
        await _repository.DeleteAsync(entryToDelete);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Assert
        var result = await _repository.GetByUserAndEntityAsync(_testUserId, entityType, entityId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_GetReturnsNull()
    {
        // Arrange
        var entityId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByUserAndEntityAsync(_testUserId, EntityType.Player, entityId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByUserAndEntityAsync Tests

    [Fact]
    public async Task GetByUserAndEntityAsync_WhenNotInCollection_ReturnsNull()
    {
        // Arrange
        var entityId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByUserAndEntityAsync(_testUserId, EntityType.Player, entityId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserAndEntityAsync_WhenInCollection_ReturnsEntry()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var entry = new UserCollectionEntry(Guid.NewGuid(), _testUserId, EntityType.Event, entityId);
        entry.MarkAsFavorite();
        entry.UpdateNotes("Test notes");

        await _repository.AddAsync(entry);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetByUserAndEntityAsync(_testUserId, EntityType.Event, entityId);

        // Assert
        result.Should().NotBeNull();
        result!.EntityId.Should().Be(entityId);
        result.IsFavorite.Should().BeTrue();
        result.Notes.Should().Be("Test notes");
    }

    [Theory]
    [InlineData(EntityType.Player)]
    [InlineData(EntityType.Event)]
    [InlineData(EntityType.Session)]
    [InlineData(EntityType.Agent)]
    [InlineData(EntityType.Document)]
    [InlineData(EntityType.ChatSession)]
    public async Task GetByUserAndEntityAsync_WorksForAllEntityTypes(EntityType entityType)
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var entry = new UserCollectionEntry(Guid.NewGuid(), _testUserId, entityType, entityId);
        await _repository.AddAsync(entry);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetByUserAndEntityAsync(_testUserId, entityType, entityId);

        // Assert
        result.Should().NotBeNull();
        result!.EntityType.Should().Be(entityType);
    }

    #endregion

    #region Cross-Entity Isolation Tests

    [Fact]
    public async Task Collection_DifferentEntityTypes_AreIsolated()
    {
        // Arrange
        var entityId = Guid.NewGuid();

        // Add same ID to Player collection
        var playerEntry = new UserCollectionEntry(Guid.NewGuid(), _testUserId, EntityType.Player, entityId);
        await _repository.AddAsync(playerEntry);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act - Check if same ID is in Event collection (should not be)
        var eventResult = await _repository.GetByUserAndEntityAsync(_testUserId, EntityType.Event, entityId);

        // Assert
        eventResult.Should().BeNull();
    }

    #endregion

    #region End-to-End Workflow Tests

    [Fact]
    public async Task CompleteWorkflow_AddCheckRemove_WorksCorrectly()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var entityType = EntityType.Agent;

        // Step 1: Check initial status (not in collection)
        var initialCheck = await _repository.GetByUserAndEntityAsync(_testUserId, entityType, entityId);
        initialCheck.Should().BeNull();

        // Step 2: Add to collection
        var entry = new UserCollectionEntry(Guid.NewGuid(), _testUserId, entityType, entityId);
        entry.MarkAsFavorite();
        entry.UpdateNotes("My favorite agent");
        await _repository.AddAsync(entry);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Step 3: Verify in collection
        var afterAdd = await _repository.GetByUserAndEntityAsync(_testUserId, entityType, entityId);
        afterAdd.Should().NotBeNull();
        afterAdd!.IsFavorite.Should().BeTrue();

        // Step 4: Remove from collection (fetch, prepare, delete)
        var entryToDelete = await _repository.GetByUserAndEntityAsync(_testUserId, entityType, entityId);
        entryToDelete.Should().NotBeNull();
        entryToDelete!.PrepareForRemoval();
        await _repository.DeleteAsync(entryToDelete);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Step 5: Verify removed
        var finalCheck = await _repository.GetByUserAndEntityAsync(_testUserId, entityType, entityId);
        finalCheck.Should().BeNull();
    }

    #endregion
}
