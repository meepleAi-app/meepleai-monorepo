using Api.SharedKernel.Domain.Interfaces;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Infrastructure;
using Api.SharedKernel.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using Api.Tests.TestHelpers;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Integration tests for SharedGameRepository
/// Tests: CRUD operations using actual repository interface methods
/// Issue #2371 Phase 2
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private ISharedGameRepository _repository = null!;
    private static readonly Guid TestUserId = Guid.NewGuid();

    public SharedGameRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"sharedgame_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        // Create unique test database using fixture helper
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        // Build DbContext with test database
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector()) // Issue #3547
            .Options;

        // Mock MediatR and DomainEventCollector (required by DbContext)
        var mockMediator = new Mock<IMediator>();
        var eventCollectorMock = new Mock<IDomainEventCollector>();
        // CRITICAL FIX: Setup mock to return empty list (prevents NullReferenceException in SaveChangesAsync)
        // Pattern from DbContextHelper.cs - ensures GetAndClearEvents() returns empty collection, not null
        eventCollectorMock.Setup(x => x.GetAndClearEvents())
            .Returns(new List<IDomainEvent>().AsReadOnly());

        // Fix: Use PostgreSQL DbContext with Testcontainers, not in-memory
        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, eventCollectorMock.Object);
        await _dbContext.Database.MigrateAsync();

        // Initialize repository (only needs DbContext)
        _repository = new SharedGameRepository(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        // Fixture handles database cleanup automatically
    }

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WithValidGame_PersistsToDatabase()
    {
        // Arrange
        var game = SharedGame.Create(
            title: "Catan",
            yearPublished: 1995,
            description: "Trade, build, and settle the island of Catan",
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: 7.8m,
            imageUrl: "https://example.com/catan.jpg",
            thumbnailUrl: "https://example.com/catan-thumb.jpg",
            rules: null,
            createdBy: TestUserId
        );

        // Act
        await _repository.AddAsync(game);
        await _dbContext.SaveChangesAsync();

        // Assert
        var retrieved = await _repository.GetByIdAsync(game.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Catan", retrieved.Title);
        Assert.Equal(1995, retrieved.YearPublished);
        Assert.Equal(GameStatus.Draft, retrieved.Status);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingGame_ReturnsGame()
    {
        // Arrange
        var game = SharedGame.Create("Pandemic", 2008, "Cooperative disease outbreak game", 2, 4, 45, 8,
            null, null, "https://example.com/pandemic.jpg", "https://example.com/pandemic-thumb.jpg", null, TestUserId);
        await _repository.AddAsync(game);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(game.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(game.Id, result.Id);
        Assert.Equal("Pandemic", result.Title);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentGame_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Update_WithModifiedGame_PersistsChanges()
    {
        // Arrange
        var game = SharedGame.Create("Ticket to Ride", 2004, "Original description", 2, 5, 60, 8,
            null, null, "https://example.com/ticket.jpg", "https://example.com/ticket-thumb.jpg", null, TestUserId);
        await _repository.AddAsync(game);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear(); // Detach entities to simulate separate request

        // Act
        game.UpdateInfo(
            title: "Ticket to Ride",
            yearPublished: 2004,
            description: "Updated description - claim railway routes across North America",
            minPlayers: 2,
            maxPlayers: 5,
            playingTimeMinutes: 60,
            minAge: 8,
            complexityRating: null,
            averageRating: null,
            imageUrl: "https://example.com/ticket.jpg",
            thumbnailUrl: "https://example.com/ticket-thumb.jpg",
            rules: null,
            modifiedBy: TestUserId
        );
        _repository.Update(game);
        await _dbContext.SaveChangesAsync();

        // Assert
        var retrieved = await _repository.GetByIdAsync(game.Id);
        Assert.NotNull(retrieved);
        Assert.Contains("Updated description", retrieved.Description);
    }

    #endregion

    #region GetByBggIdAsync Tests

    [Fact]
    public async Task GetByBggIdAsync_WithExistingBggId_ReturnsGame()
    {
        // Arrange
        var game = SharedGame.Create("7 Wonders", 2010, "Draft cards to build civilizations", 2, 7, 30, 10,
            null, null, "https://example.com/7wonders.jpg", "https://example.com/7wonders-thumb.jpg", null, TestUserId, bggId: 68448);
        await _repository.AddAsync(game);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByBggIdAsync(68448);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(game.Id, result.Id);
        Assert.Equal(68448, result.BggId);
    }

    [Fact]
    public async Task ExistsByBggIdAsync_WithExistingBggId_ReturnsTrue()
    {
        // Arrange
        var game = SharedGame.Create("Wingspan", 2019, "Bird collection game", 1, 5, 60, 10,
            null, null, "https://example.com/wingspan.jpg", "https://example.com/wingspan-thumb.jpg", null, TestUserId, bggId: 266192);
        await _repository.AddAsync(game);
        await _dbContext.SaveChangesAsync();

        // Act
        var exists = await _repository.ExistsByBggIdAsync(266192);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsByBggIdAsync_WithNonExistentBggId_ReturnsFalse()
    {
        // Act
        var exists = await _repository.ExistsByBggIdAsync(999999);

        // Assert
        Assert.False(exists);
    }

    #endregion

    #region Full-Text Search Integration Tests (via DbContext)

    [Fact]
    public async Task FullTextSearch_WithMatchingTitle_FindsGame()
    {
        // Arrange - Create test games with Italian text
        var catan = SharedGame.Create("Catan", 1995, "Gioco di costruzione e commercio", 3, 4, 90, 10,
            null, null, "https://example.com/catan.jpg", "https://example.com/catan-thumb.jpg", null, TestUserId);
        catan.SubmitForApproval(TestUserId);
        catan.ApprovePublication(TestUserId);
        await _repository.AddAsync(catan);
        await _dbContext.SaveChangesAsync();

        // Act - Direct DbContext query to test FTS (same as SearchQueryHandler)
        var searchTerm = "catan";
        var games = await _dbContext.SharedGames
            .Where(g => g.Status == (int)GameStatus.Published)
            .Where(g => EF.Functions.ToTsVector("italian", g.Title + " " + g.Description)
                .Matches(EF.Functions.PlainToTsQuery("italian", searchTerm)))
            .ToListAsync();

        // Assert
        Assert.Single(games);
        Assert.Equal("Catan", games.First().Title);
    }

    [Fact]
    public async Task FullTextSearch_WithItalianText_FindsMatchingGames()
    {
        // Arrange - Italian descriptions
        var game1 = SharedGame.Create("Gioco A", 2020, "Avventura fantasy con draghi e cavalieri", 2, 4, 60, 12,
            null, null, "https://example.com/a.jpg", "https://example.com/a-thumb.jpg", null, TestUserId);
        game1.SubmitForApproval(TestUserId);
        game1.ApprovePublication(TestUserId);
        await _repository.AddAsync(game1);
        await _dbContext.SaveChangesAsync();

        // Act - Search Italian word "cavalieri"
        var searchTerm = "cavalieri";
        var games = await _dbContext.SharedGames
            .Where(g => g.Status == (int)GameStatus.Published)
            .Where(g => EF.Functions.ToTsVector("italian", g.Title + " " + g.Description)
                .Matches(EF.Functions.PlainToTsQuery("italian", searchTerm)))
            .ToListAsync();

        // Assert
        Assert.Single(games);
        Assert.Equal("Gioco A", games.First().Title);
        Assert.Contains("cavalieri", games.First().Description);
    }

    #endregion
}
