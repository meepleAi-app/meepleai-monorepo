using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.UserLibrary.Infrastructure;

/// <summary>
/// Integration tests for UserLibraryRepository with in-memory database.
/// Tests repository query methods, navigation properties, and data persistence.
/// </summary>
public sealed class UserLibraryRepositoryIntegrationTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    [Fact]
    public void Constructor_CreatesRepository()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"UserLibraryTest_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();

#pragma warning disable CA2000 // DbContext disposed immediately after test
        var dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
#pragma warning restore CA2000

        // Act
        var repository = new UserLibraryRepository(
            dbContext,
            mockEventCollector.Object,
            NullLogger<UserLibraryRepository>.Instance);

        // Assert
        repository.Should().NotBeNull();
        dbContext.Dispose();
    }

    [Fact]
    public void RepositoryInterface_ImplementsIUserLibraryRepository()
    {
        // Assert
        typeof(UserLibraryRepository).Should().BeAssignableTo<IUserLibraryRepository>();
    }

    [Fact]
    public void NewQueryMethods_ExistInInterface()
    {
        // Assert - Verify new methods from Issue #2825 exist
        var interfaceType = typeof(IUserLibraryRepository);
        
        interfaceType.GetMethod("GetUserGameWithStatsAsync").Should().NotBeNull(
            "GetUserGameWithStatsAsync should exist per Issue #2825 requirements");
        
        interfaceType.GetMethod("GetUserGamesAsync").Should().NotBeNull(
            "GetUserGamesAsync should exist per Issue #2825 requirements");
    }

    [Fact]
    public void NewQueryMethods_ExistInImplementation()
    {
        // Assert - Verify new methods implemented
        var implType = typeof(UserLibraryRepository);

        implType.GetMethod("GetUserGameWithStatsAsync").Should().NotBeNull();
        implType.GetMethod("GetUserGamesAsync").Should().NotBeNull();
    }

    #region GetUserGameWithStatsAsync Tests

    [Fact]
    public async Task GetUserGameWithStatsAsync_LoadsSessionsAndChecklist()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"UserLibraryTest_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents()).Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());

#pragma warning disable CA2000
        var dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
#pragma warning restore CA2000

        var repository = new UserLibraryRepository(
            dbContext,
            mockEventCollector.Object,
            NullLogger<UserLibraryRepository>.Instance);

        // Create entry with sessions and checklist
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);
        entry.RecordGameSession(DateTime.UtcNow.AddDays(-1), 60, true, "Alice, Bob", "Great game!");
        entry.RecordGameSession(DateTime.UtcNow, 90, false, "Charlie", "Close match");
        entry.AddChecklistItem("Setup board", "Place tiles in grid");
        entry.AddChecklistItem("Shuffle deck", "Remove jokers first");

        await repository.AddAsync(entry);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetUserGameWithStatsAsync(_userId, _gameId);

        // Assert
        result.Should().NotBeNull();
        result!.Sessions.Should().HaveCount(2);
        result.Checklist.Should().HaveCount(2);
        result.Stats.TimesPlayed.Should().Be(2);
        result.Stats.WinRate.Should().Be(50m, "1 win, 1 loss = 50%");

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetUserGameWithStatsAsync_NonExistentGame_ReturnsNull()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"UserLibraryTest_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents()).Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());

#pragma warning disable CA2000
        var dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
#pragma warning restore CA2000

        var repository = new UserLibraryRepository(
            dbContext,
            mockEventCollector.Object,
            NullLogger<UserLibraryRepository>.Instance);

        // Act - Query non-existent game
        var result = await repository.GetUserGameWithStatsAsync(_userId, Guid.NewGuid());

        // Assert
        result.Should().BeNull();

        dbContext.Dispose();
    }

    #endregion

    #region GetUserGamesAsync Tests

    [Fact]
    public async Task GetUserGamesAsync_NoStateFilter_ReturnsAllGames()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"UserLibraryTest_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents()).Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());

#pragma warning disable CA2000
        var dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
#pragma warning restore CA2000

        var repository = new UserLibraryRepository(
            dbContext,
            mockEventCollector.Object,
            NullLogger<UserLibraryRepository>.Instance);

        // Create entries in different states
        var entry1 = new UserLibraryEntry(Guid.NewGuid(), _userId, Guid.NewGuid());
        entry1.MarkAsOwned();

        var entry2 = new UserLibraryEntry(Guid.NewGuid(), _userId, Guid.NewGuid());
        entry2.AddToWishlist();

        var entry3 = new UserLibraryEntry(Guid.NewGuid(), _userId, Guid.NewGuid());
        entry3.MarkAsOwned();
        entry3.MarkAsOnLoan("Borrowed");

        await repository.AddAsync(entry1);
        await repository.AddAsync(entry2);
        await repository.AddAsync(entry3);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetUserGamesAsync(_userId);

        // Assert
        result.Should().HaveCount(3);

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetUserGamesAsync_WithOwnedFilter_ReturnsOnlyOwnedGames()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"UserLibraryTest_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents()).Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());

