using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Integration tests for GetFilteredSharedGamesQueryHandler.
/// Tests filtered listing with status, search, pagination, and sorting.
/// Issue #3533: Admin API Endpoints - Filtered Games List
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetFilteredSharedGamesQueryHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private GetFilteredSharedGamesQueryHandler _handler = null!;
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    public GetFilteredSharedGamesQueryHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"filtered_games_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var mediatorMock = new Mock<IMediator>();
        var eventCollectorMock = new Mock<IDomainEventCollector>();
        eventCollectorMock.Setup(x => x.GetAndClearEvents())
            .Returns(new List<IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mediatorMock.Object, eventCollectorMock.Object);
        await _dbContext.Database.MigrateAsync();

        await SeedTestUsersAsync();

        var loggerMock = new Mock<ILogger<GetFilteredSharedGamesQueryHandler>>();
        _handler = new GetFilteredSharedGamesQueryHandler(_dbContext, loggerMock.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private async Task SeedTestUsersAsync()
    {
        var users = new[]
        {
            new UserEntity
            {
                Id = TestUserId,
                Email = "testuser@meepleai.dev",
                Role = "admin",
                CreatedAt = DateTime.UtcNow
            },
            new UserEntity
            {
                Id = OtherUserId,
                Email = "otheruser@meepleai.dev",
                Role = "editor",
                CreatedAt = DateTime.UtcNow
            }
        };
        _dbContext.Set<UserEntity>().AddRange(users);
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedMixedGamesAsync()
    {
        var games = new[]
        {
            new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                BggId = 13,
                Title = "Catan",
                YearPublished = 1995,
                Description = "Trade and build settlements",
                MinPlayers = 3,
                MaxPlayers = 4,
                PlayingTimeMinutes = 90,
                MinAge = 10,
                ImageUrl = "https://example.com/catan.jpg",
                ThumbnailUrl = "https://example.com/catan-thumb.jpg",
                Status = (int)GameStatus.Published,
                CreatedBy = TestUserId,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                ModifiedAt = DateTime.UtcNow.AddDays(-5)
            },
            new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                BggId = 30549,
                Title = "Pandemic",
                YearPublished = 2008,
                Description = "Cooperative disease outbreak game",
                MinPlayers = 2,
                MaxPlayers = 4,
                PlayingTimeMinutes = 45,
                MinAge = 8,
                ImageUrl = "https://example.com/pandemic.jpg",
                ThumbnailUrl = "https://example.com/pandemic-thumb.jpg",
                Status = (int)GameStatus.Published,
                CreatedBy = OtherUserId,
                CreatedAt = DateTime.UtcNow.AddDays(-8),
                ModifiedAt = DateTime.UtcNow.AddDays(-3)
            },
            new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                Title = "Draft Game A",
                YearPublished = 2023,
                Description = "Work in progress board game",
                MinPlayers = 2,
                MaxPlayers = 4,
                PlayingTimeMinutes = 60,
                MinAge = 10,
                ImageUrl = "https://example.com/draft-a.jpg",
                ThumbnailUrl = "https://example.com/draft-a-thumb.jpg",
                Status = (int)GameStatus.Draft,
                CreatedBy = TestUserId,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                ModifiedAt = DateTime.UtcNow.AddDays(-2)
            },
            new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                Title = "Pending Approval Game",
                YearPublished = 2024,
                Description = "Awaiting admin review",
                MinPlayers = 1,
                MaxPlayers = 6,
                PlayingTimeMinutes = 120,
                MinAge = 12,
                ImageUrl = "https://example.com/pending.jpg",
                ThumbnailUrl = "https://example.com/pending-thumb.jpg",
                Status = (int)GameStatus.PendingApproval,
                CreatedBy = OtherUserId,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                ModifiedAt = DateTime.UtcNow.AddDays(-1)
            },
            new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                Title = "Archived Classic",
                YearPublished = 2010,
                Description = "No longer available in catalog",
                MinPlayers = 2,
                MaxPlayers = 4,
                PlayingTimeMinutes = 90,
                MinAge = 10,
                ImageUrl = "https://example.com/archived.jpg",
                ThumbnailUrl = "https://example.com/archived-thumb.jpg",
                Status = (int)GameStatus.Archived,
                CreatedBy = TestUserId,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                ModifiedAt = DateTime.UtcNow.AddDays(-20)
            }
        };

        _dbContext.Set<SharedGameEntity>().AddRange(games);
        await _dbContext.SaveChangesAsync();
    }

    #region No Filter Tests

    [Fact]
    public async Task Handle_WithNoFilters_ReturnsAllGames()
    {
        // Arrange
        await SeedMixedGamesAsync();
        var query = new GetFilteredSharedGamesQuery();

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

    #region Status Filter Tests

    [Fact]
    public async Task Handle_WithPublishedStatusFilter_ReturnsOnlyPublishedGames()
    {
        // Arrange
        await SeedMixedGamesAsync();
        var query = new GetFilteredSharedGamesQuery(Status: GameStatus.Published);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Items.Should().AllSatisfy(g => g.Status.Should().Be(GameStatus.Published));
    }

    [Fact]
    public async Task Handle_WithDraftStatusFilter_ReturnsOnlyDraftGames()
    {
        // Arrange
        await SeedMixedGamesAsync();
        var query = new GetFilteredSharedGamesQuery(Status: GameStatus.Draft);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Title.Should().Be("Draft Game A");
    }

    [Fact]
    public async Task Handle_WithPendingApprovalStatusFilter_ReturnsOnlyPendingGames()
    {
        // Arrange
        await SeedMixedGamesAsync();
        var query = new GetFilteredSharedGamesQuery(Status: GameStatus.PendingApproval);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Title.Should().Be("Pending Approval Game");
    }

    #endregion

    #region Search Filter Tests

    [Fact]
    public async Task Handle_WithSearchTerm_FiltersGamesByTitle()
    {
        // Arrange
        await SeedMixedGamesAsync();
        var query = new GetFilteredSharedGamesQuery(Search: "Catan");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Title.Should().Be("Catan");
    }

    [Fact]
    public async Task Handle_WithSearchTerm_FiltersGamesByDescription()
    {
        // Arrange
        await SeedMixedGamesAsync();
        var query = new GetFilteredSharedGamesQuery(Search: "disease");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Title.Should().Be("Pandemic");
    }

    [Fact]
    public async Task Handle_WithPartialSearchTerm_ReturnsMatchingGames()
    {
        // Arrange
        await SeedMixedGamesAsync();
        var query = new GetFilteredSharedGamesQuery(Search: "game");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    #endregion

    #region Submitter Filter Tests

    [Fact]
    public async Task Handle_WithSubmitterFilter_ReturnsOnlyGamesFromSubmitter()
    {
        // Arrange
        await SeedMixedGamesAsync();
        var query = new GetFilteredSharedGamesQuery(SubmittedBy: TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3); // Catan, Draft Game A, Archived Classic
    }

    #endregion

    #region Pagination Tests

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        await SeedMixedGamesAsync();
        var query = new GetFilteredSharedGamesQuery(PageNumber: 1, PageSize: 2);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(5);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithSecondPage_ReturnsRemainingItems()
    {
        // Arrange
        await SeedMixedGamesAsync();
        var query = new GetFilteredSharedGamesQuery(PageNumber: 2, PageSize: 2);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(5);
        result.Page.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithLastPage_ReturnsPartialPage()
    {
        // Arrange
        await SeedMixedGamesAsync();
        var query = new GetFilteredSharedGamesQuery(PageNumber: 3, PageSize: 2);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(5);
        result.Page.Should().Be(3);
    }

    #endregion

    #region Sorting Tests

    [Fact]
    public async Task Handle_WithTitleSortAsc_ReturnsSortedByTitle()
    {
        // Arrange
        await SeedMixedGamesAsync();
        var query = new GetFilteredSharedGamesQuery(SortBy: "title:asc");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(5);
        result.Items.First().Title.Should().Be("Archived Classic");
    }

    [Fact]
    public async Task Handle_WithTitleSortDesc_ReturnsSortedByTitleDescending()
    {
        // Arrange
        await SeedMixedGamesAsync();
        var query = new GetFilteredSharedGamesQuery(SortBy: "title:desc");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(5);
        result.Items.First().Title.Should().Be("Pending Approval Game");
    }

    [Fact]
    public async Task Handle_WithDefaultSort_SortsByModifiedAtDescending()
    {
        // Arrange
        await SeedMixedGamesAsync();
        var query = new GetFilteredSharedGamesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        // Most recently modified should be first
        result.Items.First().Title.Should().Be("Pending Approval Game");
    }

    #endregion

    #region Combined Filters Tests

    [Fact]
    public async Task Handle_WithStatusAndSearch_AppliesBothFilters()
    {
        // Arrange
        await SeedMixedGamesAsync();
        var query = new GetFilteredSharedGamesQuery(
            Status: GameStatus.Published,
            Search: "Catan");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Title.Should().Be("Catan");
    }

    [Fact]
    public async Task Handle_WithStatusAndSubmitter_AppliesBothFilters()
    {
        // Arrange
        await SeedMixedGamesAsync();
        var query = new GetFilteredSharedGamesQuery(
            Status: GameStatus.Published,
            SubmittedBy: TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Title.Should().Be("Catan");
    }

    #endregion

    #region Empty Results Tests

    [Fact]
    public async Task Handle_WithNoMatchingSearch_ReturnsEmptyResults()
    {
        // Arrange
        await SeedMixedGamesAsync();
        var query = new GetFilteredSharedGamesQuery(Search: "NonexistentGame");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithEmptyDatabase_ReturnsEmptyResults()
    {
        // Arrange - no seeding
        var query = new GetFilteredSharedGamesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None));
    }

    #endregion
}
