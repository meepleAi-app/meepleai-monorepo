using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Services;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.UserNotifications.Application.EventHandlers;

public sealed class ShareRequestApprovedNotificationHandlerTests
{
    private readonly Mock<INotificationRepository> _notificationRepo;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IShareRequestRepository> _shareRequestRepo;
    private readonly Mock<ISharedGameRepository> _sharedGameRepo;
    private readonly Mock<IEmailService> _emailService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<ShareRequestApprovedNotificationHandler>> _logger;
    private readonly ShareRequestApprovedNotificationHandler _sut;

    private readonly Guid _shareRequestId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _sharedGameId = Guid.NewGuid();

    public ShareRequestApprovedNotificationHandlerTests()
    {
        _notificationRepo = new Mock<INotificationRepository>();
        _userRepo = new Mock<IUserRepository>();
        _shareRequestRepo = new Mock<IShareRequestRepository>();
        _sharedGameRepo = new Mock<ISharedGameRepository>();
        _emailService = new Mock<IEmailService>();
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _logger = new Mock<ILogger<ShareRequestApprovedNotificationHandler>>();

        _sut = new ShareRequestApprovedNotificationHandler(
            _dbContext,
            _notificationRepo.Object,
            _userRepo.Object,
            _shareRequestRepo.Object,
            _sharedGameRepo.Object,
            _emailService.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Handle_CreatesSuccessNotificationAndSendsEmail()
    {
        // Arrange
        var user = new UserBuilder()
            .WithEmail("test@example.com")
            .WithDisplayName("Test User")
            .Build();
        var sourceGameId = Guid.NewGuid();
        var shareRequest = ShareRequest.Create(
            _userId,
            sourceGameId,
            ContributionType.NewGame,
            "Test notes");
        var sourceGame = SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "Test description",
            minPlayers: 1,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 13,
            complexityRating: 2.5m,
            averageRating: 7.5m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: null);

        // Target game created by same user (ApproveAsNew scenario)
        var targetGame = SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "Newly approved",
            minPlayers: 1,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 13,
            complexityRating: 2.5m,
            averageRating: null,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: _userId, // Same user = new game
            bggId: null);

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(sourceGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceGame);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetGame);

