using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserContributionStats;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
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
using System.Text.Json;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserContributionStats;

/// <summary>
/// Integration tests for GetUserContributionStatsQueryHandler.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetUserContributionStatsQueryHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private GetUserContributionStatsQueryHandler _handler = null!;
    private Mock<IRateLimitEvaluator> _rateLimitEvaluatorMock = null!;
    private static readonly Guid TestUserId = Guid.NewGuid();

    public GetUserContributionStatsQueryHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"contrib_stats_test_{Guid.NewGuid():N}";
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

        _rateLimitEvaluatorMock = new Mock<IRateLimitEvaluator>();
        _rateLimitEvaluatorMock
            .Setup(x => x.GetUserStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDefaultRateLimitStatus());

        var loggerMock = new Mock<ILogger<GetUserContributionStatsQueryHandler>>();
        _handler = new GetUserContributionStatsQueryHandler(
            _dbContext,
            _rateLimitEvaluatorMock.Object,
            loggerMock.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private static RateLimitStatus CreateDefaultRateLimitStatus() => RateLimitStatus.Create(
        TestUserId,
        UserTier.Free,
        hasOverride: false,
        currentPendingCount: 1,
        currentMonthlyCount: 5,
        lastRejectionAt: null,
        effectiveMaxPending: 3,
        effectiveMaxPerMonth: 10,
        effectiveCooldown: TimeSpan.FromHours(24));

    private async Task SeedTestDataAsync()
    {
        // Seed test user
        _dbContext.Set<UserEntity>().Add(new UserEntity
        {
            Id = TestUserId,
            Email = "testuser@meepleai.dev",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        });

        // Seed test games
        var games = new[]
        {
            CreateSharedGame("Catan"),
            CreateSharedGame("Ticket to Ride")
        };
        _dbContext.Set<SharedGameEntity>().AddRange(games);
        await _dbContext.SaveChangesAsync();

        // Seed share requests with various statuses
        var requests = new[]
        {
            CreateShareRequest(ShareRequestStatus.Pending, null),
            CreateShareRequest(ShareRequestStatus.Approved, null),
            CreateShareRequest(ShareRequestStatus.Approved, null), // Consecutive approval without changes
            CreateShareRequest(ShareRequestStatus.Approved, "Minor feedback"), // Approval with feedback
            CreateShareRequest(ShareRequestStatus.Rejected, null)
        };
        _dbContext.Set<ShareRequestEntity>().AddRange(requests);
        await _dbContext.SaveChangesAsync();

        // Seed contributors
        var contributors = new[]
        {
            CreateContributor(games[0].Id, isPrimary: true),
            CreateContributor(games[1].Id, isPrimary: false)
        };
        _dbContext.Set<ContributorEntity>().AddRange(contributors);
        await _dbContext.SaveChangesAsync();

        // Seed contribution records
        var records = new[]
        {
            CreateContributionRecord(contributors[0].Id, ContributionRecordType.InitialSubmission, 1),
            CreateContributionRecord(contributors[0].Id, ContributionRecordType.DocumentAddition, 2),
            CreateContributionRecord(contributors[1].Id, ContributionRecordType.DocumentAddition, 1)
        };
        _dbContext.Set<ContributionRecordEntity>().AddRange(records);
        await _dbContext.SaveChangesAsync();
    }

    private static int _bggIdCounter = 1000;

    private static SharedGameEntity CreateSharedGame(string title) => new()
    {
        Id = Guid.NewGuid(),
        BggId = _bggIdCounter++,
        Title = title,
        YearPublished = 2000,
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

    private static int _dayOffset = 1;

    private ShareRequestEntity CreateShareRequest(ShareRequestStatus status, string? adminFeedback)
    {
        var game = _dbContext.Set<SharedGameEntity>().Local.FirstOrDefault()
            ?? new SharedGameEntity { Id = Guid.NewGuid() };

        return new ShareRequestEntity
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            SourceGameId = game.Id,
            Status = (int)status,
            ContributionType = (int)ContributionType.NewGame,
            AdminFeedback = adminFeedback,
            CreatedAt = DateTime.UtcNow.AddDays(-(_dayOffset++)),
            CreatedBy = TestUserId,
            ResolvedAt = status is ShareRequestStatus.Approved or ShareRequestStatus.Rejected
                ? DateTime.UtcNow
                : null
        };
    }

    private static ContributorEntity CreateContributor(Guid gameId, bool isPrimary) => new()
    {
        Id = Guid.NewGuid(),
        UserId = TestUserId,
        SharedGameId = gameId,
        IsPrimaryContributor = isPrimary,
        CreatedAt = DateTime.UtcNow.AddDays(-30)
    };

    private static ContributionRecordEntity CreateContributionRecord(
        Guid contributorId,
        ContributionRecordType type,
        int version) => new()
        {
            Id = Guid.NewGuid(),
            ContributorId = contributorId,
            Type = (int)type,
            Description = $"Contribution v{version}",
            Version = version,
            ContributedAt = DateTime.UtcNow.AddDays(-version),
            IncludesGameData = type == ContributionRecordType.InitialSubmission,
            IncludesMetadata = true,
            DocumentIdsJson = type == ContributionRecordType.DocumentAddition
            ? JsonSerializer.Serialize(new List<Guid> { Guid.NewGuid(), Guid.NewGuid() })
            : null
        };

    [Fact]
    public async Task Handle_ShouldReturnCorrectShareRequestStats()
    {
        // Arrange
        var query = new GetUserContributionStatsQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(TestUserId);
        result.TotalShareRequests.Should().Be(5);
        result.PendingRequests.Should().Be(1);
        result.ApprovedRequests.Should().Be(3);
        result.RejectedRequests.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCalculateApprovalRate()
    {
        // Arrange
        var query = new GetUserContributionStatsQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        // 3 approved / 5 total = 60%
        result.ApprovalRate.Should().Be(60m);
    }

    [Fact]
    public async Task Handle_ShouldCalculateContributionStats()
    {
        // Arrange
        var query = new GetUserContributionStatsQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalContributions.Should().Be(3);
        result.GamesContributed.Should().Be(2);
        result.PrimaryContributions.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCountDocumentsContributed()
    {
        // Arrange
        var query = new GetUserContributionStatsQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.DocumentsContributed.Should().Be(4); // 2 records with 2 docs each
    }

    [Fact]
    public async Task Handle_ShouldIncludeRateLimitStatus()
    {
        // Arrange
        var query = new GetUserContributionStatsQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.RateLimitStatus.Should().NotBeNull();
        result.RateLimitStatus.CurrentPendingCount.Should().Be(1);
        result.RateLimitStatus.MaxPendingAllowed.Should().Be(3);
        result.RateLimitStatus.CurrentMonthlyCount.Should().Be(5);
        result.RateLimitStatus.MaxMonthlyAllowed.Should().Be(10);
    }

    [Fact]
    public async Task Handle_ShouldCallRateLimitEvaluator()
    {
        // Arrange
        var query = new GetUserContributionStatsQuery(TestUserId);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _rateLimitEvaluatorMock.Verify(
            x => x.GetUserStatusAsync(TestUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ForUserWithNoData_ShouldReturnZeroStats()
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

        _rateLimitEvaluatorMock
            .Setup(x => x.GetUserStatusAsync(newUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateLimitStatus.Create(
                newUserId,
                UserTier.Free,
                false, 0, 0, null, 3, 10, TimeSpan.FromHours(24)));

        var query = new GetUserContributionStatsQuery(newUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalShareRequests.Should().Be(0);
        result.TotalContributions.Should().Be(0);
        result.GamesContributed.Should().Be(0);
        result.ApprovalRate.Should().Be(0);
        result.FirstContributionAt.Should().BeNull();
        result.LastContributionAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldIncludeContributionTimeline()
    {
        // Arrange
        var query = new GetUserContributionStatsQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.FirstContributionAt.Should().NotBeNull();
        result.LastContributionAt.Should().NotBeNull();
        result.FirstContributionAt.Should().BeBefore(result.LastContributionAt!.Value);
    }
}
