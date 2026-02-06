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

/// <summary>
/// Tests for ShareRequestReviewStartedNotificationHandler.
/// Issue #3668: Phase 7 - Game proposal lifecycle notifications.
/// </summary>
public sealed class ShareRequestReviewStartedNotificationHandlerTests
{
    private readonly Mock<INotificationRepository> _notificationRepo;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IShareRequestRepository> _shareRequestRepo;
    private readonly Mock<ISharedGameRepository> _sharedGameRepo;
    private readonly Mock<IEmailService> _emailService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<ShareRequestReviewStartedNotificationHandler>> _logger;
    private readonly ShareRequestReviewStartedNotificationHandler _sut;

    private readonly Guid _shareRequestId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _sourceGameId = Guid.NewGuid();

    public ShareRequestReviewStartedNotificationHandlerTests()
    {
        _notificationRepo = new Mock<INotificationRepository>();
        _userRepo = new Mock<IUserRepository>();
        _shareRequestRepo = new Mock<IShareRequestRepository>();
        _sharedGameRepo = new Mock<ISharedGameRepository>();
        _emailService = new Mock<IEmailService>();
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _logger = new Mock<ILogger<ShareRequestReviewStartedNotificationHandler>>();

        _sut = new ShareRequestReviewStartedNotificationHandler(
            _dbContext,
            _notificationRepo.Object,
            _userRepo.Object,
            _shareRequestRepo.Object,
            _sharedGameRepo.Object,
            _emailService.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Handle_CreatesInAppNotificationAndSendsEmail()
    {
        // Arrange
        var user = new UserBuilder()
            .WithEmail("user@example.com")
            .WithDisplayName("Test User")
            .Build();
        var shareRequest = ShareRequest.Create(
            _userId,
            _sourceGameId,
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

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_sourceGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceGame);

        var domainEvent = new ShareRequestReviewStartedEvent(_shareRequestId, _adminId);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n =>
                n.UserId == _userId &&
                n.Type.Value == NotificationType.GameProposalInReview.Value &&
                n.Severity.Value == NotificationSeverity.Info.Value &&
                n.Title == "Game Proposal Under Review" &&
                n.Link == $"/contributions/requests/{_shareRequestId}"),
            It.IsAny<CancellationToken>()), Times.Once);

        _emailService.Verify(e => e.SendShareRequestReviewStartedEmailAsync(
            user.Email,
            user.DisplayName,
            "Catan",
            _shareRequestId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenShareRequestNotFound_SkipsNotification()
    {
        // Arrange
        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShareRequest?)null);

        var domainEvent = new ShareRequestReviewStartedEvent(_shareRequestId, _adminId);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.IsAny<Notification>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(e => e.SendShareRequestReviewStartedEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_SkipsNotification()
    {
        // Arrange
        var shareRequest = ShareRequest.Create(
            _userId,
            _sourceGameId,
            ContributionType.NewGameProposal,
            "Test proposal");

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var domainEvent = new ShareRequestReviewStartedEvent(_shareRequestId, _adminId);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.IsAny<Notification>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(e => e.SendShareRequestReviewStartedEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEmailFails_ContinuesSuccessfully()
    {
        // Arrange
        var user = new UserBuilder()
            .WithEmail("user@example.com")
            .WithDisplayName("Test User")
            .Build();
        var shareRequest = ShareRequest.Create(
            _userId,
            _sourceGameId,
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

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_sourceGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceGame);
        _emailService.Setup(e => e.SendShareRequestReviewStartedEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP error"));

        var domainEvent = new ShareRequestReviewStartedEvent(_shareRequestId, _adminId);

        // Act
        var act = async () => await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert - Should not throw (graceful email failure)
        await act.Should().NotThrowAsync();

        // Verify notification was still created
        _notificationRepo.Verify(r => r.AddAsync(
            It.IsAny<Notification>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