        var domainEvent = new ShareRequestApprovedEvent(
            _shareRequestId,
            _adminId,
            _sharedGameId);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n =>
                n.UserId == _userId &&
                n.Type.Value == NotificationType.ShareRequestApproved.Value &&
                n.Severity.Value == NotificationSeverity.Success.Value),
            It.IsAny<CancellationToken>()), Times.Once);

        _emailService.Verify(e => e.SendShareRequestApprovedEmailAsync(
            user.Email,
            user.DisplayName,
            It.IsAny<string>(),
            _sharedGameId,
            _userId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenShareRequestNotFound_SkipsNotification()
    {
        // Arrange
        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest?)null);

        var domainEvent = new ShareRequestApprovedEvent(
            _shareRequestId,
            _adminId,
            _sharedGameId);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.IsAny<Notification>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEmailFails_ContinuesSuccessfully()
    {
        // Arrange
        var user = new UserBuilder()
            .WithEmail("test@example.com")
            .WithDisplayName("Test User")
            .Build();
        var sourceGameId = Guid.NewGuid();
        var shareRequest = ShareRequest.Create(
            _userId,
            sourceGameId,
            ContributionType.NewGame,
            "Test notes");
        var sourceGame = SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "Test description",
            minPlayers: 1,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 13,
            complexityRating: 2.5m,
            averageRating: 7.5m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: null);

        var targetGame = SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "Test",
            minPlayers: 1,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 13,
            complexityRating: 2.5m,
            averageRating: null,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: _userId,
            bggId: null);

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(sourceGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceGame);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetGame);
        _emailService.Setup(e => e.SendShareRequestApprovedEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP error"));

        var domainEvent = new ShareRequestApprovedEvent(
            _shareRequestId,
            _adminId,
            _sharedGameId);

        // Act
        var act = async () => await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert - Should not throw (graceful email failure)
        await act.Should().NotThrowAsync();

        // Verify notification was still created
        _notificationRepo.Verify(r => r.AddAsync(
            It.IsAny<Notification>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenKbMerge_CreatesKbMergedNotificationAndSendsSpecificEmail()
    {
        // Arrange - Issue #3668: Test KB merge scenario
        var user = new UserBuilder()
            .WithEmail("user@example.com")
            .WithDisplayName("Test User")
            .Build();
        var shareRequest = ShareRequest.Create(
            _userId,
            Guid.NewGuid(),
            ContributionType.NewGameProposal,
            "Test proposal");
        var sourceGame = SharedGame.Create(
            title: "Catan",
            yearPublished: 1995,
            description: "Settlers of Catan",
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            complexityRating: 2.3m,
            averageRating: 7.8m,
            imageUrl: "https://example.com/catan.jpg",
            thumbnailUrl: "https://example.com/catan-thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: null);

        // Target game created by DIFFERENT user (old game = KB merge scenario)
        // CreatedBy != userId indicates pre-existing game, not created by this approval
        var differentCreatorId = Guid.NewGuid(); // Different from _userId
        var oldTargetGame = SharedGame.Create(
            title: "Catan",
            yearPublished: 1995,
            description: "Existing Catan entry",
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            complexityRating: 2.3m,
            averageRating: 7.8m,
            imageUrl: "https://example.com/catan.jpg",
            thumbnailUrl: "https://example.com/catan-thumb.jpg",
            rules: null,
            createdBy: differentCreatorId, // Key: different creator = KB merge
            bggId: 13);

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(shareRequest.SourceGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceGame);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldTargetGame);

        var domainEvent = new ShareRequestApprovedEvent(
            _shareRequestId,
            _adminId,
            _sharedGameId);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert - Should create KB merged notification
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n =>
                n.UserId == _userId &&
                n.Type.Value == NotificationType.GameProposalKbMerged.Value &&
                n.Severity.Value == NotificationSeverity.Success.Value &&
                n.Title == "Game Proposal Merged! 🎉"),
            It.IsAny<CancellationToken>()), Times.Once);

        // Should send KB merged email (not standard approved email)
        _emailService.Verify(e => e.SendShareRequestKbMergedEmailAsync(
            user.Email,
            user.DisplayName,
            "Catan",
            _sharedGameId,
            It.IsAny<CancellationToken>()), Times.Once);

        _emailService.Verify(e => e.SendShareRequestApprovedEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenApproveAsNew_CreatesStandardNotificationAndEmail()
    {
        // Arrange - Issue #3668: Test new game scenario
        var user = new UserBuilder()
            .WithEmail("user@example.com")
            .WithDisplayName("Test User")
            .Build();
        var shareRequest = ShareRequest.Create(
            _userId,
            Guid.NewGuid(),
            ContributionType.NewGameProposal,
            "Test proposal");
        var sourceGame = SharedGame.Create(
            title: "New Game",
            yearPublished: 2024,
            description: "Brand new game",
            minPlayers: 2,
            maxPlayers: 6,
            playingTimeMinutes: 45,
            minAge: 8,
            complexityRating: 1.8m,
            averageRating: null,
            imageUrl: "https://example.com/new.jpg",
            thumbnailUrl: "https://example.com/new-thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: null);

        // Target game created just now (new game = ApproveAsNew scenario)
        var newTargetGame = SharedGame.Create(
            title: "New Game",
            yearPublished: 2024,
            description: "Just created",
            minPlayers: 2,
            maxPlayers: 6,
            playingTimeMinutes: 45,
            minAge: 8,
            complexityRating: 1.8m,
            averageRating: null,
            imageUrl: "https://example.com/new.jpg",
            thumbnailUrl: "https://example.com/new-thumb.jpg",
            rules: null,
            createdBy: _userId,
            bggId: null);
        // CreatedAt is recent (< 30 seconds ago by default)

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(shareRequest.SourceGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceGame);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_sharedGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newTargetGame);

        var domainEvent = new ShareRequestApprovedEvent(
            _shareRequestId,
            _adminId,
            _sharedGameId);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert - Should create standard approved notification
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n =>
                n.UserId == _userId &&
                n.Type.Value == NotificationType.ShareRequestApproved.Value &&
                n.Severity.Value == NotificationSeverity.Success.Value &&
                n.Title == "Contribution Approved! 🎉"),
            It.IsAny<CancellationToken>()), Times.Once);

        // Should send standard approved email (not KB merged email)
        _emailService.Verify(e => e.SendShareRequestApprovedEmailAsync(
            user.Email,
            user.DisplayName,
            "New Game",
            _sharedGameId,
            _userId,
            It.IsAny<CancellationToken>()), Times.Once);

        _emailService.Verify(e => e.SendShareRequestKbMergedEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}