using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetPendingShareRequests;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetPendingShareRequests;

/// <summary>
/// Integration tests for GetPendingShareRequestsQueryHandler.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetPendingShareRequestsQueryHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private GetPendingShareRequestsQueryHandler _handler = null!;
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid AdminUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    public GetPendingShareRequestsQueryHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"pending_requests_test_{Guid.NewGuid():N}";
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

        await SeedTestDataAsync();

        var loggerMock = new Mock<ILogger<GetPendingShareRequestsQueryHandler>>();
        _handler = new GetPendingShareRequestsQueryHandler(_dbContext, loggerMock.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private async Task SeedTestDataAsync()
    {
        // Seed test users
        var users = new[]
        {
            new UserEntity { Id = TestUserId, Email = "testuser@meepleai.dev", DisplayName = "Test User", Role = "user", CreatedAt = DateTime.UtcNow },
            new UserEntity { Id = AdminUserId, Email = "admin@meepleai.dev", DisplayName = "Admin User", Role = "admin", CreatedAt = DateTime.UtcNow },
            new UserEntity { Id = OtherUserId, Email = "other@meepleai.dev", DisplayName = "Other User", Role = "user", CreatedAt = DateTime.UtcNow }
        };
        _dbContext.Set<UserEntity>().AddRange(users);

        // Seed test games
        var games = new[]
        {
            CreateSharedGame("Catan", 13),
            CreateSharedGame("Ticket to Ride", 9209),
            CreateSharedGame("Pandemic", 30549),
            CreateSharedGame("7 Wonders", 68448)
        };
        _dbContext.Set<SharedGameEntity>().AddRange(games);
        await _dbContext.SaveChangesAsync();

        // Seed share requests with various statuses
        var requests = new[]
        {
            CreateShareRequest(TestUserId, games[0].Id, ShareRequestStatus.Pending, "Please review Catan"),
            CreateShareRequest(TestUserId, games[1].Id, ShareRequestStatus.InReview, "Ticket to Ride rules", AdminUserId),
            CreateShareRequest(OtherUserId, games[2].Id, ShareRequestStatus.ChangesRequested, "Pandemic updated"),
            CreateShareRequest(OtherUserId, games[3].Id, ShareRequestStatus.Approved, null), // Terminal - should not appear
            CreateShareRequest(TestUserId, games[2].Id, ShareRequestStatus.Rejected, null) // Terminal - should not appear
        };
        _dbContext.Set<ShareRequestEntity>().AddRange(requests);
        await _dbContext.SaveChangesAsync();
    }

    private static SharedGameEntity CreateSharedGame(string title, int bggId) => new()
    {
        Id = Guid.NewGuid(),
        BggId = bggId,
        Title = title,
        YearPublished = 2020,
        Description = $"Description for {title}",
        MinPlayers = 2,
        MaxPlayers = 4,
        PlayingTimeMinutes = 60,
        MinAge = 10,
        ImageUrl = $"https://example.com/{title}.jpg",
        ThumbnailUrl = $"https://example.com/{title}_thumb.jpg",
        Status = (int)GameStatus.Published,
        CreatedBy = TestUserId,
        CreatedAt = DateTime.UtcNow
    };

    private static ShareRequestEntity CreateShareRequest(
        Guid userId,
        Guid sourceGameId,
        ShareRequestStatus status,
        string? notes,
        Guid? reviewingAdminId = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        SourceGameId = sourceGameId,
        Status = (int)status,
        ContributionType = (int)ContributionType.NewGame,
        UserNotes = notes,
        ReviewingAdminId = reviewingAdminId,
        ReviewStartedAt = reviewingAdminId.HasValue ? DateTime.UtcNow : null,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = userId,
        ResolvedAt = status is ShareRequestStatus.Approved or ShareRequestStatus.Rejected ? DateTime.UtcNow : null
    };

    [Fact]
    public async Task Handle_ShouldReturnOnlyActiveRequests()
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(3); // Pending, InReview, ChangesRequested only
        result.Items.Should().HaveCount(3);
        result.Items.Should().NotContain(r =>
            r.Status == ShareRequestStatus.Approved ||
            r.Status == ShareRequestStatus.Rejected);
    }

    [Fact]
    public async Task Handle_WithStatusFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(ShareRequestStatus.Pending, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items.First().Status.Should().Be(ShareRequestStatus.Pending);
    }

    [Fact]
    public async Task Handle_WithTypeFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(null, ContributionType.NewGame, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().AllSatisfy(r => r.ContributionType.Should().Be(ContributionType.NewGame));
    }

    [Fact]
    public async Task Handle_WithSearchTerm_ShouldReturnMatchingResults()
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(null, null, "Catan");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(1);
        result.Items.First().GameTitle.Should().Contain("Catan");
    }

    [Fact]
    public async Task Handle_WithSearchTermInNotes_ShouldReturnMatchingResults()
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(null, null, "Ticket");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().Contain(r => r.UserNotes != null && r.UserNotes.Contains("Ticket"));
    }

    [Fact]
    public async Task Handle_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(null, null, null, PageNumber: 1, PageSize: 2);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(3);
        result.Items.Should().HaveCount(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithSortByCreatedAt_ShouldSortCorrectly()
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(
            null, null, null,
            SortBy: ShareRequestSortField.CreatedAt,
            SortDirection: SortDirection.Ascending);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeInAscendingOrder(r => r.CreatedAt);
    }

    [Fact]
    public async Task Handle_WithSortByGameTitle_ShouldSortCorrectly()
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(
            null, null, null,
            SortBy: ShareRequestSortField.GameTitle,
            SortDirection: SortDirection.Ascending);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeInAscendingOrder(r => r.GameTitle);
    }

    [Fact]
    public async Task Handle_ShouldIncludeUserInfo()
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().AllSatisfy(r =>
        {
            r.UserName.Should().NotBeNullOrEmpty();
            r.UserId.Should().NotBeEmpty();
        });
    }

    [Fact]
    public async Task Handle_ShouldIncludeLockInfo()
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(ShareRequestStatus.InReview, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        var lockedRequest = result.Items.First();
        lockedRequest.IsInReview.Should().BeTrue();
        lockedRequest.ReviewingAdminId.Should().Be(AdminUserId);
        lockedRequest.ReviewingAdminName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_ShouldIncludeGameDetails()
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().AllSatisfy(r =>
        {
            r.GameTitle.Should().NotBeNullOrEmpty();
            r.GameTitle.Should().NotBe("Unknown Game");
            r.SourceGameId.Should().NotBeEmpty();
        });
    }

    [Fact]
    public async Task Handle_WithEmptySearchTerm_ShouldReturnAllActiveRequests()
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(null, null, "   ");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WaitingTimeShouldBeCalculated()
    {
        // Arrange
        var query = new GetPendingShareRequestsQuery(null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().AllSatisfy(r =>
        {
            r.WaitingTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        });
    }
}
