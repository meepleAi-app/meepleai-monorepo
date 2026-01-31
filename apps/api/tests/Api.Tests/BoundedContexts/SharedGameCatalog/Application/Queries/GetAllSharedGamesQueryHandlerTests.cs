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
using Microsoft.Extensions.Logging;
using Moq;
using Api.Tests.TestHelpers;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Integration tests for GetAllSharedGamesQueryHandler.
/// Tests basic listing with status filters and pagination.
/// Issue #2371 Phase 2 - Coverage gap resolution
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetAllSharedGamesQueryHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private GetAllSharedGamesQueryHandler _handler = null!;
    private static readonly Guid TestUserId = Guid.NewGuid();

    public GetAllSharedGamesQueryHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"getall_handler_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        // Create unique test database using fixture helper
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        // Build DbContext with test database
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString)
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

        // Initialize handler (NO HybridCache parameter!)
        var loggerMock = new Mock<ILogger<GetAllSharedGamesQueryHandler>>();
        _handler = new GetAllSharedGamesQueryHandler(_dbContext, loggerMock.Object);
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

    private async Task SeedMixedStatusGamesAsync()
    {
        var games = new[]
        {
            new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                BggId = 13,
                Title = "Catan",
                YearPublished = 1995,
                Description = "Trade and build",
                MinPlayers = 3,
                MaxPlayers = 4,
                PlayingTimeMinutes = 90,
                MinAge = 10,
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
                Description = "Cooperative disease outbreak",
                MinPlayers = 2,
                MaxPlayers = 4,
                PlayingTimeMinutes = 45,
                MinAge = 8,
                ImageUrl = "https://example.com/pandemic.jpg",
                ThumbnailUrl = "https://example.com/pandemic-thumb.jpg",
                Status = (int)GameStatus.Published,
                CreatedBy = TestUserId,
                CreatedAt = DateTime.UtcNow
            },
            new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                Title = "Draft Game A",
                YearPublished = 2023,
                Description = "Work in progress",
                MinPlayers = 2,
                MaxPlayers = 4,
                PlayingTimeMinutes = 60,
                MinAge = 10,
                ImageUrl = "https://example.com/draft-a.jpg",
                ThumbnailUrl = "https://example.com/draft-a-thumb.jpg",
                Status = (int)GameStatus.Draft,
                CreatedBy = TestUserId,
                CreatedAt = DateTime.UtcNow
            },
            new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                Title = "Draft Game B",
                YearPublished = 2024,
                Description = "Under development",
                MinPlayers = 1,
                MaxPlayers = 6,
                PlayingTimeMinutes = 120,
                MinAge = 12,
                ImageUrl = "https://example.com/draft-b.jpg",
                ThumbnailUrl = "https://example.com/draft-b-thumb.jpg",
                Status = (int)GameStatus.Draft,
                CreatedBy = TestUserId,
                CreatedAt = DateTime.UtcNow
            },
            new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                Title = "Archived Game",
                YearPublished = 2020,
                Description = "No longer available",
                MinPlayers = 2,
                MaxPlayers = 4,
                PlayingTimeMinutes = 90,
                MinAge = 10,
                ImageUrl = "https://example.com/archived.jpg",
                ThumbnailUrl = "https://example.com/archived-thumb.jpg",
                Status = (int)GameStatus.Archived,
                CreatedBy = TestUserId,
                CreatedAt = DateTime.UtcNow
            }
        };

        _dbContext.Set<SharedGameEntity>().AddRange(games);
        await _dbContext.SaveChangesAsync();
    }

    #region No Status Filter Tests

    [Fact]
    public async Task Handle_WithNoStatusFilter_ReturnsAllGames()
    {
        // Arrange
        await SeedMixedStatusGamesAsync();
        var query = new GetAllSharedGamesQuery(
            Status: null,
            PageNumber: 1,
            PageSize: 20);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(5);
        result.Total.Should().Be(5);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    #endregion

    #region Published Status Filter Tests

    [Fact]
    public async Task Handle_WithPublishedStatusFilter_ReturnsOnlyPublishedGames()
    {
        // Arrange
        await SeedMixedStatusGamesAsync();
        var query = new GetAllSharedGamesQuery(
            Status: GameStatus.Published,
            PageNumber: 1,
            PageSize: 20);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Items.Should().AllSatisfy(g => g.Status.Should().Be(GameStatus.Published));
        result.Items.Should().Contain(g => g.Title == "Catan");
        result.Items.Should().Contain(g => g.Title == "Pandemic");
    }

    #endregion

    #region Draft Status Filter Tests

    [Fact]
    public async Task Handle_WithDraftStatusFilter_ReturnsOnlyDraftGames()
    {
        // Arrange
        await SeedMixedStatusGamesAsync();
        var query = new GetAllSharedGamesQuery(
            Status: GameStatus.Draft,
            PageNumber: 1,
            PageSize: 20);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Items.Should().AllSatisfy(g => g.Status.Should().Be(GameStatus.Draft));
        result.Items.Should().Contain(g => g.Title == "Draft Game A");
        result.Items.Should().Contain(g => g.Title == "Draft Game B");
    }

    #endregion

    #region Archived Status Filter Tests

    [Fact]
    public async Task Handle_WithArchivedStatusFilter_ReturnsOnlyArchivedGames()
    {
        // Arrange
        await SeedMixedStatusGamesAsync();
        var query = new GetAllSharedGamesQuery(
            Status: GameStatus.Archived,
            PageNumber: 1,
            PageSize: 20);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Items.First().Status.Should().Be(GameStatus.Archived);
        result.Items.First().Title.Should().Be("Archived Game");
    }

    #endregion

    #region Pagination Tests

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        await SeedMixedStatusGamesAsync();
        var query = new GetAllSharedGamesQuery(
            Status: null,
            PageNumber: 1,
            PageSize: 3);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
        result.Total.Should().Be(5);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WithSecondPage_ReturnsRemainingItems()
    {
        // Arrange
        await SeedMixedStatusGamesAsync();
        var query = new GetAllSharedGamesQuery(
            Status: null,
            PageNumber: 2,
            PageSize: 3);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(5);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(3);
    }

    #endregion

    #region Sorting Tests

    [Fact]
    public async Task Handle_SortsByTitleByDefault()
    {
        // Arrange
        await SeedMixedStatusGamesAsync();
        var query = new GetAllSharedGamesQuery(
            Status: null,
            PageNumber: 1,
            PageSize: 20);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(5);
        // Verify alphabetical order (Archived, Catan, Draft A, Draft B, Pandemic)
        result.Items.First().Title.Should().Be("Archived Game");
        result.Items.Last().Title.Should().Be("Pandemic");
    }

    #endregion
}