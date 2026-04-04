using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Infrastructure;

/// <summary>
/// Integration tests for UserLibraryRepository with InMemory database.
/// Issue #3007: Tests filter queries for Owned and Wishlist games.
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class UserLibraryRepositoryIntegrationTests : IAsyncLifetime
{
    private MeepleAiDbContext _dbContext = null!;
    private UserLibraryRepository _repository = null!;
    private readonly Mock<IDomainEventCollector> _eventCollector;

    private Guid _testUserId;
    private readonly List<Guid> _ownedGameIds = new();
    private readonly List<Guid> _wishlistGameIds = new();

    public UserLibraryRepositoryIntegrationTests()
    {
        _eventCollector = TestDbContextFactory.CreateMockEventCollector();
    }

    public async ValueTask InitializeAsync()
    {
        // Create unique database for test isolation
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext($"UserLibraryRepoTest_{Guid.NewGuid()}");
        _repository = new UserLibraryRepository(
            _dbContext,
            _eventCollector.Object,
            NullLogger<UserLibraryRepository>.Instance);

        // Seed test data
        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    /// <summary>
    /// Seeds test data with a user having 2 Owned and 2 Wishlist games.
    /// </summary>
    private async Task SeedTestDataAsync()
    {
        _testUserId = Guid.NewGuid();

        // Create user entity
        var userEntity = new UserEntity
        {
            Id = _testUserId,
            Email = $"test_{Guid.NewGuid()}@example.com",
            DisplayName = "Test User",
            PasswordHash = "hashedpassword123",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(userEntity);

        // Create 4 shared games
        var games = new List<SharedGameEntity>();
        for (int i = 0; i < 4; i++)
        {
            var game = new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                Title = $"Test Game {i + 1}",
                YearPublished = 2020 + i,
                Description = $"Description for game {i + 1}",
                MinPlayers = 1,
                MaxPlayers = 4,
                PlayingTimeMinutes = 60,
                MinAge = 10,
                CreatedBy = _testUserId,
                CreatedAt = DateTime.UtcNow
            };
            games.Add(game);
            _dbContext.SharedGames.Add(game);
        }

        await _dbContext.SaveChangesAsync();

        // Create 2 Owned library entries
        for (int i = 0; i < 2; i++)
        {
            var entry = new UserLibraryEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                GameId = games[i].Id,
                CurrentState = (int)GameStateType.Owned,
                StateChangedAt = DateTime.UtcNow,
                AddedAt = DateTime.UtcNow.AddDays(-i - 1)
            };
            _dbContext.UserLibraryEntries.Add(entry);
            _ownedGameIds.Add(entry.Id);
        }

        // Create 2 Wishlist library entries
        for (int i = 2; i < 4; i++)
        {
            var entry = new UserLibraryEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                GameId = games[i].Id,
                CurrentState = (int)GameStateType.Wishlist,
                StateChangedAt = DateTime.UtcNow,
                AddedAt = DateTime.UtcNow.AddDays(-i - 1)
            };
            _dbContext.UserLibraryEntries.Add(entry);
            _wishlistGameIds.Add(entry.Id);
        }

        await _dbContext.SaveChangesAsync();

        // Detach all entities to simulate fresh repository queries
        _dbContext.ChangeTracker.Clear();
    }

    #region GetUserGamesAsync Filter Tests

    [Fact]
    public async Task GetUserGamesAsync_WithOwnedFilter_ReturnsOnlyOwnedGames()
    {
        // Act
        var result = await _repository.GetUserGamesAsync(_testUserId, GameStateType.Owned);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(e => e.CurrentState.Value == GameStateType.Owned);
    }

    [Fact]
    public async Task GetUserGamesAsync_WithWishlistFilter_ReturnsOnlyWishlistGames()
    {
        // Act
        var result = await _repository.GetUserGamesAsync(_testUserId, GameStateType.Wishlist);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(e => e.CurrentState.Value == GameStateType.Wishlist);
    }

    [Fact]
    public async Task GetUserGamesAsync_WithNoFilter_ReturnsAllGames()
    {
        // Act
        var result = await _repository.GetUserGamesAsync(_testUserId);

        // Assert
        result.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetUserGamesAsync_WithNonExistentUser_ReturnsEmpty()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _repository.GetUserGamesAsync(nonExistentUserId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserGamesAsync_WithNuovoFilter_ReturnsOnlyNuovoGames()
    {
        // Arrange - Add a Nuovo game
        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Nuovo Game",
            YearPublished = 2024,
            Description = "A new game",
            MinPlayers = 2,
            MaxPlayers = 6,
            PlayingTimeMinutes = 90,
            MinAge = 12,
            CreatedBy = _testUserId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.SharedGames.Add(game);

        var entry = new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            GameId = game.Id,
            CurrentState = (int)GameStateType.Nuovo,
            StateChangedAt = DateTime.UtcNow,
            AddedAt = DateTime.UtcNow
        };
        _dbContext.UserLibraryEntries.Add(entry);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetUserGamesAsync(_testUserId, GameStateType.Nuovo);

        // Assert
        result.Should().HaveCount(1);
        result.Should().OnlyContain(e => e.CurrentState.Value == GameStateType.Nuovo);
    }

    [Fact]
    public async Task GetUserGamesAsync_WithInPrestitoFilter_ReturnsOnlyLoanedGames()
    {
        // Arrange - Add an InPrestito game
        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Loaned Game",
            YearPublished = 2023,
            Description = "A loaned game",
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 45,
            MinAge = 8,
            CreatedBy = _testUserId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.SharedGames.Add(game);

        var entry = new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            GameId = game.Id,
            CurrentState = (int)GameStateType.InPrestito,
            StateChangedAt = DateTime.UtcNow,
            StateNotes = "Loaned to John",
            AddedAt = DateTime.UtcNow
        };
        _dbContext.UserLibraryEntries.Add(entry);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetUserGamesAsync(_testUserId, GameStateType.InPrestito);

        // Assert
        result.Should().HaveCount(1);
        result.Should().OnlyContain(e => e.CurrentState.Value == GameStateType.InPrestito);
    }

    #endregion

    #region GetUserGamesAsync Ordering Tests

    [Fact]
    public async Task GetUserGamesAsync_ReturnsGamesOrderedByAddedAtDescending()
    {
        // Act
        var result = await _repository.GetUserGamesAsync(_testUserId);

        // Assert
        result.Should().BeInDescendingOrder(e => e.AddedAt);
    }

    [Fact]
    public async Task AddAsync_WithPrivatePdfId_PersistsAssociation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        // Seed user
        var userEntity = new UserEntity
        {
            Id = userId,
            Email = $"test_{Guid.NewGuid()}@example.com",
            DisplayName = "Test User",
            PasswordHash = "hashedpassword123",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(userEntity);

        // Seed game
        var gameEntity = new SharedGameEntity
        {
            Id = gameId,
            Title = "Test Game",
            YearPublished = 2024,
            Description = "Test Description",
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.SharedGames.Add(gameEntity);

        // Seed PDF document
        var pdfEntity = new PdfDocumentEntity
        {
            Id = pdfId,
            UploadedByUserId = userId,
            GameId = gameId,
            UploadedAt = DateTime.UtcNow,
            FilePath = "/test/path.pdf",
            FileName = "test.pdf",
            FileSizeBytes = 1024,
            PageCount = 10,
        };
        _dbContext.PdfDocuments.Add(pdfEntity);

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Create library entry with private PDF
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);
        entry.AssociatePrivatePdf(pdfId);

        // Act
        await _repository.AddAsync(entry);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Assert - verify persisted correctly
        var retrieved = await _repository.GetUserGameWithStatsAsync(userId, gameId);
        retrieved.Should().NotBeNull();
        retrieved!.PrivatePdfId.Should().Be(pdfId);
        retrieved.HasPrivatePdf.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserGameWithStatsAsync_WithPrivatePdf_LoadsNavigationProperty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        // Seed user
        var userEntity = new UserEntity
        {
            Id = userId,
            Email = $"test_{Guid.NewGuid()}@example.com",
            DisplayName = "Test User",
            PasswordHash = "hashedpassword123",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(userEntity);

        // Seed game
        var gameEntity = new SharedGameEntity
        {
            Id = gameId,
            Title = "Test Game",
            YearPublished = 2024,
            Description = "Test Description",
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.SharedGames.Add(gameEntity);

        // Seed PDF document
        var pdfEntity = new PdfDocumentEntity
        {
            Id = pdfId,
            UploadedByUserId = userId,
            GameId = gameId,
            UploadedAt = DateTime.UtcNow,
            FilePath = "/test/path.pdf",
            FileName = "test.pdf",
            FileSizeBytes = 1024,
            PageCount = 10,
        };
        _dbContext.PdfDocuments.Add(pdfEntity);

        // Create library entry entity with private PDF
        var entryEntity = new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            AddedAt = DateTime.UtcNow,
            IsFavorite = false,
            CurrentState = 0,
            TimesPlayed = 0,
            CompetitiveSessions = 0,
            PrivatePdfId = pdfId
        };
        _dbContext.UserLibraryEntries.Add(entryEntity);

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var retrieved = await _repository.GetUserGameWithStatsAsync(userId, gameId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.PrivatePdfId.Should().Be(pdfId);
        retrieved.HasPrivatePdf.Should().BeTrue();
    }

    #endregion

    #region GetUserGameWithStatsAsync Tests

    [Fact]
    public async Task GetUserGameWithStatsAsync_WithValidUserAndGame_ReturnsEntry()
    {
        // Arrange
        var gameId = await GetFirstGameIdForUserAsync(_testUserId);

        // Act
        var result = await _repository.GetUserGameWithStatsAsync(_testUserId, gameId);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(_testUserId);
        result.GameId.Should().Be(gameId);
    }

    [Fact]
    public async Task GetUserGameWithStatsAsync_WithNonExistentEntry_ReturnsNull()
    {
        // Arrange
        var nonExistentGameId = Guid.NewGuid();

        // Act
        var result = await _repository.GetUserGameWithStatsAsync(_testUserId, nonExistentGameId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private async Task<Guid> GetFirstGameIdForUserAsync(Guid userId)
    {
        var entry = await _dbContext.UserLibraryEntries
            .AsNoTracking()
            .FirstAsync(e => e.UserId == userId);
        return entry.GameId;
    }

    #endregion
}
