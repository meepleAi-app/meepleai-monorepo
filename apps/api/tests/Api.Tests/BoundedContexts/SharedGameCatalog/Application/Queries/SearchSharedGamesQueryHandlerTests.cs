using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Api.Tests.TestHelpers;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Integration tests for SearchSharedGamesQueryHandler.
/// Tests full-text search, filtering, pagination, and HybridCache integration.
/// Issue #2371 Phase 2 - Coverage gap resolution
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SearchSharedGamesQueryHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private SearchSharedGamesQueryHandler _handler = null!;
    private static readonly Guid TestUserId = Guid.NewGuid();

    public SearchSharedGamesQueryHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"search_handler_test_{Guid.NewGuid():N}";
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
        var mediatorMock = new Mock<IMediator>();
        var eventCollectorMock = new Mock<IDomainEventCollector>();
        eventCollectorMock.Setup(x => x.GetAndClearEvents())
            .Returns(new List<IDomainEvent>().AsReadOnly());

        // Fix: Use PostgreSQL DbContext with Testcontainers, not in-memory
        _dbContext = new MeepleAiDbContext(options, mediatorMock.Object, eventCollectorMock.Object);
        await _dbContext.Database.MigrateAsync();

        // Seed test user (FK requirement)
        await SeedTestUserAsync();

        // Initialize handler with real HybridCache (in-memory backend)
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddHybridCache();
        var serviceProvider = services.BuildServiceProvider();
        var cache = serviceProvider.GetRequiredService<HybridCache>();

        var loggerMock = new Mock<ILogger<SearchSharedGamesQueryHandler>>();

        _handler = new SearchSharedGamesQueryHandler(_dbContext, cache, loggerMock.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private async Task SeedTestUserAsync()
    {
        var user = new UserEntity
        {
            Id = TestUserId,
            Email = "testuser@meepleai.dev",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Set<UserEntity>().Add(user);
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedPublishedGamesAsync()
    {
        var games = new[]
        {
            new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                BggId = 13,
                Title = "Catan",
                YearPublished = 1995,
                Description = "Gioco di costruzione e commercio di risorse",
                MinPlayers = 3,
                MaxPlayers = 4,
                PlayingTimeMinutes = 90,
                MinAge = 10,
                ComplexityRating = 2.5m,
                AverageRating = 7.8m,
                ImageUrl = "https://example.com/catan.jpg",
                ThumbnailUrl = "https://example.com/catan-thumb.jpg",
                Status = (int)GameStatus.Published,
                CreatedBy = TestUserId,
                CreatedAt = DateTime.UtcNow
            },
            new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                BggId = 30549,
                Title = "Pandemic",
                YearPublished = 2008,
                Description = "Gioco cooperativo di epidemie globali",
                MinPlayers = 2,
                MaxPlayers = 4,
                PlayingTimeMinutes = 45,
                MinAge = 8,
                ComplexityRating = 2.4m,
                AverageRating = 7.6m,
                ImageUrl = "https://example.com/pandemic.jpg",
                ThumbnailUrl = "https://example.com/pandemic-thumb.jpg",
                Status = (int)GameStatus.Published,
                CreatedBy = TestUserId,
                CreatedAt = DateTime.UtcNow
            },
            new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                BggId = 174430,
                Title = "Gloomhaven",
                YearPublished = 2017,
                Description = "Gioco di avventura tattica fantasy",
                MinPlayers = 1,
                MaxPlayers = 4,
                PlayingTimeMinutes = 120,
                MinAge = 14,
                ComplexityRating = 3.8m,
                AverageRating = 8.9m,
                ImageUrl = "https://example.com/gloomhaven.jpg",
                ThumbnailUrl = "https://example.com/gloomhaven-thumb.jpg",
                Status = (int)GameStatus.Published,
                CreatedBy = TestUserId,
                CreatedAt = DateTime.UtcNow
            }
        };

        _dbContext.Set<SharedGameEntity>().AddRange(games);
        await _dbContext.SaveChangesAsync();
    }

    #region Basic Search Tests

    [Fact]
    public async Task Handle_WithNoFilters_ReturnsAllPublishedGames()
    {
        // Arrange
        await SeedPublishedGamesAsync();
        var query = new SearchSharedGamesQuery(
            SearchTerm: null,
            CategoryIds: null,
            MechanicIds: null,
            MinPlayers: null,
            MaxPlayers: null,
            MaxPlayingTime: null,
            MinComplexity: null,
            MaxComplexity: null,
            Status: null,
            PageNumber: 1,
            PageSize: 20);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
        result.Total.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_WithSearchTerm_UsesPostgreSqlFullTextSearch()
    {
        // Arrange
        await SeedPublishedGamesAsync();
        var query = new SearchSharedGamesQuery(
            SearchTerm: "cooperativo",
            CategoryIds: null,
            MechanicIds: null,
            MinPlayers: null,
            MaxPlayers: null,
            MaxPlayingTime: null,
            MinComplexity: null,
            MaxComplexity: null,
            Status: null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Title.Should().Be("Pandemic");
    }

    #endregion

    #region Player Count Filter Tests

    [Fact]
    public async Task Handle_WithMinPlayersFilter_ReturnsCompatibleGames()
    {
        // Arrange
        await SeedPublishedGamesAsync();
        var query = new SearchSharedGamesQuery(
            SearchTerm: null,
            CategoryIds: null,
            MechanicIds: null,
            MinPlayers: 3,
            MaxPlayers: null,
            MaxPlayingTime: null,
            MinComplexity: null,
            MaxComplexity: null,
            Status: null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - All games that CAN accommodate 3+ players (MaxPlayers >= 3)
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3); // All games have MaxPlayers >= 3
        result.Items.Should().Contain(g => g.Title == "Catan");
        result.Items.Should().Contain(g => g.Title == "Pandemic");
        result.Items.Should().Contain(g => g.Title == "Gloomhaven");
    }

    [Fact]
    public async Task Handle_WithMaxPlayersFilter_ReturnsCompatibleGames()
    {
        // Arrange
        await SeedPublishedGamesAsync();
        var query = new SearchSharedGamesQuery(
            SearchTerm: null,
            CategoryIds: null,
            MechanicIds: null,
            MinPlayers: null,
            MaxPlayers: 2,
            MaxPlayingTime: null,
            MinComplexity: null,
            MaxComplexity: null,
            Status: null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2); // Pandemic (2-4) and Gloomhaven (1-4)
        result.Items.Should().Contain(g => g.Title == "Pandemic");
        result.Items.Should().Contain(g => g.Title == "Gloomhaven");
    }

    #endregion

    #region Playing Time Filter Tests

    [Fact]
    public async Task Handle_WithMaxPlayingTimeFilter_ReturnsGamesWithinTime()
    {
        // Arrange
        await SeedPublishedGamesAsync();
        var query = new SearchSharedGamesQuery(
            SearchTerm: null,
            CategoryIds: null,
            MechanicIds: null,
            MinPlayers: null,
            MaxPlayers: null,
            MaxPlayingTime: 60,
            MinComplexity: null,
            MaxComplexity: null,
            Status: null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Title.Should().Be("Pandemic");
    }

    #endregion

    #region Status Filter Tests

    [Fact]
    public async Task Handle_WithDraftStatusFilter_ReturnsOnlyDraftGames()
    {
        // Arrange
        await SeedPublishedGamesAsync();
        var draftGame = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Draft Game",
            YearPublished = 2023,
            Description = "Test draft game",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = "https://example.com/draft.jpg",
            ThumbnailUrl = "https://example.com/draft-thumb.jpg",
            Status = (int)GameStatus.Draft,
            CreatedBy = TestUserId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Set<SharedGameEntity>().Add(draftGame);
        await _dbContext.SaveChangesAsync();

        var query = new SearchSharedGamesQuery(
            SearchTerm: null,
            CategoryIds: null,
            MechanicIds: null,
            MinPlayers: null,
            MaxPlayers: null,
            MaxPlayingTime: null,
            MinComplexity: null,
            MaxComplexity: null,
            Status: GameStatus.Draft);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Title.Should().Be("Draft Game");
        result.Items.First().Status.Should().Be(GameStatus.Draft);
    }

    #endregion

    #region Sorting Tests

    [Fact]
    public async Task Handle_WithYearPublishedSort_ReturnsSortedGames()
    {
        // Arrange
        await SeedPublishedGamesAsync();
        var query = new SearchSharedGamesQuery(
            SearchTerm: null,
            CategoryIds: null,
            MechanicIds: null,
            MinPlayers: null,
            MaxPlayers: null,
            MaxPlayingTime: null,
            MinComplexity: null,
            MaxComplexity: null,
            Status: null,
            SortBy: "YearPublished",
            SortDescending: false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
        result.Items.First().Title.Should().Be("Catan");
        result.Items.Last().Title.Should().Be("Gloomhaven");
    }

    [Fact]
    public async Task Handle_WithAverageRatingSort_ReturnsSortedGames()
    {
        // Arrange
        await SeedPublishedGamesAsync();
        var query = new SearchSharedGamesQuery(
            SearchTerm: null,
            CategoryIds: null,
            MechanicIds: null,
            MinPlayers: null,
            MaxPlayers: null,
            MaxPlayingTime: null,
            MinComplexity: null,
            MaxComplexity: null,
            Status: null,
            SortBy: "AverageRating",
            SortDescending: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
        result.Items.First().Title.Should().Be("Gloomhaven");
        result.Items.First().AverageRating.Should().Be(8.9m);
    }

    #endregion

    #region Pagination Tests

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        await SeedPublishedGamesAsync();
        var query = new SearchSharedGamesQuery(
            SearchTerm: null,
            CategoryIds: null,
            MechanicIds: null,
            MinPlayers: null,
            MaxPlayers: null,
            MaxPlayingTime: null,
            MinComplexity: null,
            MaxComplexity: null,
            Status: null,
            PageNumber: 1,
            PageSize: 2);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    #endregion
}
