using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Services;
using Api.SharedKernel.Application.IntegrationEvents;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Unit tests for VectorDocumentReadyNotificationHandler.
/// Issue #5237: Verifies multi-channel notification via integration event.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class VectorDocumentReadyNotificationHandlerTests
{
    private readonly Mock<INotificationRepository> _notificationRepo;
    private readonly Mock<INotificationPreferencesRepository> _preferencesRepo;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IMediator> _mediator;
    private readonly Mock<IPushNotificationService> _pushService;
    private readonly Mock<ILogger<VectorDocumentReadyNotificationHandler>> _logger;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _documentId = Guid.NewGuid();
    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private const string TestFileName = "twilight-imperium-rulebook.pdf";
    private const int TestChunkCount = 247;

    public VectorDocumentReadyNotificationHandlerTests()
    {
        _notificationRepo = new Mock<INotificationRepository>();
        _preferencesRepo = new Mock<INotificationPreferencesRepository>();
        _userRepo = new Mock<IUserRepository>();
        _mediator = new Mock<IMediator>();
        _pushService = new Mock<IPushNotificationService>();
        _logger = new Mock<ILogger<VectorDocumentReadyNotificationHandler>>();
    }

    private VectorDocumentReadyNotificationHandler CreateHandler() =>
        new(
            _notificationRepo.Object,
            _preferencesRepo.Object,
            _userRepo.Object,
            _mediator.Object,
            _pushService.Object,
            _logger.Object);

    private VectorDocumentReadyIntegrationEvent CreateEvent() =>
        new()
        {
            DocumentId = _documentId,
            GameId = _gameId,
            ChunkCount = TestChunkCount,
            PdfDocumentId = _pdfDocumentId,
            UploadedByUserId = _userId,
            FileName = TestFileName,
            CurrentProcessingState = "Processing"
        };

    private NotificationPreferences CreatePreferences(
        bool inAppReady = true,
        bool emailReady = false,
        bool pushReady = false)
    {
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdateAllPreferences(
            emailReady: emailReady, emailFailed: false, emailRetry: false,
            pushReady: pushReady, pushFailed: false, pushRetry: false,
            inAppReady: inAppReady, inAppFailed: false, inAppRetry: false);
        return prefs;
    }

    private void SetupPushSubscription(NotificationPreferences prefs)
    {
        prefs.UpdatePushSubscription(
            "https://push.example.com/endpoint",
            "p256dhKey",
            "authKey");
    }

    #region No Preferences

    [Fact]
    public async Task Handle_NoUserPreferences_SkipsNotification()
    {
        // Arrange
        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreferences?)null);

        var sut = CreateHandler();
        var evt = CreateEvent();

        // Act
        await sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<EnqueueEmailCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region In-App Notification

    [Fact]
    public async Task Handle_InAppEnabled_CreatesNotificationWithCorrectProperties()
    {
        // Arrange
        var prefs = CreatePreferences(inAppReady: true);
        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);

        Notification? capturedNotification = null;
        _notificationRepo.Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => capturedNotification = n);

        var sut = CreateHandler();
        var evt = CreateEvent();

        // Act
        await sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
        capturedNotification.Should().NotBeNull();
        capturedNotification!.UserId.Should().Be(_userId);
        capturedNotification.Type.Should().Be(NotificationType.ProcessingJobCompleted);
        capturedNotification.Severity.Should().Be(NotificationSeverity.Success);
        capturedNotification.Title.Should().Be("Knowledge Base pronta");
        capturedNotification.Message.Should().Contain(TestFileName);
        capturedNotification.Message.Should().Contain(TestChunkCount.ToString());
        capturedNotification.Link.Should().Be($"/library/games/{_gameId}/agent");
    }

    [Fact]
    public async Task Handle_InAppDisabled_DoesNotCreateNotification()
    {
        // Arrange
        var prefs = CreatePreferences(inAppReady: false);
        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);

        var sut = CreateHandler();
        var evt = CreateEvent();

        // Act
        await sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Email Notification

    [Fact]
    public async Task Handle_EmailEnabled_EnqueuesEmailWithCorrectData()
    {
        // Arrange
        var prefs = CreatePreferences(inAppReady: false, emailReady: true);
        var user = new UserBuilder()
            .WithEmail("player@example.com")
            .WithDisplayName("Test Player")
            .Build();

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var sut = CreateHandler();
        var evt = CreateEvent();

        // Act
        await sut.Handle(evt, CancellationToken.None);

        // Assert
        _mediator.Verify(m => m.Send(
            It.Is<EnqueueEmailCommand>(c =>
                c.UserId == _userId &&
                c.To == "player@example.com" &&
                c.UserName == "Test Player" &&
                c.FileName == TestFileName &&
                c.TemplateName == "kb_indexed" &&
                c.DocumentUrl == $"/library/games/{_gameId}/agent"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmailEnabled_EmailFailure_DoesNotThrow()
    {
        // Arrange
        var prefs = CreatePreferences(inAppReady: false, emailReady: true);
        var user = new UserBuilder().Build();

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mediator.Setup(m => m.Send(It.IsAny<EnqueueEmailCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Email service unavailable"));

        var sut = CreateHandler();
        var evt = CreateEvent();

        // Act & Assert — email failure must not propagate (fail-safe)
        await sut.Invoking(h => h.Handle(evt, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    #endregion

    #region Push Notification

    [Fact]
    public async Task Handle_PushEnabled_PushFailure_DoesNotThrow()
    {
        // Arrange
        var prefs = CreatePreferences(inAppReady: false, pushReady: true);
        SetupPushSubscription(prefs);

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        _pushService.Setup(p => p.SendPushNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Push service unavailable"));

        var sut = CreateHandler();
        var evt = CreateEvent();

        // Act & Assert — push failure must not propagate (fail-safe)
        await sut.Invoking(h => h.Handle(evt, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    #endregion

    #region All Channels

    [Fact]
    public async Task Handle_AllChannelsEnabled_SendsAllNotifications()
    {
        // Arrange
        var prefs = CreatePreferences(inAppReady: true, emailReady: true, pushReady: true);
        SetupPushSubscription(prefs);
        var user = new UserBuilder()
            .WithEmail("player@example.com")
            .WithDisplayName("Test Player")
            .Build();

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var sut = CreateHandler();
        var evt = CreateEvent();

        // Act
        await sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<EnqueueEmailCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _pushService.Verify(p => p.SendPushNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
