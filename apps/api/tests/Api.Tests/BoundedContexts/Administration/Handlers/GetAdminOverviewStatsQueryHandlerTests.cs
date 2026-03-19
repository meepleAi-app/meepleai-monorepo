using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Services;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Handlers;

/// <summary>
/// Unit tests for GetAdminOverviewStatsQueryHandler.
/// Issue #4198: Verifies lightweight overview stats aggregation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetAdminOverviewStatsQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly FakeHybridCache _cache;
    private readonly FakeTimeProvider _timeProvider;
    private readonly Mock<ILlmRequestLogRepository> _mockLlmRequestLogRepository;
    private readonly GetAdminOverviewStatsQueryHandler _handler;

    public GetAdminOverviewStatsQueryHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _cache = new FakeHybridCache();
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero));
        _mockLlmRequestLogRepository = new Mock<ILlmRequestLogRepository>();
        _mockLlmRequestLogRepository
            .Setup(x => x.GetActiveAiUserCountAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _handler = new GetAdminOverviewStatsQueryHandler(_dbContext, _cache, _mockLlmRequestLogRepository.Object, _timeProvider);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dbContext?.Dispose();
        }
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsZeroStats()
    {
        var result = await _handler.Handle(new GetAdminOverviewStatsQuery(), CancellationToken.None);

        result.TotalGames.Should().Be(0);
        result.PublishedGames.Should().Be(0);
        result.TotalUsers.Should().Be(0);
        result.ActiveUsers.Should().Be(0);
        result.ActiveAiUsers.Should().Be(0);
        result.ApprovalRate.Should().Be(0);
        result.PendingApprovals.Should().Be(0);
        result.RecentSubmissions.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithGames_ReturnsTotalAndPublishedCounts()
    {
        // Arrange - 3 games, 2 published
        SeedGames(published: 2, unpublished: 1);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetAdminOverviewStatsQuery(), CancellationToken.None);

        // Assert
        result.TotalGames.Should().Be(3);
        result.PublishedGames.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithUsers_ReturnsTotalAndActiveCounts()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var userId1 = SeedUser("active@test.com");
        var userId2 = SeedUser("inactive@test.com");
        var userId3 = SeedUser("recent@test.com");
        await _dbContext.SaveChangesAsync(); // Save users before sessions reference them

        // userId1 has recent session (active)
        SeedSession(userId1, lastSeenAt: now.AddDays(-5));
        // userId2 has old session (inactive - last seen 60 days ago)
        SeedSession(userId2, lastSeenAt: now.AddDays(-60));
        // userId3 has recent session (active)
        SeedSession(userId3, lastSeenAt: now.AddDays(-1));

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetAdminOverviewStatsQuery(), CancellationToken.None);

        // Assert
        result.TotalUsers.Should().Be(3);
        result.ActiveUsers.Should().Be(2); // userId1 and userId3
    }

    [Fact]
    public async Task Handle_WithShareRequests_ReturnsApprovalStats()
    {
        // Arrange - Need source game for FK
        var gameId = SeedSharedGame("Test Game");
        SeedShareRequest(gameId, status: 3); // Approved
        SeedShareRequest(gameId, status: 3); // Approved
        SeedShareRequest(gameId, status: 4); // Rejected
        SeedShareRequest(gameId, status: 0); // Pending
        SeedShareRequest(gameId, status: 1); // InReview
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetAdminOverviewStatsQuery(), CancellationToken.None);

        // Assert
        result.ApprovalRate.Should().Be(40.0); // 2 approved out of 5
        result.PendingApprovals.Should().Be(2); // 1 Pending + 1 InReview
    }

    [Fact]
    public async Task Handle_WithRecentSubmissions_ReturnsLastSevenDays()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var gameId = SeedSharedGame("Test Game");

        // 2 recent (within 7 days)
        SeedShareRequest(gameId, status: 0, createdAt: now.AddDays(-3));
        SeedShareRequest(gameId, status: 0, createdAt: now.AddDays(-1));
        // 1 older (outside 7 days)
        SeedShareRequest(gameId, status: 0, createdAt: now.AddDays(-10));

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetAdminOverviewStatsQuery(), CancellationToken.None);

        // Assert
        result.RecentSubmissions.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ReturnsActiveAiUsersFromRepository()
    {
        // Arrange
        _mockLlmRequestLogRepository
            .Setup(x => x.GetActiveAiUserCountAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        // Act
        var result = await _handler.Handle(new GetAdminOverviewStatsQuery(), CancellationToken.None);

        // Assert
        result.ActiveAiUsers.Should().Be(42);
        _mockLlmRequestLogRepository.Verify(
            x => x.GetActiveAiUserCountAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CachesResult()
    {
        // Act - call twice
        await _handler.Handle(new GetAdminOverviewStatsQuery(), CancellationToken.None);
        await _handler.Handle(new GetAdminOverviewStatsQuery(), CancellationToken.None);

        // Assert - second call should hit cache
        _cache.HitCount.Should().Be(1);
        _cache.MissCount.Should().Be(1);
    }

    // ========================================================================
    // Seed helpers
    // ========================================================================

    private void SeedGames(int published, int unpublished)
    {
        for (var i = 0; i < published; i++)
        {
            _dbContext.Games.Add(new GameEntity
            {
                Id = Guid.NewGuid(),
                Name = $"Published Game {i}",
                IsPublished = true,
                PublishedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });
        }
        for (var i = 0; i < unpublished; i++)
        {
            _dbContext.Games.Add(new GameEntity
            {
                Id = Guid.NewGuid(),
                Name = $"Unpublished Game {i}",
                IsPublished = false,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private Guid SeedUser(string email)
    {
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = email,
            DisplayName = email.Split('@')[0],
            Role = "User",
            CreatedAt = DateTime.UtcNow
        });
        return userId;
    }

    private void SeedSession(Guid userId, DateTime lastSeenAt)
    {
        var user = _dbContext.Users.Find(userId)!;
        _dbContext.UserSessions.Add(new UserSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = user,
            LastSeenAt = lastSeenAt,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            TokenHash = Guid.NewGuid().ToString()
        });
    }

    private Guid SeedSharedGame(string title)
    {
        var id = Guid.NewGuid();
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = id,
            Title = title,
            CreatedAt = DateTime.UtcNow
        });
        return id;
    }

    private void SeedShareRequest(Guid sourceGameId, int status, DateTime? createdAt = null)
    {
        _dbContext.Set<ShareRequestEntity>().Add(new ShareRequestEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SourceGameId = sourceGameId,
            Status = status,
            CreatedAt = createdAt ?? _timeProvider.GetUtcNow().UtcDateTime,
            CreatedBy = Guid.NewGuid()
        });
    }
}