#pragma warning disable CA2000
        var dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
#pragma warning restore CA2000

        var repository = new UserLibraryRepository(
            dbContext,
            mockEventCollector.Object,
            NullLogger<UserLibraryRepository>.Instance);

        // Create entries
        var entry1 = new UserLibraryEntry(Guid.NewGuid(), _userId, Guid.NewGuid());
        entry1.MarkAsOwned();

        var entry2 = new UserLibraryEntry(Guid.NewGuid(), _userId, Guid.NewGuid());
        entry2.AddToWishlist();

        var entry3 = new UserLibraryEntry(Guid.NewGuid(), _userId, Guid.NewGuid());
        entry3.MarkAsOwned();

        await repository.AddAsync(entry1);
        await repository.AddAsync(entry2);
        await repository.AddAsync(entry3);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetUserGamesAsync(_userId, GameStateType.Owned);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(e => e.CurrentState.Value.Should().Be(GameStateType.Owned));

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetUserGamesAsync_WithWishlistFilter_ReturnsOnlyWishlistGames()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"UserLibraryTest_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents()).Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());

#pragma warning disable CA2000
        var dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
#pragma warning restore CA2000

        var repository = new UserLibraryRepository(
            dbContext,
            mockEventCollector.Object,
            NullLogger<UserLibraryRepository>.Instance);

        // Create entries
        var entry1 = new UserLibraryEntry(Guid.NewGuid(), _userId, Guid.NewGuid());
        entry1.MarkAsOwned();

        var entry2 = new UserLibraryEntry(Guid.NewGuid(), _userId, Guid.NewGuid());
        entry2.AddToWishlist("Want this!");

        var entry3 = new UserLibraryEntry(Guid.NewGuid(), _userId, Guid.NewGuid());
        entry3.AddToWishlist("And this!");

        await repository.AddAsync(entry1);
        await repository.AddAsync(entry2);
        await repository.AddAsync(entry3);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetUserGamesAsync(_userId, GameStateType.Wishlist);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(e => e.CurrentState.Value.Should().Be(GameStateType.Wishlist));

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetUserGamesAsync_EmptyLibrary_ReturnsEmptyList()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"UserLibraryTest_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents()).Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());

#pragma warning disable CA2000
        var dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
#pragma warning restore CA2000

        var repository = new UserLibraryRepository(
            dbContext,
            mockEventCollector.Object,
            NullLogger<UserLibraryRepository>.Instance);

        // Act - Query empty library
        var result = await repository.GetUserGamesAsync(_userId);

        // Assert
        result.Should().BeEmpty();

        dbContext.Dispose();
    }

    #endregion

    #region GetByUserAndGameAsync Tests

    [Fact]
    public async Task GetByUserAndGameAsync_ExistingGame_ReturnsEntry()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"UserLibraryTest_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents()).Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());

#pragma warning disable CA2000
        var dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
#pragma warning restore CA2000

        var repository = new UserLibraryRepository(
            dbContext,
            mockEventCollector.Object,
            NullLogger<UserLibraryRepository>.Instance);

        // Create entry
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);
        await repository.AddAsync(entry);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetByUserAndGameAsync(_userId, _gameId);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(_userId);
        result.GameId.Should().Be(_gameId);

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetByUserAndGameAsync_NonExistingGame_ReturnsNull()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"UserLibraryTest_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents()).Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());

#pragma warning disable CA2000
        var dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
#pragma warning restore CA2000

        var repository = new UserLibraryRepository(
            dbContext,
            mockEventCollector.Object,
            NullLogger<UserLibraryRepository>.Instance);

        // Act
        var result = await repository.GetByUserAndGameAsync(_userId, Guid.NewGuid());

        // Assert
        result.Should().BeNull();

        dbContext.Dispose();
    }

    #endregion

    #region IsGameInLibraryAsync Tests

    [Fact]
    public async Task IsGameInLibraryAsync_ExistingGame_ReturnsTrue()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"UserLibraryTest_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents()).Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());

#pragma warning disable CA2000
        var dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
#pragma warning restore CA2000

        var repository = new UserLibraryRepository(
            dbContext,
            mockEventCollector.Object,
            NullLogger<UserLibraryRepository>.Instance);

        // Create entry
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);
        await repository.AddAsync(entry);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await repository.IsGameInLibraryAsync(_userId, _gameId);

        // Assert
        result.Should().BeTrue();

        dbContext.Dispose();
    }

    [Fact]
    public async Task IsGameInLibraryAsync_NonExistingGame_ReturnsFalse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"UserLibraryTest_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents()).Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());

#pragma warning disable CA2000
        var dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
#pragma warning restore CA2000

        var repository = new UserLibraryRepository(
            dbContext,
            mockEventCollector.Object,
            NullLogger<UserLibraryRepository>.Instance);

        // Act
        var result = await repository.IsGameInLibraryAsync(_userId, Guid.NewGuid());

        // Assert
        result.Should().BeFalse();

        dbContext.Dispose();
    }

    #endregion
}
