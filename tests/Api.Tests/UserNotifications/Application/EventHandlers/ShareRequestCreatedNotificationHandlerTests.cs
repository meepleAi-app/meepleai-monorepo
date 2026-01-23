using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
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
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Collections.ObjectModel;
using Email = Api.BoundedContexts.Authentication.Domain.ValueObjects.Email;

namespace Api.Tests.UserNotifications.Application.EventHandlers;

public sealed class ShareRequestCreatedNotificationHandlerTests
{
    private readonly Mock<INotificationRepository> _notificationRepo;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IShareRequestRepository> _shareRequestRepo;
    private readonly Mock<ISharedGameRepository> _sharedGameRepo;
    private readonly Mock<IEmailService> _emailService;
    private readonly Mock<MeepleAiDbContext> _dbContext;
    private readonly Mock<ILogger<ShareRequestCreatedNotificationHandler>> _logger;
    private readonly ShareRequestCreatedNotificationHandler _sut;

    private readonly Guid _shareRequestId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _sourceGameId = Guid.NewGuid();

    public ShareRequestCreatedNotificationHandlerTests()
    {
        _notificationRepo = new Mock<INotificationRepository>();
        _userRepo = new Mock<IUserRepository>();
        _shareRequestRepo = new Mock<IShareRequestRepository>();
        _sharedGameRepo = new Mock<ISharedGameRepository>();
        _emailService = new Mock<IEmailService>();
        _dbContext = new Mock<MeepleAiDbContext>();
        _logger = new Mock<ILogger<ShareRequestCreatedNotificationHandler>>();

        _sut = new ShareRequestCreatedNotificationHandler(
            _dbContext.Object,
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
        var user = new User(Guid.NewGuid(), new Email("test@example.com"), "Test User", PasswordHash.Create("TestPassword123!"), Role.User);
        var shareRequest = ShareRequest.Create(
            _userId,
            _sourceGameId,
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

        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_sourceGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceGame);

        var domainEvent = new ShareRequestCreatedEvent(
            _shareRequestId,
            _userId,
            _sourceGameId,
            ContributionType.NewGame);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n =>
                n.UserId == _userId &&
                n.Type.Value == NotificationType.ShareRequestCreated.Value &&
                n.Severity.Value == NotificationSeverity.Info.Value),
            It.IsAny<CancellationToken>()), Times.Once);

        _emailService.Verify(e => e.SendShareRequestCreatedEmailAsync(
            user.Email,
            user.DisplayName,
            It.IsAny<string>(),
            "new game",
            _shareRequestId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_SkipsNotification()
    {
        // Arrange
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var domainEvent = new ShareRequestCreatedEvent(
            _shareRequestId,
            _userId,
            _sourceGameId,
            ContributionType.NewGame);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.IsAny<Notification>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(e => e.SendShareRequestCreatedEmailAsync(
            It.IsAny<string>(),
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
        var user = new User(Guid.NewGuid(), new Email("test@example.com"), "Test User", PasswordHash.Create("TestPassword123!"), Role.User);
        var shareRequest = ShareRequest.Create(
            _userId,
            _sourceGameId,
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

        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_sourceGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceGame);
        _emailService.Setup(e => e.SendShareRequestCreatedEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP error"));

        var domainEvent = new ShareRequestCreatedEvent(
            _shareRequestId,
            _userId,
            _sourceGameId,
            ContributionType.NewGame);

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
