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
/// Integration tests for GetApprovalQueueQueryHandler.
/// Tests approval queue with urgency, submitter, and PDF filters.
/// Issue #3533: Admin API Endpoints - Approval Queue Management
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetApprovalQueueQueryHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private GetApprovalQueueQueryHandler _handler = null!;
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid EditorUserId = Guid.NewGuid();

    public GetApprovalQueueQueryHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"approval_queue_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector()) // Issue #3547
            .Options;

        var mediatorMock = new Mock<IMediator>();
        var eventCollectorMock = new Mock<IDomainEventCollector>();
        eventCollectorMock.Setup(x => x.GetAndClearEvents())
            .Returns(new List<IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mediatorMock.Object, eventCollectorMock.Object);
        await _dbContext.Database.MigrateAsync();

        await SeedTestUsersAsync();

        var loggerMock = new Mock<ILogger<GetApprovalQueueQueryHandler>>();
        _handler = new GetApprovalQueueQueryHandler(_dbContext, loggerMock.Object);
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
                Email = "admin@meepleai.dev",
                Role = "admin",
                CreatedAt = DateTime.UtcNow
            },
            new UserEntity
            {
                Id = EditorUserId,
                Email = "editor@meepleai.dev",
                Role = "editor",
                CreatedAt = DateTime.UtcNow
            }
        };
        _dbContext.Set<UserEntity>().AddRange(users);
        await _dbContext.SaveChangesAsync();
    }

    private async Task<List<Guid>> SeedPendingApprovalGamesAsync()
    {
        var gameIds = new List<Guid>();

        // Game 1: Pending for 10 days (urgent), submitted by editor, with PDF
        var gameId1 = Guid.NewGuid();
        gameIds.Add(gameId1);
        var game1 = new SharedGameEntity
        {
            Id = gameId1,
            Title = "Urgent Game With PDF",
            YearPublished = 2024,
            Description = "Pending for over 7 days",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = "https://example.com/urgent.jpg",
            ThumbnailUrl = "https://example.com/urgent-thumb.jpg",
            Status = (int)GameStatus.PendingApproval,
            CreatedBy = EditorUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        // Game 2: Pending for 3 days (not urgent), submitted by admin, no PDF
        var gameId2 = Guid.NewGuid();
        gameIds.Add(gameId2);
        var game2 = new SharedGameEntity
        {
            Id = gameId2,
            Title = "Recent Game No PDF",
            YearPublished = 2024,
            Description = "Recently submitted",
            MinPlayers = 2,
            MaxPlayers = 6,
            PlayingTimeMinutes = 90,
            MinAge = 12,
            ImageUrl = "https://example.com/recent.jpg",
            ThumbnailUrl = "https://example.com/recent-thumb.jpg",
            Status = (int)GameStatus.PendingApproval,
            CreatedBy = TestUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        };

        // Game 3: Pending for 8 days (urgent), submitted by editor, no PDF
        var gameId3 = Guid.NewGuid();
        gameIds.Add(gameId3);
        var game3 = new SharedGameEntity
        {
            Id = gameId3,
            Title = "Urgent Game No PDF",
            YearPublished = 2023,
            Description = "Another urgent submission",
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 45,
            MinAge = 8,
            ImageUrl = "https://example.com/urgent2.jpg",
            ThumbnailUrl = "https://example.com/urgent2-thumb.jpg",
            Status = (int)GameStatus.PendingApproval,
            CreatedBy = EditorUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-8)
        };

        // Game 4: Published (not in queue)
        var game4 = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Published Game",
            YearPublished = 2022,
            Description = "Already published",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = "https://example.com/published.jpg",
            ThumbnailUrl = "https://example.com/published-thumb.jpg",
            Status = (int)GameStatus.Published,
            CreatedBy = TestUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };

        _dbContext.Set<SharedGameEntity>().AddRange(game1, game2, game3, game4);
        await _dbContext.SaveChangesAsync();

        // Add PDF document to game1
        var document = new SharedGameDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = gameId1,
            PdfDocumentId = Guid.NewGuid(),
            DocumentType = 0, // Rulebook
            Version = "1.0",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-9),
            CreatedBy = EditorUserId,
            ApprovalStatus = 0 // Pending
        };
        _dbContext.Set<SharedGameDocumentEntity>().Add(document);
        await _dbContext.SaveChangesAsync();

        return gameIds;
    }

    #region Basic Query Tests

    [Fact]
    public async Task Handle_WithNoFilters_ReturnsAllPendingApprovalGames()
    {
        // Arrange
        await SeedPendingApprovalGamesAsync();
        var query = new GetApprovalQueueQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(item => item.DaysPending.Should().BeGreaterThanOrEqualTo(0));
    }

    [Fact]
    public async Task Handle_ReturnsGamesOrderedBySubmittedAtAscending()
    {
        // Arrange
        await SeedPendingApprovalGamesAsync();
        var query = new GetApprovalQueueQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        // Oldest first (10 days ago, then 8 days ago, then 3 days ago)
        result.First().Title.Should().Be("Urgent Game With PDF");
        result.Last().Title.Should().Be("Recent Game No PDF");
    }

    [Fact]
    public async Task Handle_ExcludesNonPendingApprovalGames()
    {
        // Arrange
        await SeedPendingApprovalGamesAsync();
        var query = new GetApprovalQueueQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotContain(item => item.Title == "Published Game");
    }

    #endregion

    #region Urgency Filter Tests

    [Fact]
    public async Task Handle_WithUrgencyFilterTrue_ReturnsOnlyUrgentGames()
    {
        // Arrange
        await SeedPendingApprovalGamesAsync();
        var query = new GetApprovalQueueQuery(Urgency: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2); // Only games pending > 7 days
        result.Should().AllSatisfy(item => item.DaysPending.Should().BeGreaterThanOrEqualTo(7));
    }

    [Fact]
    public async Task Handle_WithUrgencyFilterFalse_ReturnsAllGames()
    {
        // Arrange
        await SeedPendingApprovalGamesAsync();
        var query = new GetApprovalQueueQuery(Urgency: false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
    }

    #endregion

    #region Submitter Filter Tests

    [Fact]
    public async Task Handle_WithSubmitterFilter_ReturnsOnlyGamesFromSubmitter()
    {
        // Arrange
        await SeedPendingApprovalGamesAsync();
        var query = new GetApprovalQueueQuery(Submitter: EditorUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(item => item.SubmittedBy.Should().Be(EditorUserId));
    }

    [Fact]
    public async Task Handle_WithAdminSubmitterFilter_ReturnsAdminSubmissions()
    {
        // Arrange
        await SeedPendingApprovalGamesAsync();
        var query = new GetApprovalQueueQuery(Submitter: TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Recent Game No PDF");
    }

    #endregion

    #region HasPdfs Filter Tests

    [Fact]
    public async Task Handle_WithHasPdfsTrue_ReturnsOnlyGamesWithPdfs()
    {
        // Arrange
        await SeedPendingApprovalGamesAsync();
        var query = new GetApprovalQueueQuery(HasPdfs: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Urgent Game With PDF");
        result.First().PdfCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_WithHasPdfsFalse_ReturnsOnlyGamesWithoutPdfs()
    {
        // Arrange
        await SeedPendingApprovalGamesAsync();
        var query = new GetApprovalQueueQuery(HasPdfs: false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(item => item.PdfCount.Should().Be(0));
    }

    #endregion

    #region Combined Filters Tests

    [Fact]
    public async Task Handle_WithUrgencyAndSubmitter_AppliesBothFilters()
    {
        // Arrange
        await SeedPendingApprovalGamesAsync();
        var query = new GetApprovalQueueQuery(Urgency: true, Submitter: EditorUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(item =>
        {
            item.DaysPending.Should().BeGreaterThanOrEqualTo(7);
            item.SubmittedBy.Should().Be(EditorUserId);
        });
    }

    [Fact]
    public async Task Handle_WithUrgencyAndHasPdfs_AppliesBothFilters()
    {
        // Arrange
        await SeedPendingApprovalGamesAsync();
        var query = new GetApprovalQueueQuery(Urgency: true, HasPdfs: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Urgent Game With PDF");
    }

    [Fact]
    public async Task Handle_WithAllFilters_AppliesAllFilters()
    {
        // Arrange
        await SeedPendingApprovalGamesAsync();
        var query = new GetApprovalQueueQuery(
            Urgency: true,
            Submitter: EditorUserId,
            HasPdfs: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        var item = result.First();
        item.Title.Should().Be("Urgent Game With PDF");
        item.DaysPending.Should().BeGreaterThanOrEqualTo(7);
        item.SubmittedBy.Should().Be(EditorUserId);
        item.PdfCount.Should().BeGreaterThan(0);
    }

    #endregion

    #region Empty Results Tests

    [Fact]
    public async Task Handle_WithEmptyDatabase_ReturnsEmptyResults()
    {
        // Arrange - no seeding
        var query = new GetApprovalQueueQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithNoMatchingFilters_ReturnsEmptyResults()
    {
        // Arrange
        await SeedPendingApprovalGamesAsync();
        var query = new GetApprovalQueueQuery(
            Urgency: true,
            Submitter: TestUserId, // Admin only has 1 submission at 3 days (not urgent)
            HasPdfs: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region DaysPending Calculation Tests

    [Fact]
    public async Task Handle_CalculatesDaysPendingCorrectly()
    {
        // Arrange
        await SeedPendingApprovalGamesAsync();
        var query = new GetApprovalQueueQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var urgentGame = result.First(g => g.Title == "Urgent Game With PDF");
        urgentGame.DaysPending.Should().Be(10);

        var recentGame = result.First(g => g.Title == "Recent Game No PDF");
        recentGame.DaysPending.Should().Be(3);
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
