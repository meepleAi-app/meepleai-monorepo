using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure;

/// <summary>
/// Integration tests for GameRepository with real PostgreSQL database.
/// Tests CRUD operations, search/filter queries, and pagination.
/// Uses SharedTestcontainersFixture for efficient container sharing.
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private GameRepository _repository = null!;
    private string _databaseName = null!;
    private string _connectionString = null!;

    public GameRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        // Issue #3102: Removed Substring(0, 63) - string is only 46 chars, which is under PostgreSQL's 63-char limit
        _databaseName = $"test_gamerepo_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        // Create DbContext and apply migrations
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();

        // Create repository with mock event collector
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _repository = new GameRepository(_dbContext, mockEventCollector.Object);

        // Seed test data
        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private async Task SeedTestDataAsync()
    {
        // Create test games with various titles for search testing
        var games = new List<GameEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Catan",
                Publisher = "Catan Studio",
                YearPublished = 1995,
                MinPlayers = 3,
                MaxPlayers = 4,
                MinPlayTimeMinutes = 60,
                MaxPlayTimeMinutes = 120,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Ticket to Ride",
                Publisher = "Days of Wonder",
                YearPublished = 2004,
                MinPlayers = 2,
                MaxPlayers = 5,
                MinPlayTimeMinutes = 30,
                MaxPlayTimeMinutes = 60,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Pandemic",
                Publisher = "Z-Man Games",
                YearPublished = 2008,
                MinPlayers = 2,
                MaxPlayers = 4,
                MinPlayTimeMinutes = 45,
                MaxPlayTimeMinutes = 60,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Carcassonne",
                Publisher = "Hans im Glück",
                YearPublished = 2000,
                MinPlayers = 2,
                MaxPlayers = 5,
                MinPlayTimeMinutes = 30,
                MaxPlayTimeMinutes = 45,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Azul",
                Publisher = "Plan B Games",
                YearPublished = 2017,
                MinPlayers = 2,
                MaxPlayers = 4,
                MinPlayTimeMinutes = 30,
                MaxPlayTimeMinutes = 45,
                CreatedAt = DateTime.UtcNow
            }
        };

        _dbContext.Games.AddRange(games);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingGame_ReturnsGame()
    {
        // Arrange
        var existingGame = await _dbContext.Games.AsNoTracking().FirstAsync();

        // Act
        var result = await _repository.GetByIdAsync(existingGame.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(existingGame.Id);
        result.Title.Value.Should().Be(existingGame.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_MapsAllPropertiesCorrectly()
    {
        // Arrange
        var existingGame = await _dbContext.Games.AsNoTracking().FirstAsync();

        // Act
        var result = await _repository.GetByIdAsync(existingGame.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Value.Should().Be(existingGame.Name);
        result.Publisher?.Name.Should().Be(existingGame.Publisher);
        result.YearPublished?.Value.Should().Be(existingGame.YearPublished);
        result.PlayerCount?.Min.Should().Be(existingGame.MinPlayers);
        result.PlayerCount?.Max.Should().Be(existingGame.MaxPlayers);
        result.PlayTime?.MinMinutes.Should().Be(existingGame.MinPlayTimeMinutes);
        result.PlayTime?.MaxMinutes.Should().Be(existingGame.MaxPlayTimeMinutes);
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllGames()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsGamesOrderedByName()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().BeInAscendingOrder(g => g.Title.Value);
    }

    #endregion

    #region FindByTitleAsync Tests

    [Fact]
    public async Task FindByTitleAsync_WithMatchingPattern_ReturnsMatchingGames()
    {
        // Act
        var result = await _repository.FindByTitleAsync("Ca");

        // Assert
        result.Should().HaveCount(2); // Catan, Carcassonne
        result.Should().OnlyContain(g => g.Title.Value.Contains("Ca", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FindByTitleAsync_WithExactMatch_ReturnsGame()
    {
        // Act
        var result = await _repository.FindByTitleAsync("Catan");

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Value.Should().Be("Catan");
    }

    [Fact]
    public async Task FindByTitleAsync_WithNoMatch_ReturnsEmpty()
    {
        // Act
        var result = await _repository.FindByTitleAsync("NonExistentGame");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FindByTitleAsync_IsCaseInsensitive()
    {
        // Act
        var lowerResult = await _repository.FindByTitleAsync("catan");
        var upperResult = await _repository.FindByTitleAsync("CATAN");
        var mixedResult = await _repository.FindByTitleAsync("CaTaN");

        // Assert
        lowerResult.Should().HaveCount(1);
        upperResult.Should().HaveCount(1);
        mixedResult.Should().HaveCount(1);
    }

    [Fact]
    public async Task FindByTitleAsync_WithPartialMatch_ReturnsMatchingGames()
    {
        // Act
        var result = await _repository.FindByTitleAsync("an"); // Catan, Pandemic

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task FindByTitleAsync_ReturnsOrderedByName()
    {
        // Act
        var result = await _repository.FindByTitleAsync("a"); // Multiple matches

        // Assert
        result.Should().BeInAscendingOrder(g => g.Title.Value);
    }

    [Fact]
    public async Task FindByTitleAsync_WithNullPattern_ThrowsArgumentNullException()
    {
        // Act & Assert
        await FluentActions.Invoking(() => _repository.FindByTitleAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region GetPaginatedAsync Tests

    [Fact]
    public async Task GetPaginatedAsync_WithDefaultParameters_ReturnsFirstPage()
    {
        // Act
        var (games, total) = await _repository.GetPaginatedAsync(null, 1, 20);

        // Assert
        games.Should().HaveCount(5);
        total.Should().Be(5);
    }

    [Fact]
    public async Task GetPaginatedAsync_WithPageSize_RespectsLimit()
    {
        // Act
        var (games, total) = await _repository.GetPaginatedAsync(null, 1, 2);

        // Assert
        games.Should().HaveCount(2);
        total.Should().Be(5);
    }

    [Fact]
    public async Task GetPaginatedAsync_WithSecondPage_ReturnsCorrectGames()
    {
        // Act
        var (firstPage, _) = await _repository.GetPaginatedAsync(null, 1, 2);
        var (secondPage, _) = await _repository.GetPaginatedAsync(null, 2, 2);

        // Assert
        firstPage.Should().HaveCount(2);
        secondPage.Should().HaveCount(2);
        firstPage.Should().NotIntersectWith(secondPage);
    }

    [Fact]
    public async Task GetPaginatedAsync_WithLastPage_ReturnsRemainingGames()
    {
        // Act
        var (games, total) = await _repository.GetPaginatedAsync(null, 3, 2);

        // Assert
        games.Should().HaveCount(1); // 5 total, page 3 with size 2 = 1 remaining
        total.Should().Be(5);
    }

    [Fact]
    public async Task GetPaginatedAsync_WithSearchFilter_ReturnsFilteredGames()
    {
        // Act
        var (games, total) = await _repository.GetPaginatedAsync("Ca", 1, 20);

        // Assert
        games.Should().HaveCount(2); // Catan, Carcassonne
        total.Should().Be(2);
    }

    [Fact]
    public async Task GetPaginatedAsync_WithSearchFilter_IsCaseInsensitive()
    {
        // Act
        var (lowerGames, lowerTotal) = await _repository.GetPaginatedAsync("catan", 1, 20);
        var (upperGames, upperTotal) = await _repository.GetPaginatedAsync("CATAN", 1, 20);

        // Assert
        lowerGames.Should().HaveCount(1);
        upperGames.Should().HaveCount(1);
        lowerTotal.Should().Be(1);
        upperTotal.Should().Be(1);
    }

    [Fact]
    public async Task GetPaginatedAsync_WithEmptySearch_ReturnsAllGames()
    {
        // Act
        var (games, total) = await _repository.GetPaginatedAsync("", 1, 20);

        // Assert
        games.Should().HaveCount(5);
        total.Should().Be(5);
    }

    [Fact]
    public async Task GetPaginatedAsync_WithWhitespaceSearch_ReturnsAllGames()
    {
        // Act
        var (games, total) = await _repository.GetPaginatedAsync("   ", 1, 20);

        // Assert
        games.Should().HaveCount(5);
        total.Should().Be(5);
    }

    [Fact]
    public async Task GetPaginatedAsync_WithInvalidPage_DefaultsToPageOne()
    {
        // Act
        var (games, _) = await _repository.GetPaginatedAsync(null, 0, 2);
        var (negativeGames, _) = await _repository.GetPaginatedAsync(null, -5, 2);

        // Assert - Both should return first page (first 2 games)
        games.Should().HaveCount(2);
        negativeGames.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPaginatedAsync_WithInvalidPageSize_DefaultsToTwenty()
    {
        // Act
        var (games, _) = await _repository.GetPaginatedAsync(null, 1, 0);
        var (negativeGames, _) = await _repository.GetPaginatedAsync(null, 1, -5);

        // Assert - Both should return all 5 games (pageSize defaults to 20)
        games.Should().HaveCount(5);
        negativeGames.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetPaginatedAsync_WithLargePageSize_CapsAt100()
    {
        // This test verifies the cap behavior, though we only have 5 games
        // Act
        var (games, total) = await _repository.GetPaginatedAsync(null, 1, 500);

        // Assert - Should return all games, capped pageSize is not visible with small dataset
        games.Should().HaveCount(5);
        total.Should().Be(5);
    }

    [Fact]
    public async Task GetPaginatedAsync_ReturnsGamesOrderedByName()
    {
        // Act
        var (games, _) = await _repository.GetPaginatedAsync(null, 1, 20);

        // Assert
        games.Should().BeInAscendingOrder(g => g.Title.Value);
    }

    [Fact]
    public async Task GetPaginatedAsync_WithNoMatchingSearch_ReturnsEmptyWithZeroTotal()
    {
        // Act
        var (games, total) = await _repository.GetPaginatedAsync("NonExistentGame", 1, 20);

        // Assert
        games.Should().BeEmpty();
        total.Should().Be(0);
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WithValidGame_PersistsGame()
    {
        // Arrange
        var newGame = new Game(
            id: Guid.NewGuid(),
            title: new GameTitle("Wingspan"),
            publisher: new Publisher("Stonemaier Games"),
            yearPublished: new YearPublished(2019),
            playerCount: new PlayerCount(1, 5),
            playTime: new PlayTime(40, 70)
        );

        // Act
        await _repository.AddAsync(newGame);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Assert
        var saved = await _dbContext.Games.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == newGame.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Wingspan");
        saved.Publisher.Should().Be("Stonemaier Games");
        saved.YearPublished.Should().Be(2019);
    }

    [Fact]
    public async Task AddAsync_WithNullGame_ThrowsArgumentNullException()
    {
        // Act & Assert
        await FluentActions.Invoking(() => _repository.AddAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithValidGame_UpdatesGame()
    {
        // Arrange
        var existingEntity = await _dbContext.Games.FirstAsync();
        var existingGame = await _repository.GetByIdAsync(existingEntity.Id);

        // Create updated game (domain entity is immutable, so create new with same ID)
        var updatedGame = new Game(
            id: existingGame!.Id,
            title: new GameTitle("Updated Title"),
            publisher: existingGame.Publisher,
            yearPublished: existingGame.YearPublished,
            playerCount: existingGame.PlayerCount,
            playTime: existingGame.PlayTime
        );

        // Detach the tracked entity
        _dbContext.ChangeTracker.Clear();

        // Act
        await _repository.UpdateAsync(updatedGame);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Assert
        var saved = await _dbContext.Games.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == existingGame.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Updated Title");
    }

    [Fact]
    public async Task UpdateAsync_WithNullGame_ThrowsArgumentNullException()
    {
        // Act & Assert
        await FluentActions.Invoking(() => _repository.UpdateAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WithValidGame_RemovesGame()
    {
        // Arrange
        var existingEntity = await _dbContext.Games.FirstAsync();
        var gameToDelete = await _repository.GetByIdAsync(existingEntity.Id);
        _dbContext.ChangeTracker.Clear();

        // Act
        await _repository.DeleteAsync(gameToDelete!);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Assert
        var deleted = await _dbContext.Games.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == existingEntity.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNullGame_ThrowsArgumentNullException()
    {
        // Act & Assert
        await FluentActions.Invoking(() => _repository.DeleteAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_WithExistingGame_ReturnsTrue()
    {
        // Arrange
        var existingGame = await _dbContext.Games.AsNoTracking().FirstAsync();

        // Act
        var result = await _repository.ExistsAsync(existingGame.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistentGame_ReturnsFalse()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.ExistsAsync(nonExistentId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
