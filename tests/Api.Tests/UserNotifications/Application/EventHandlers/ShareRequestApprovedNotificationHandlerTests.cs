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
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Collections.ObjectModel;
using Email = Api.BoundedContexts.Authentication.Domain.ValueObjects.Email;

namespace Api.Tests.UserNotifications.Application.EventHandlers;

/// <summary>
/// Integration tests for ShareRequestApprovedNotificationHandler with in-memory database.
/// ISSUE-3005: Fixed DbContext mocking issues by using InMemoryDatabase.
/// </summary>
public sealed class ShareRequestApprovedNotificationHandlerTests : IAsyncLifetime
{
    private readonly DbContextOptions<MeepleAiDbContext> _options;
    private readonly Mock<INotificationRepository> _notificationRepo;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IShareRequestRepository> _shareRequestRepo;
    private readonly Mock<ISharedGameRepository> _sharedGameRepo;
    private readonly Mock<IEmailService> _emailService;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IDomainEventCollector> _mockEventCollector;
    private readonly Mock<ILogger<ShareRequestApprovedNotificationHandler>> _logger;
    private MeepleAiDbContext _dbContext = null!;
    private ShareRequestApprovedNotificationHandler _sut = null!;

    private readonly Guid _shareRequestId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _sharedGameId = Guid.NewGuid();

    public ShareRequestApprovedNotificationHandlerTests()
    {
        _options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ShareRequestApprovedTest_{Guid.NewGuid()}")
            .Options;

        _notificationRepo = new Mock<INotificationRepository>();
        _userRepo = new Mock<IUserRepository>();
        _shareRequestRepo = new Mock<IShareRequestRepository>();
        _sharedGameRepo = new Mock<ISharedGameRepository>();
        _emailService = new Mock<IEmailService>();
        _mockMediator = new Mock<IMediator>();
        _mockEventCollector = new Mock<IDomainEventCollector>();
        _mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(Array.Empty<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());
        _logger = new Mock<ILogger<ShareRequestApprovedNotificationHandler>>();
    }

    public async Task InitializeAsync()
    {
        _dbContext = new MeepleAiDbContext(_options, _mockMediator.Object, _mockEventCollector.Object);
        _sut = new ShareRequestApprovedNotificationHandler(
            _dbContext,
            _notificationRepo.Object,
            _userRepo.Object,
            _shareRequestRepo.Object,
            _sharedGameRepo.Object,
            _emailService.Object,
            _logger.Object);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CreatesCelebratoryNotificationAndSendsEmail()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), new Email("test@example.com"), "Test User", PasswordHash.Create("TestPassword123!"), Role.User);
        var shareRequest = ShareRequest.Create(
            _userId,
            Guid.NewGuid(),
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

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceGame);

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
                n.Severity.Value == NotificationSeverity.Success.Value &&
                n.Title.Contains("🎉")),
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
    public async Task Handle_WhenTargetSharedGameIdIsNull_SkipsNotification()
    {
        // Arrange
        var shareRequest = ShareRequest.Create(
            _userId,
            Guid.NewGuid(),
            ContributionType.NewGame,
            "Test notes");

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        var domainEvent = new ShareRequestApprovedEvent(
            _shareRequestId,
            _adminId,
            null); // TargetSharedGameId is null

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
        var user = new User(Guid.NewGuid(), new Email("test@example.com"), "Test User", PasswordHash.Create("TestPassword123!"), Role.User);
        var shareRequest = ShareRequest.Create(
            _userId,
            Guid.NewGuid(),
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

        _shareRequestRepo.Setup(r => r.GetByIdAsync(_shareRequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceGame);
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
}
