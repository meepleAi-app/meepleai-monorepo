using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserShareRequests;
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

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserShareRequests;

/// <summary>
/// Integration tests for GetUserShareRequestsQueryHandler.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetUserShareRequestsQueryHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private GetUserShareRequestsQueryHandler _handler = null!;
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    public GetUserShareRequestsQueryHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"share_requests_test_{Guid.NewGuid():N}";
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

        await SeedTestDataAsync();

        var loggerMock = new Mock<ILogger<GetUserShareRequestsQueryHandler>>();
        _handler = new GetUserShareRequestsQueryHandler(_dbContext, loggerMock.Object);
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
            new UserEntity { Id = TestUserId, Email = "testuser@meepleai.dev", Role = "user", CreatedAt = DateTime.UtcNow },
            new UserEntity { Id = OtherUserId, Email = "other@meepleai.dev", Role = "user", CreatedAt = DateTime.UtcNow }
        };
        _dbContext.Set<UserEntity>().AddRange(users);

        // Seed test games
        var games = new[]
        {
            CreateSharedGame("Game 1", 1),
            CreateSharedGame("Game 2", 2),
            CreateSharedGame("Game 3", 3)
        };
        _dbContext.Set<SharedGameEntity>().AddRange(games);
        await _dbContext.SaveChangesAsync();

        // Seed share requests
        var requests = new[]
        {
            CreateShareRequest(TestUserId, games[0].Id, ShareRequestStatus.Pending),
            CreateShareRequest(TestUserId, games[1].Id, ShareRequestStatus.Approved),
            CreateShareRequest(TestUserId, games[2].Id, ShareRequestStatus.Rejected),
            CreateShareRequest(OtherUserId, games[0].Id, ShareRequestStatus.Pending) // Other user's request
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

    private static ShareRequestEntity CreateShareRequest(Guid userId, Guid sourceGameId, ShareRequestStatus status) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        SourceGameId = sourceGameId,
        Status = (int)status,
        ContributionType = (int)ContributionType.NewGame,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = userId,
        ResolvedAt = status is ShareRequestStatus.Approved or ShareRequestStatus.Rejected ? DateTime.UtcNow : null
    };

    [Fact]
    public async Task Handle_ShouldReturnOnlyUserRequests()
    {
        // Arrange
        var query = new GetUserShareRequestsQuery(TestUserId, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(3);
        result.Items.Should().HaveCount(3);
        result.Items.Should().AllSatisfy(r => r.SourceGameId.Should().NotBeEmpty());
    }

    [Fact]
    public async Task Handle_WithStatusFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        var query = new GetUserShareRequestsQuery(TestUserId, ShareRequestStatus.Pending);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items.First().Status.Should().Be(ShareRequestStatus.Pending);
    }

    [Fact]
    public async Task Handle_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var query = new GetUserShareRequestsQuery(TestUserId, null, PageNumber: 1, PageSize: 2);

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
    public async Task Handle_ForUserWithNoRequests_ShouldReturnEmptyResult()
    {
        // Arrange
        var newUserId = Guid.NewGuid();
        _dbContext.Set<UserEntity>().Add(new UserEntity
        {
            Id = newUserId,
            Email = "newuser@meepleai.dev",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var query = new GetUserShareRequestsQuery(newUserId, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldIncludeGameTitleAndThumbnail()
    {
        // Arrange
        var query = new GetUserShareRequestsQuery(TestUserId, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().AllSatisfy(r =>
        {
            r.GameTitle.Should().NotBeNullOrEmpty();
            r.GameTitle.Should().NotBe("Unknown Game");
        });
    }

    [Fact]
    public async Task Handle_ShouldIncludeAttachedDocumentCount()
    {
        // Arrange
        var query = new GetUserShareRequestsQuery(TestUserId, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().AllSatisfy(r =>
        {
            r.AttachedDocumentCount.Should().BeGreaterThanOrEqualTo(0);
        });
    }
}
