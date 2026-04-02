using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Unit tests for GetLibraryForDowngradeQueryHandler.
/// Uses mocked repository and in-memory DbContext for SharedGames lookup.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class GetLibraryForDowngradeQueryHandlerTests : IDisposable
{
    private readonly Mock<IUserLibraryRepository> _mockRepository;
    private readonly MeepleAiDbContext _db;
    private readonly GetLibraryForDowngradeQueryHandler _handler;

    public GetLibraryForDowngradeQueryHandlerTests()
    {
        _mockRepository = new Mock<IUserLibraryRepository>();
        _db = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new GetLibraryForDowngradeQueryHandler(_mockRepository.Object, _db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    #region Empty library

    [Fact]
    public async Task Handle_EmptyLibrary_ReturnsBothListsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetUserLibraryPaginatedAsync(
                userId, null, null, null, "addedAt", true, 1, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<UserLibraryEntry>(), 0));

        var query = new GetLibraryForDowngradeQuery(userId, NewQuota: 5);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.GamesToKeep.Should().BeEmpty();
        result.GamesToRemove.Should().BeEmpty();
    }

    #endregion

    #region Quota split

    [Fact]
    public async Task Handle_WithEntriesUnderQuota_AllGoToKeep()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        var entries = BuildEntries(userId, new[] { gameId1, gameId2 });

        _mockRepository
            .Setup(r => r.GetUserLibraryPaginatedAsync(
                userId, null, null, null, "addedAt", true, 1, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync((entries, entries.Count));

        SeedSharedGames(new[] { gameId1, gameId2 });

        var query = new GetLibraryForDowngradeQuery(userId, NewQuota: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.GamesToKeep.Should().HaveCount(2);
        result.GamesToRemove.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_LibraryExactlyAtQuota_AllGoToKeepNoneToRemove()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        var entries = BuildEntries(userId, gameIds);

        _mockRepository
            .Setup(r => r.GetUserLibraryPaginatedAsync(
                userId, null, null, null, "addedAt", true, 1, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync((entries, entries.Count));

        SeedSharedGames(gameIds);

        // Quota exactly equals library size
        var query = new GetLibraryForDowngradeQuery(userId, NewQuota: 3);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.GamesToKeep.Should().HaveCount(3);
        result.GamesToRemove.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_QuotaLessThanEntries_SplitsCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var entries = BuildEntries(userId, gameIds);

        _mockRepository
            .Setup(r => r.GetUserLibraryPaginatedAsync(
                userId, null, null, null, "addedAt", true, 1, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync((entries, entries.Count));

        SeedSharedGames(gameIds);

        var query = new GetLibraryForDowngradeQuery(userId, NewQuota: 3);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.GamesToKeep.Should().HaveCount(3);
        result.GamesToRemove.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_QuotaZero_AllGoToRemove()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        var entries = BuildEntries(userId, gameIds);

        _mockRepository
            .Setup(r => r.GetUserLibraryPaginatedAsync(
                userId, null, null, null, "addedAt", true, 1, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync((entries, entries.Count));

        SeedSharedGames(gameIds);

        var query = new GetLibraryForDowngradeQuery(userId, NewQuota: 0);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.GamesToKeep.Should().BeEmpty();
        result.GamesToRemove.Should().HaveCount(3);
    }

    #endregion

    #region Sort order — favorites first

    [Fact]
    public async Task Handle_FavoritesAppearFirstInKeep()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var favoriteGameId = Guid.NewGuid();
        var regularGameId1 = Guid.NewGuid();
        var regularGameId2 = Guid.NewGuid();

        var favoriteEntry = new UserLibraryEntry(Guid.NewGuid(), userId, favoriteGameId);
        favoriteEntry.MarkAsFavorite();

        var regularEntry1 = new UserLibraryEntry(Guid.NewGuid(), userId, regularGameId1);
        var regularEntry2 = new UserLibraryEntry(Guid.NewGuid(), userId, regularGameId2);

        // Favorite is in the middle to confirm sort reorders it
        var entries = new List<UserLibraryEntry> { regularEntry1, favoriteEntry, regularEntry2 };

        _mockRepository
            .Setup(r => r.GetUserLibraryPaginatedAsync(
                userId, null, null, null, "addedAt", true, 1, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync((entries, entries.Count));

        SeedSharedGames(new[] { favoriteGameId, regularGameId1, regularGameId2 });

        // Only keep 2 — favorite must be first in GamesToKeep
        var query = new GetLibraryForDowngradeQuery(userId, NewQuota: 2);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.GamesToKeep.Should().HaveCount(2);
        result.GamesToKeep[0].IsFavorite.Should().BeTrue();
        result.GamesToKeep[0].GameId.Should().Be(favoriteGameId);
    }

    #endregion

    #region Sort order — TimesPlayed tiebreak

    [Fact]
    public async Task Handle_NonFavorites_SortedByTimesPlayedDescending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var highPlayGameId = Guid.NewGuid();
        var lowPlayGameId = Guid.NewGuid();

        var highPlayEntry = new UserLibraryEntry(Guid.NewGuid(), userId, highPlayGameId);
        highPlayEntry.RecordGameSession(DateTime.UtcNow.AddDays(-3), 60);
        highPlayEntry.RecordGameSession(DateTime.UtcNow.AddDays(-2), 60);
        highPlayEntry.RecordGameSession(DateTime.UtcNow.AddDays(-1), 60); // 3 plays

        var lowPlayEntry = new UserLibraryEntry(Guid.NewGuid(), userId, lowPlayGameId);
        lowPlayEntry.RecordGameSession(DateTime.UtcNow.AddDays(-1), 60); // 1 play

        // lowPlayEntry is first in input list to confirm sort reorders it
        var entries = new List<UserLibraryEntry> { lowPlayEntry, highPlayEntry };

        _mockRepository
            .Setup(r => r.GetUserLibraryPaginatedAsync(
                userId, null, null, null, "addedAt", true, 1, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync((entries, entries.Count));

        SeedSharedGames(new[] { highPlayGameId, lowPlayGameId });

        // Keep only 1 — the entry with the most plays must survive
        var query = new GetLibraryForDowngradeQuery(userId, NewQuota: 1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.GamesToKeep.Should().HaveCount(1);
        result.GamesToKeep[0].GameId.Should().Be(highPlayGameId);
        result.GamesToKeep[0].TimesPlayed.Should().Be(3);

        result.GamesToRemove.Should().HaveCount(1);
        result.GamesToRemove[0].GameId.Should().Be(lowPlayGameId);
    }

    #endregion

    #region DTO mapping

    [Fact]
    public async Task Handle_MapsGameTitleAndImageCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);

        _mockRepository
            .Setup(r => r.GetUserLibraryPaginatedAsync(
                userId, null, null, null, "addedAt", true, 1, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<UserLibraryEntry> { entry }, 1));

        _db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = "Catan",
            ImageUrl = "https://example.com/catan.jpg",
            ThumbnailUrl = "https://example.com/catan-thumb.jpg",
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var query = new GetLibraryForDowngradeQuery(userId, NewQuota: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.GamesToKeep.Should().HaveCount(1);
        var dto = result.GamesToKeep[0];
        dto.GameTitle.Should().Be("Catan");
        dto.GameImageUrl.Should().Be("https://example.com/catan.jpg");
        dto.EntryId.Should().Be(entry.Id);
        dto.GameId.Should().Be(gameId);
    }

    [Fact]
    public async Task Handle_MissingSharedGame_UsesUnknownGameFallback()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);

        _mockRepository
            .Setup(r => r.GetUserLibraryPaginatedAsync(
                userId, null, null, null, "addedAt", true, 1, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<UserLibraryEntry> { entry }, 1));

        // No SharedGame seeded — handler should fall back to "Unknown Game"

        var query = new GetLibraryForDowngradeQuery(userId, NewQuota: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.GamesToKeep.Should().HaveCount(1);
        result.GamesToKeep[0].GameTitle.Should().Be("Unknown Game");
        result.GamesToKeep[0].GameImageUrl.Should().BeNull();
    }

    #endregion

    #region Helpers

    private static IReadOnlyList<UserLibraryEntry> BuildEntries(Guid userId, Guid[] gameIds)
        => gameIds.Select(gid => new UserLibraryEntry(Guid.NewGuid(), userId, gid)).ToList();

    private void SeedSharedGames(Guid[] gameIds)
    {
        var index = 1;
        foreach (var gameId in gameIds)
        {
            _db.SharedGames.Add(new SharedGameEntity
            {
                Id = gameId,
                Title = $"Game {index++}",
                ImageUrl = string.Empty,
                ThumbnailUrl = string.Empty,
                IsDeleted = false,
            });
        }
        _db.SaveChanges();
    }

    #endregion
}
