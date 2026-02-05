using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Infrastructure;

/// <summary>
/// Integration tests for PrivateGameRepository with InMemory database.
/// Issue #3662: Phase 1 - Tests repository operations with database.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class PrivateGameRepositoryIntegrationTests : IAsyncLifetime
{
    private MeepleAiDbContext _dbContext = null!;
    private PrivateGameRepository _repository = null!;
    private readonly Mock<IDomainEventCollector> _eventCollector;

    private Guid _testOwnerId;
    private Guid _otherUserId;

    public PrivateGameRepositoryIntegrationTests()
    {
        _eventCollector = TestDbContextFactory.CreateMockEventCollector();
    }

    public async ValueTask InitializeAsync()
    {
        // Create unique database for test isolation
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext($"PrivateGameRepoTest_{Guid.NewGuid()}");
        _repository = new PrivateGameRepository(_dbContext, _eventCollector.Object);

        // Seed test data
        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    private async Task SeedTestDataAsync()
    {
        _testOwnerId = Guid.NewGuid();
        _otherUserId = Guid.NewGuid();

        // Create test users
        var testUser = new UserEntity
        {
            Id = _testOwnerId,
            Email = $"testowner_{Guid.NewGuid()}@example.com",
            DisplayName = "Test Owner",
            PasswordHash = "hashedpassword123",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(testUser);

        var otherUser = new UserEntity
        {
            Id = _otherUserId,
            Email = $"otheruser_{Guid.NewGuid()}@example.com",
            DisplayName = "Other User",
            PasswordHash = "hashedpassword456",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(otherUser);

        // Create 3 private games for test owner
        var game1 = new PrivateGameEntity
        {
            Id = Guid.NewGuid(),
            OwnerId = _testOwnerId,
            BggId = 12345,
            Title = "BGG Game 1",
            YearPublished = 2020,
            Description = "A BGG-sourced game",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ComplexityRating = 2.5m,
            ImageUrl = "https://example.com/image1.jpg",
            ThumbnailUrl = "https://example.com/thumb1.jpg",
            Source = PrivateGameSource.BoardGameGeek,
            BggSyncedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            IsDeleted = false
        };
        _dbContext.PrivateGames.Add(game1);

        var game2 = new PrivateGameEntity
        {
            Id = Guid.NewGuid(),
            OwnerId = _testOwnerId,
            Title = "Manual Game",
            MinPlayers = 1,
            MaxPlayers = 6,
            PlayingTimeMinutes = 45,
            Source = PrivateGameSource.Manual,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            IsDeleted = false
        };
        _dbContext.PrivateGames.Add(game2);

        var deletedGame = new PrivateGameEntity
        {
            Id = Guid.NewGuid(),
            OwnerId = _testOwnerId,
            Title = "Deleted Game",
            MinPlayers = 2,
            MaxPlayers = 4,
            Source = PrivateGameSource.Manual,
            CreatedAt = DateTime.UtcNow.AddDays(-15),
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow.AddDays(-2)
        };
        _dbContext.PrivateGames.Add(deletedGame);

        // Create 1 private game for other user
        var otherUserGame = new PrivateGameEntity
        {
            Id = Guid.NewGuid(),
            OwnerId = _otherUserId,
            BggId = 54321,
            Title = "Other User Game",
            MinPlayers = 3,
            MaxPlayers = 5,
            Source = PrivateGameSource.BoardGameGeek,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            IsDeleted = false
        };
        _dbContext.PrivateGames.Add(otherUserGame);

        await _dbContext.SaveChangesAsync();
    }

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ExistingGame_ReturnsGame()
    {
        // Arrange
        var existingGameEntity = await _dbContext.PrivateGames.FirstAsync(g => g.Title == "BGG Game 1");

        // Act
        var result = await _repository.GetByIdAsync(existingGameEntity.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(existingGameEntity.Id);
        result.Title.Should().Be("BGG Game 1");
        result.OwnerId.Should().Be(_testOwnerId);
        result.BggId.Should().Be(12345);
        result.Source.Should().Be(PrivateGameSource.BoardGameGeek);
        result.ThumbnailUrl.Should().Be("https://example.com/thumb1.jpg");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentGame_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_DeletedGame_ReturnsNull()
    {
        // Arrange
        var deletedGameEntity = await _dbContext.PrivateGames.IgnoreQueryFilters()
            .FirstAsync(g => g.IsDeleted);

        // Act
        var result = await _repository.GetByIdAsync(deletedGameEntity.Id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByOwnerIdAsync Tests

    [Fact]
    public async Task GetByOwnerIdAsync_ReturnsOnlyOwnerGames()
    {
        // Act
        var result = await _repository.GetByOwnerIdAsync(_testOwnerId);

        // Assert
        result.Should().HaveCount(2); // Excludes deleted game
        result.Should().AllSatisfy(g => g.OwnerId.Should().Be(_testOwnerId));
        result.Should().AllSatisfy(g => g.IsDeleted.Should().BeFalse());
    }

    [Fact]
    public async Task GetByOwnerIdAsync_OrdersByCreatedAtDescending()
    {
        // Act
        var result = await _repository.GetByOwnerIdAsync(_testOwnerId);

        // Assert
        result.Should().HaveCount(2);
        result.First().Title.Should().Be("Manual Game"); // Created 5 days ago
        result.Last().Title.Should().Be("BGG Game 1");   // Created 10 days ago
    }

    [Fact]
    public async Task GetByOwnerIdAsync_UserWithNoGames_ReturnsEmpty()
    {
        // Act
        var result = await _repository.GetByOwnerIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByOwnerAndBggIdAsync Tests

    [Fact]
    public async Task GetByOwnerAndBggIdAsync_ExistingGame_ReturnsGame()
    {
        // Act
        var result = await _repository.GetByOwnerAndBggIdAsync(_testOwnerId, 12345);

        // Assert
        result.Should().NotBeNull();
        result!.BggId.Should().Be(12345);
        result.OwnerId.Should().Be(_testOwnerId);
        result.Title.Should().Be("BGG Game 1");
    }

    [Fact]
    public async Task GetByOwnerAndBggIdAsync_WrongOwner_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByOwnerAndBggIdAsync(_otherUserId, 12345);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByOwnerAndBggIdAsync_NonExistentBggId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByOwnerAndBggIdAsync(_testOwnerId, 99999);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region ExistsByOwnerAndBggIdAsync Tests

    [Fact]
    public async Task ExistsByOwnerAndBggIdAsync_ExistingGame_ReturnsTrue()
    {
        // Act
        var result = await _repository.ExistsByOwnerAndBggIdAsync(_testOwnerId, 12345);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByOwnerAndBggIdAsync_WrongOwner_ReturnsFalse()
    {
        // Act
        var result = await _repository.ExistsByOwnerAndBggIdAsync(_otherUserId, 12345);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByOwnerAndBggIdAsync_NonExistent_ReturnsFalse()
    {
        // Act
        var result = await _repository.ExistsByOwnerAndBggIdAsync(_testOwnerId, 99999);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CountByOwnerIdAsync Tests

    [Fact]
    public async Task CountByOwnerIdAsync_ReturnsCorrectCount()
    {
        // Act
        var result = await _repository.CountByOwnerIdAsync(_testOwnerId);

        // Assert
        result.Should().Be(2); // Excludes deleted game
    }

    [Fact]
    public async Task CountByOwnerIdAsync_UserWithNoGames_ReturnsZero()
    {
        // Act
        var result = await _repository.CountByOwnerIdAsync(Guid.NewGuid());

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region SearchByTitleAsync Tests
    // Note: SearchByTitleAsync uses EF.Functions.ILike which is PostgreSQL-specific
    // and not supported by InMemory database. These tests require Testcontainers
    // or can be tested in E2E tests. Skipping for InMemory integration tests.

    #endregion

    #region GetByOwnerIdWithDeletedAsync Tests

    [Fact]
    public async Task GetByOwnerIdWithDeletedAsync_ReturnsAllGamesIncludingDeleted()
    {
        // Act
        var result = await _repository.GetByOwnerIdWithDeletedAsync(_testOwnerId);

        // Assert
        result.Should().HaveCount(3); // Includes the deleted game
        result.Should().Contain(g => g.IsDeleted);
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_NewGame_AddsToDatabase()
    {
        // Arrange
        var newGame = PrivateGame.CreateManual(
            ownerId: _testOwnerId,
            title: "New Test Game",
            minPlayers: 2,
            maxPlayers: 6);

        // Act
        await _repository.AddAsync(newGame);
        await _dbContext.SaveChangesAsync();

        // Assert
        var retrieved = await _dbContext.PrivateGames.FindAsync(newGame.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("New Test Game");
        retrieved.OwnerId.Should().Be(_testOwnerId);
        retrieved.Source.Should().Be(PrivateGameSource.Manual);
    }

    [Fact]
    public async Task AddAsync_BggGame_PreservesThumbnailUrl()
    {
        // Arrange
        var bggGame = PrivateGame.CreateFromBgg(
            ownerId: _testOwnerId,
            bggId: 99999,
            title: "Test BGG Game",
            yearPublished: 2024,
            description: "Test",
            minPlayers: 1,
            maxPlayers: 4,
            playingTimeMinutes: 30,
            minAge: 8,
            complexityRating: 1.5m,
            imageUrl: "https://example.com/img.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg");

        // Act
        await _repository.AddAsync(bggGame);
        await _dbContext.SaveChangesAsync();

        // Assert - Verify thumbnail is persisted
        var retrieved = await _dbContext.PrivateGames.FindAsync(bggGame.Id);
        retrieved.Should().NotBeNull();
        retrieved!.ThumbnailUrl.Should().Be("https://example.com/thumb.jpg");

        // Also verify domain entity is correctly reconstructed
        var domainEntity = await _repository.GetByIdAsync(bggGame.Id);
        domainEntity.Should().NotBeNull();
        domainEntity!.ThumbnailUrl.Should().Be("https://example.com/thumb.jpg");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ModifiesExistingGame()
    {
        // Arrange
        var entityId = (await _dbContext.PrivateGames.FirstAsync(g => g.Title == "Manual Game")).Id;
        _dbContext.ChangeTracker.Clear(); // Clear tracking to avoid conflicts

        var existing = await _repository.GetByIdAsync(entityId);
        existing!.UpdateInfo(
            title: "Updated Title",
            minPlayers: 3,
            maxPlayers: 8,
            yearPublished: 2023,
            description: "Updated",
            playingTimeMinutes: 90,
            minAge: 12,
            complexityRating: 3.0m,
            imageUrl: "https://example.com/updated.jpg");

        // Act
        await _repository.UpdateAsync(existing);
        await _dbContext.SaveChangesAsync();

        // Assert
        _dbContext.ChangeTracker.Clear();
        var retrieved = await _repository.GetByIdAsync(existing.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("Updated Title");
        retrieved.MinPlayers.Should().Be(3);
        retrieved.MaxPlayers.Should().Be(8);
        retrieved.UpdatedAt.Should().NotBeNull();
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_SoftDeletesGame()
    {
        // Arrange
        var entityId = (await _dbContext.PrivateGames.FirstAsync(g => g.Title == "Manual Game")).Id;
        _dbContext.ChangeTracker.Clear(); // Clear tracking to avoid conflicts

        var toDelete = await _repository.GetByIdAsync(entityId);
        toDelete.Should().NotBeNull();

        // Act
        toDelete!.Delete(); // Call domain method to soft delete
        await _repository.UpdateAsync(toDelete);
        await _dbContext.SaveChangesAsync();

        // Assert
        _dbContext.ChangeTracker.Clear();
        var retrieved = await _repository.GetByIdAsync(toDelete.Id);
        retrieved.Should().BeNull(); // Soft deleted games are filtered out

        // Verify it's soft deleted in database
        var entity = await _dbContext.PrivateGames.IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Id == toDelete.Id);
        entity.Should().NotBeNull();
        entity!.IsDeleted.Should().BeTrue();
        entity.DeletedAt.Should().NotBeNull();
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_ExistingGame_ReturnsTrue()
    {
        // Arrange
        var existingId = (await _dbContext.PrivateGames.FirstAsync(g => g.Title == "BGG Game 1")).Id;

        // Act
        var result = await _repository.ExistsAsync(existingId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistentGame_ReturnsFalse()
    {
        // Act
        var result = await _repository.ExistsAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_DeletedGame_ReturnsFalse()
    {
        // Arrange
        var deletedGameId = (await _dbContext.PrivateGames.IgnoreQueryFilters()
            .FirstAsync(g => g.IsDeleted)).Id;

        // Act
        var result = await _repository.ExistsAsync(deletedGameId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Domain Event Collection Tests
    // Note: PrivateGame currently doesn't raise domain events (data-focused entity).
    // Event collection tests not applicable. Domain events may be added in future phases.

    #endregion

    #region Unique Constraint Tests
    // Note: InMemory database doesn't fully enforce unique indexes.
    // Constraint tests require Testcontainers with real PostgreSQL.
    // These validations are tested in E2E tests or with real database.

    [Fact]
    public async Task AddAsync_SameBggIdForDifferentOwner_Succeeds()
    {
        // Arrange
        _dbContext.ChangeTracker.Clear();

        var sameGggDifferentOwner = PrivateGame.CreateFromBgg(
            ownerId: _otherUserId,
            bggId: 12345, // Exists for _testOwnerId, but different owner
            title: "Same BGG Different Owner",
            yearPublished: 2024,
            description: null,
            minPlayers: 1,
            maxPlayers: 4,
            playingTimeMinutes: null,
            minAge: null,
            complexityRating: null,
            imageUrl: null,
            thumbnailUrl: null);

        // Act
        await _repository.AddAsync(sameGggDifferentOwner);
        await _dbContext.SaveChangesAsync();

        // Assert
        var retrieved = await _repository.GetByIdAsync(sameGggDifferentOwner.Id);
        retrieved.Should().NotBeNull();
        retrieved!.OwnerId.Should().Be(_otherUserId);
    }

    #endregion
}
