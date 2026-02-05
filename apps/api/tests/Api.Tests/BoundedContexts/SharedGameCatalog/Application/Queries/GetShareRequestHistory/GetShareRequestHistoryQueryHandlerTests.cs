using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestHistory;
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

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestHistory;

/// <summary>
/// Integration tests for GetShareRequestHistoryQueryHandler.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetShareRequestHistoryQueryHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private GetShareRequestHistoryQueryHandler _handler = null!;
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid AdminUserId = Guid.NewGuid();
    private Guid _testShareRequestId;
    private Guid _requestWithAuditLogId;

    public GetShareRequestHistoryQueryHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"request_history_test_{Guid.NewGuid():N}";
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

        var loggerMock = new Mock<ILogger<GetShareRequestHistoryQueryHandler>>();
        _handler = new GetShareRequestHistoryQueryHandler(_dbContext, loggerMock.Object);
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
            new UserEntity { Id = AdminUserId, Email = "admin@meepleai.dev", DisplayName = "Admin User", Role = "admin", CreatedAt = DateTime.UtcNow }
        };
        _dbContext.Set<UserEntity>().AddRange(users);

        // Seed test game
        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            BggId = 13,
            Title = "Catan",
            YearPublished = 1995,
            Description = "A classic trading and building game",
            MinPlayers = 3,
            MaxPlayers = 4,
            PlayingTimeMinutes = 90,
            MinAge = 10,
            ImageUrl = "https://example.com/catan.jpg",
            ThumbnailUrl = "https://example.com/catan_thumb.jpg",
            Status = (int)GameStatus.Published,
            CreatedBy = TestUserId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Set<SharedGameEntity>().Add(game);
        await _dbContext.SaveChangesAsync();

        // Seed share requests
        _testShareRequestId = Guid.NewGuid();
        _requestWithAuditLogId = Guid.NewGuid();

        var requests = new[]
        {
            new ShareRequestEntity
            {
                Id = _testShareRequestId,
                UserId = TestUserId,
                SourceGameId = game.Id,
                Status = (int)ShareRequestStatus.Pending,
                ContributionType = (int)ContributionType.NewGame,
                UserNotes = "Test request without audit logs",
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                CreatedBy = TestUserId
            },
            new ShareRequestEntity
            {
                Id = _requestWithAuditLogId,
                UserId = TestUserId,
                SourceGameId = game.Id,
                Status = (int)ShareRequestStatus.InReview,
                ContributionType = (int)ContributionType.NewGame,
                UserNotes = "Request with audit history",
                ReviewingAdminId = AdminUserId,
                ReviewStartedAt = DateTime.UtcNow.AddMinutes(-30),
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                CreatedBy = TestUserId
            }
        };
        _dbContext.Set<ShareRequestEntity>().AddRange(requests);
        await _dbContext.SaveChangesAsync();

        // Seed audit logs for second request
        var auditLogs = new[]
        {
            new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                UserId = TestUserId,
                Action = "created",
                Resource = "ShareRequest",
                ResourceId = _requestWithAuditLogId.ToString(),
                Result = "Success",
                Details = "Share request created",
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            },
            new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                UserId = AdminUserId,
                Action = "reviewstarted",
                Resource = "ShareRequest",
                ResourceId = _requestWithAuditLogId.ToString(),
                Result = "Success",
                Details = "Admin started review",
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)
            }
        };
        _dbContext.Set<AuditLogEntity>().AddRange(auditLogs);
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WithValidRequestWithoutAuditLogs_ShouldReturnMinimalHistory()
    {
        // Arrange
        var query = new GetShareRequestHistoryQuery(_testShareRequestId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(h => h.Action == ShareRequestHistoryAction.Created);
    }

    [Fact]
    public async Task Handle_WithRequestWithAuditLogs_ShouldReturnFullHistory()
    {
        // Arrange
        var query = new GetShareRequestHistoryQuery(_requestWithAuditLogId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Should().Contain(h => h.Action == ShareRequestHistoryAction.Created);
        result.Should().Contain(h => h.Action == ShareRequestHistoryAction.ReviewStarted);
    }

    [Fact]
    public async Task Handle_ShouldIncludeActorNames()
    {
        // Arrange
        var query = new GetShareRequestHistoryQuery(_requestWithAuditLogId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Contain(h => h.ActorName == "Test User");
        result.Should().Contain(h => h.ActorName == "Admin User");
    }

    [Fact]
    public async Task Handle_ShouldReturnHistoryInChronologicalOrder()
    {
        // Arrange
        var query = new GetShareRequestHistoryQuery(_requestWithAuditLogId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeInAscendingOrder(h => h.Timestamp);
    }

    [Fact]
    public async Task Handle_WithNonExistentId_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var query = new GetShareRequestHistoryQuery(Guid.NewGuid());

        // Act
        var act = () => _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldIncludeTimestamps()
    {
        // Arrange
        var query = new GetShareRequestHistoryQuery(_requestWithAuditLogId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().AllSatisfy(h => h.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromHours(2)));
    }

    [Fact]
    public async Task Handle_ShouldIncludeDetails()
    {
        // Arrange
        var query = new GetShareRequestHistoryQuery(_requestWithAuditLogId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Contain(h => h.Details != null && h.Details.Contains("Share request created"));
    }

    [Fact]
    public async Task Handle_CreatedActionShouldHaveCorrectActorId()
    {
        // Arrange
        var query = new GetShareRequestHistoryQuery(_requestWithAuditLogId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var createdEntry = result.FirstOrDefault(h => h.Action == ShareRequestHistoryAction.Created);
        createdEntry.Should().NotBeNull();
        createdEntry!.ActorId.Should().Be(TestUserId);
    }

    [Fact]
    public async Task Handle_ReviewStartedActionShouldHaveAdminActorId()
    {
        // Arrange
        var query = new GetShareRequestHistoryQuery(_requestWithAuditLogId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var reviewEntry = result.FirstOrDefault(h => h.Action == ShareRequestHistoryAction.ReviewStarted);
        reviewEntry.Should().NotBeNull();
        reviewEntry!.ActorId.Should().Be(AdminUserId);
    }
}
