using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestDetails;
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

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestDetails;

/// <summary>
/// Integration tests for GetShareRequestDetailsQueryHandler.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetShareRequestDetailsQueryHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private GetShareRequestDetailsQueryHandler _handler = null!;
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid AdminUserId = Guid.NewGuid();
    private Guid _testShareRequestId;
    private Guid _lockedShareRequestId;

    public GetShareRequestDetailsQueryHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"request_details_test_{Guid.NewGuid():N}";
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

        var loggerMock = new Mock<ILogger<GetShareRequestDetailsQueryHandler>>();
        _handler = new GetShareRequestDetailsQueryHandler(_dbContext, loggerMock.Object);
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
            new UserEntity { Id = TestUserId, Email = "testuser@meepleai.dev", DisplayName = "Test User", Role = "user", CreatedAt = DateTime.UtcNow.AddDays(-30) },
            new UserEntity { Id = AdminUserId, Email = "admin@meepleai.dev", DisplayName = "Admin User", Role = "admin", CreatedAt = DateTime.UtcNow }
        };
        _dbContext.Set<UserEntity>().AddRange(users);

        // Seed categories and mechanics
        var category = new GameCategoryEntity { Id = Guid.NewGuid(), Name = "Strategy", Slug = "strategy", CreatedAt = DateTime.UtcNow };
        var mechanic = new GameMechanicEntity { Id = Guid.NewGuid(), Name = "Dice Rolling", Slug = "dice-rolling", CreatedAt = DateTime.UtcNow };
        _dbContext.Set<GameCategoryEntity>().Add(category);
        _dbContext.Set<GameMechanicEntity>().Add(mechanic);

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
            ComplexityRating = 2.3m,
            ImageUrl = "https://example.com/catan.jpg",
            ThumbnailUrl = "https://example.com/catan_thumb.jpg",
            Status = (int)GameStatus.Published,
            CreatedBy = TestUserId,
            CreatedAt = DateTime.UtcNow,
            Categories = new List<GameCategoryEntity> { category },
            Mechanics = new List<GameMechanicEntity> { mechanic }
        };
        _dbContext.Set<SharedGameEntity>().Add(game);
        await _dbContext.SaveChangesAsync();

        // Seed share requests
        _testShareRequestId = Guid.NewGuid();
        _lockedShareRequestId = Guid.NewGuid();

        var requests = new[]
        {
            new ShareRequestEntity
            {
                Id = _testShareRequestId,
                UserId = TestUserId,
                SourceGameId = game.Id,
                Status = (int)ShareRequestStatus.Pending,
                ContributionType = (int)ContributionType.NewGame,
                UserNotes = "Please review this game submission",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = TestUserId
            },
            new ShareRequestEntity
            {
                Id = _lockedShareRequestId,
                UserId = TestUserId,
                SourceGameId = game.Id,
                Status = (int)ShareRequestStatus.InReview,
                ContributionType = (int)ContributionType.NewGame,
                UserNotes = "Currently being reviewed",
                ReviewingAdminId = AdminUserId,
                ReviewStartedAt = DateTime.UtcNow,
                ReviewLockExpiresAt = DateTime.UtcNow.AddMinutes(30),
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                CreatedBy = TestUserId
            }
        };
        _dbContext.Set<ShareRequestEntity>().AddRange(requests);

        // Add documents to first request
        var document = new ShareRequestDocumentEntity
        {
            Id = Guid.NewGuid(),
            ShareRequestId = _testShareRequestId,
            DocumentId = Guid.NewGuid(),
            FileName = "rules.pdf",
            ContentType = "application/pdf",
            FileSize = 1024000,
            AttachedAt = DateTime.UtcNow
        };
        _dbContext.Set<ShareRequestDocumentEntity>().Add(document);

        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WithValidRequest_ShouldReturnDetails()
    {
        // Arrange
        var query = new GetShareRequestDetailsQuery(_testShareRequestId, AdminUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(_testShareRequestId);
        result.Status.Should().Be(ShareRequestStatus.Pending);
        result.ContributionType.Should().Be(ContributionType.NewGame);
    }

    [Fact]
    public async Task Handle_ShouldIncludeSourceGameDetails()
    {
        // Arrange
        var query = new GetShareRequestDetailsQuery(_testShareRequestId, AdminUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.SourceGame.Should().NotBeNull();
        result.SourceGame.Title.Should().Be("Catan");
        result.SourceGame.BggId.Should().Be(13);
        result.SourceGame.MinPlayers.Should().Be(3);
        result.SourceGame.MaxPlayers.Should().Be(4);
        result.SourceGame.Categories.Should().Contain("Strategy");
        result.SourceGame.Mechanisms.Should().Contain("Dice Rolling");
    }

    [Fact]
    public async Task Handle_ShouldIncludeContributorProfile()
    {
        // Arrange
        var query = new GetShareRequestDetailsQuery(_testShareRequestId, AdminUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Contributor.Should().NotBeNull();
        result.Contributor.UserId.Should().Be(TestUserId);
        result.Contributor.UserName.Should().Be("Test User");
    }

    [Fact]
    public async Task Handle_ShouldIncludeAttachedDocuments()
    {
        // Arrange
        var query = new GetShareRequestDetailsQuery(_testShareRequestId, AdminUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.AttachedDocuments.Should().NotBeEmpty();
        result.AttachedDocuments.First().FileName.Should().Be("rules.pdf");
        result.AttachedDocuments.First().ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task Handle_ShouldIncludeLockStatus()
    {
        // Arrange
        var query = new GetShareRequestDetailsQuery(_lockedShareRequestId, AdminUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.LockStatus.Should().NotBeNull();
        result.LockStatus.IsLocked.Should().BeTrue();
        result.LockStatus.IsLockedByCurrentAdmin.Should().BeTrue();
        result.LockStatus.LockedByAdminId.Should().Be(AdminUserId);
        result.LockStatus.LockedByAdminName.Should().Be("Admin User");
        result.LockStatus.LockExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenLockedByDifferentAdmin_ShouldIndicateNotLockedByCurrentAdmin()
    {
        // Arrange
        var otherAdminId = Guid.NewGuid();
        var query = new GetShareRequestDetailsQuery(_lockedShareRequestId, otherAdminId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.LockStatus.IsLocked.Should().BeTrue();
        result.LockStatus.IsLockedByCurrentAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithNonExistentId_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var query = new GetShareRequestDetailsQuery(Guid.NewGuid(), AdminUserId);

        // Act
        var act = () => _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldIncludeHistory()
    {
        // Arrange
        var query = new GetShareRequestDetailsQuery(_testShareRequestId, AdminUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.History.Should().NotBeEmpty();
        result.History.Should().Contain(h => h.Action == Api.BoundedContexts.SharedGameCatalog.Application.DTOs.ShareRequestHistoryAction.Created);
    }

    [Fact]
    public async Task Handle_ShouldIncludeUserNotes()
    {
        // Arrange
        var query = new GetShareRequestDetailsQuery(_testShareRequestId, AdminUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.UserNotes.Should().Be("Please review this game submission");
    }

    [Fact]
    public async Task Handle_ShouldIncludeTimestamps()
    {
        // Arrange
        var query = new GetShareRequestDetailsQuery(_testShareRequestId, AdminUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Handle_UnlockedRequest_ShouldShowNotLocked()
    {
        // Arrange
        var query = new GetShareRequestDetailsQuery(_testShareRequestId, AdminUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.LockStatus.IsLocked.Should().BeFalse();
        result.LockStatus.IsLockedByCurrentAdmin.Should().BeFalse();
        result.LockStatus.LockedByAdminId.Should().BeNull();
    }
}
