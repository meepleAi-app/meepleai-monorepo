using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Role = Api.SharedKernel.Domain.ValueObjects.Role;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.UserNotifications.Application.EventHandlers;

/// <summary>
/// Unit tests for SharedGameSubmittedForApprovalNotificationHandler.
/// Tests admin notification when Editor submits a shared game for approval.
/// Issue #4159: Backend - Approval Workflow Extension
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class SharedGameSubmittedForApprovalNotificationHandlerTests : IAsyncLifetime
{
    private readonly Mock<INotificationRepository> _notificationRepo;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<ISharedGameRepository> _sharedGameRepo;
    private readonly Mock<IEmailService> _emailService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<SharedGameSubmittedForApprovalNotificationHandler>> _logger;
    private readonly SharedGameSubmittedForApprovalNotificationHandler _sut;

    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _editorId = Guid.NewGuid();
    private readonly Guid _adminId1 = Guid.NewGuid();
    private readonly Guid _adminId2 = Guid.NewGuid();

    public SharedGameSubmittedForApprovalNotificationHandlerTests()
    {
        _notificationRepo = new Mock<INotificationRepository>();
        _userRepo = new Mock<IUserRepository>();
        _sharedGameRepo = new Mock<ISharedGameRepository>();
        _emailService = new Mock<IEmailService>();
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _logger = new Mock<ILogger<SharedGameSubmittedForApprovalNotificationHandler>>();

        _sut = new SharedGameSubmittedForApprovalNotificationHandler(
            _dbContext,
            _notificationRepo.Object,
            _userRepo.Object,
            _sharedGameRepo.Object,
            _emailService.Object,
            _logger.Object);
    }

    public async ValueTask InitializeAsync()
    {
        // Setup admin users via repository mock
        var admin1 = new UserBuilder()
            .WithId(_adminId1)
            .WithEmail("admin1@example.com")
            .WithDisplayName("Admin One")
            .WithRole(Role.Admin)
            .Build();

        var admin2 = new UserBuilder()
            .WithId(_adminId2)
            .WithEmail("admin2@example.com")
            .WithDisplayName("Admin Two")
            .WithRole(Role.Admin)
            .Build();

        _userRepo.Setup(r => r.GetAdminUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { admin1, admin2 });

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CreatesInAppNotificationForAllAdminsAndSendsEmails()
    {
        // Arrange
        var editor = new UserBuilder()
            .WithId(_editorId)
            .WithEmail("editor@example.com")
            .WithDisplayName("Editor User")
            .Build();

        var game = SharedGame.Create(
            title: "Gloomhaven",
            yearPublished: 2017,
            description: "Epic dungeon crawler",
            minPlayers: 1,
            maxPlayers: 4,
            playingTimeMinutes: 120,
            minAge: 14,
            complexityRating: 3.9m,
            averageRating: 8.8m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: _editorId,
            bggId: 174430);

        _userRepo.Setup(r => r.GetByIdAsync(_editorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(editor);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var domainEvent = new SharedGameSubmittedForApprovalEvent(_gameId, _editorId);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert - Should create 2 in-app notifications (one per admin)
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n =>
                (n.UserId == _adminId1 || n.UserId == _adminId2) &&
                n.Type.Value == NotificationType.AdminSharedGameSubmitted.Value &&
                n.Severity.Value == NotificationSeverity.Info.Value &&
                n.Title == "New Game Submitted for Approval" &&
                n.Message.Contains("Gloomhaven")),
            It.IsAny<CancellationToken>()), Times.Exactly(2));

        // Should send 2 emails (one per admin)
        _emailService.Verify(e => e.SendSharedGameSubmittedForApprovalEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            "Gloomhaven",
            "Editor User",
            _gameId,
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenSubmitterNotFound_SkipsNotification()
    {
        // Arrange
        _userRepo.Setup(r => r.GetByIdAsync(_editorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var domainEvent = new SharedGameSubmittedForApprovalEvent(_gameId, _editorId);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.IsAny<Notification>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(e => e.SendSharedGameSubmittedForApprovalEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenGameNotFound_SkipsNotification()
    {
        // Arrange
        var editor = new UserBuilder().WithId(_editorId).Build();

        _userRepo.Setup(r => r.GetByIdAsync(_editorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(editor);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        var domainEvent = new SharedGameSubmittedForApprovalEvent(_gameId, _editorId);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.IsAny<Notification>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNoAdminUsersExist_SkipsNotification()
    {
        // Arrange - Mock returns empty admin list
        _userRepo.Setup(r => r.GetAdminUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        var editor = new UserBuilder().WithId(_editorId).Build();
        var game = SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "Test",
            minPlayers: 1,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.0m,
            averageRating: 7.0m,
            imageUrl: "https://example.com/img.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: _editorId,
            bggId: null);

        _userRepo.Setup(r => r.GetByIdAsync(_editorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(editor);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var domainEvent = new SharedGameSubmittedForApprovalEvent(_gameId, _editorId);

        // Act
        await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.IsAny<Notification>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(e => e.SendSharedGameSubmittedForApprovalEmailAsync(
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
        var editor = new UserBuilder().WithId(_editorId).WithDisplayName("Editor User").Build();
        var game = SharedGame.Create(
            title: "Pandemic",
            yearPublished: 2008,
            description: "Cooperative game",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 45,
            minAge: 8,
            complexityRating: 2.4m,
            averageRating: 7.6m,
            imageUrl: "https://example.com/pandemic.jpg",
            thumbnailUrl: "https://example.com/pandemic-thumb.jpg",
            rules: null,
            createdBy: _editorId,
            bggId: 30549);

        _userRepo.Setup(r => r.GetByIdAsync(_editorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(editor);
        _sharedGameRepo.Setup(r => r.GetByIdAsync(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Email service throws exception
        _emailService.Setup(e => e.SendSharedGameSubmittedForApprovalEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP server unavailable"));

        var domainEvent = new SharedGameSubmittedForApprovalEvent(_gameId, _editorId);

        // Act
        var act = async () => await _sut.Handle(domainEvent, CancellationToken.None);

        // Assert - Should NOT throw (email failure is non-critical)
        await act.Should().NotThrowAsync();

        // In-app notification should still be created
        _notificationRepo.Verify(r => r.AddAsync(
            It.IsAny<Notification>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2)); // 2 admins
    }
}
