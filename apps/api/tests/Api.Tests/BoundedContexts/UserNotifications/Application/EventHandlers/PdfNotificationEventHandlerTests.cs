using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Services;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.EventHandlers;

public sealed class PdfNotificationEventHandlerTests
{
    private readonly Mock<INotificationPreferencesRepository> _preferencesRepo;
    private readonly Mock<INotificationRepository> _notificationRepo;
    private readonly Mock<IPdfDocumentRepository> _pdfRepo;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IEmailService> _emailService;
    private readonly Mock<IPushNotificationService> _pushService;
    private readonly Mock<ILogger<PdfNotificationEventHandler>> _logger;
    private readonly PdfNotificationEventHandler _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();
    private const string TestFileName = "test-document.pdf";

    public PdfNotificationEventHandlerTests()
    {
        _preferencesRepo = new Mock<INotificationPreferencesRepository>();
        _notificationRepo = new Mock<INotificationRepository>();
        _pdfRepo = new Mock<IPdfDocumentRepository>();
        _userRepo = new Mock<IUserRepository>();
        _emailService = new Mock<IEmailService>();
        _pushService = new Mock<IPushNotificationService>();
        _logger = new Mock<ILogger<PdfNotificationEventHandler>>();

        _sut = new PdfNotificationEventHandler(
            _preferencesRepo.Object,
            _notificationRepo.Object,
            _pdfRepo.Object,
            _userRepo.Object,
            _emailService.Object,
            _pushService.Object,
            _logger.Object);
    }

    #region PdfStateChangedEvent Tests

    [Fact]
    public async Task Handle_PdfStateChangedToReady_AllChannelsEnabled_SendsAllNotifications()
    {
        // Arrange
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdateAllPreferences(
            emailReady: true, emailFailed: false, emailRetry: false,
            pushReady: true, pushFailed: false, pushRetry: false,
            inAppReady: true, inAppFailed: false, inAppRetry: false);

        var user = new UserBuilder()
            .WithEmail("test@example.com")
            .WithDisplayName("Test User")
            .Build();

        var pdfDoc = new PdfDocumentBuilder()
            .WithId(_pdfDocumentId)
            .WithGameId(_gameId)
            .WithFileName(TestFileName)
            .WithUploadedBy(_userId)
            .Build();

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var evt = new PdfStateChangedEvent(
            _pdfDocumentId,
            PdfProcessingState.Indexing,
            PdfProcessingState.Ready,
            _userId);

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n =>
                n.UserId == _userId &&
                n.Type == NotificationType.PdfUploadCompleted &&
                n.Severity == NotificationSeverity.Success &&
                n.Title == "PDF Ready"),
            It.IsAny<CancellationToken>()), Times.Once);

        _emailService.Verify(e => e.SendPdfReadyEmailAsync(
            "test@example.com",
            "Test User",
            TestFileName,
            _pdfDocumentId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PdfStateChangedToReady_OnlyEmailEnabled_SendsOnlyEmail()
    {
        // Arrange
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdateAllPreferences(
            emailReady: true, emailFailed: false, emailRetry: false,
            pushReady: false, pushFailed: false, pushRetry: false,
            inAppReady: false, inAppFailed: false, inAppRetry: false);

        var user = new UserBuilder()
            .WithEmail("test@example.com")
            .WithDisplayName("Test User")
            .Build();

        var pdfDoc = new PdfDocumentBuilder()
            .WithId(_pdfDocumentId)
            .WithGameId(_gameId)
            .WithFileName(TestFileName)
            .WithUploadedBy(_userId)
            .Build();

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var evt = new PdfStateChangedEvent(
            _pdfDocumentId,
            PdfProcessingState.Indexing,
            PdfProcessingState.Ready,
            _userId);

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.IsAny<Notification>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _emailService.Verify(e => e.SendPdfReadyEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PdfStateChangedToNonReady_DoesNotSendNotifications()
    {
        // Arrange
        var evt = new PdfStateChangedEvent(
            _pdfDocumentId,
            PdfProcessingState.Pending,
            PdfProcessingState.Uploading,
            _userId);

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.IsAny<Notification>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _emailService.Verify(e => e.SendPdfReadyEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PdfStateChangedToReady_NoPreferences_DoesNotSendNotifications()
    {
        // Arrange
        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreferences?)null);

        var evt = new PdfStateChangedEvent(
            _pdfDocumentId,
            PdfProcessingState.Indexing,
            PdfProcessingState.Ready,
            _userId);

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.IsAny<Notification>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _emailService.Verify(e => e.SendPdfReadyEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region PdfFailedEvent Tests

    [Fact]
    public async Task Handle_PdfFailed_AllChannelsEnabled_SendsAllNotifications()
    {
        // Arrange
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdateAllPreferences(
            emailReady: false, emailFailed: true, emailRetry: false,
            pushReady: false, pushFailed: true, pushRetry: false,
            inAppReady: false, inAppFailed: true, inAppRetry: false);

        var user = new UserBuilder()
            .WithEmail("test@example.com")
            .WithDisplayName("Test User")
            .Build();

        var pdfDoc = new PdfDocumentBuilder()
            .WithId(_pdfDocumentId)
            .WithGameId(_gameId)
            .WithFileName(TestFileName)
            .WithUploadedBy(_userId)
            .Build();

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var evt = new PdfFailedEvent(
            _pdfDocumentId,
            ErrorCategory.Network,
            PdfProcessingState.Extracting,
            "Network timeout",
            _userId);

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n =>
                n.UserId == _userId &&
                n.Type == NotificationType.ProcessingFailed &&
                n.Severity == NotificationSeverity.Error &&
                n.Title == "PDF Processing Failed"),
            It.IsAny<CancellationToken>()), Times.Once);

        _emailService.Verify(e => e.SendPdfFailedEmailAsync(
            "test@example.com",
            "Test User",
            TestFileName,
            "Network timeout",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PdfFailed_EmailServiceThrows_ContinuesWithoutError()
    {
        // Arrange
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdateEmailPreferences(onReady: false, onFailed: true, onRetry: false);

        var user = new UserBuilder()
            .WithEmail("test@example.com")
            .WithDisplayName("Test User")
            .Build();

        var pdfDoc = new PdfDocumentBuilder()
            .WithId(_pdfDocumentId)
            .WithGameId(_gameId)
            .WithFileName(TestFileName)
            .WithUploadedBy(_userId)
            .Build();

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _emailService.Setup(e => e.SendPdfFailedEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Email service unavailable"));

        var evt = new PdfFailedEvent(
            _pdfDocumentId,
            ErrorCategory.Network,
            PdfProcessingState.Extracting,
            "Network timeout",
            _userId);

        // Act
        var act = async () => await _sut.Handle(evt, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region PdfRetryInitiatedEvent Tests

    [Fact]
    public async Task Handle_PdfRetry_AllChannelsEnabled_SendsAllNotifications()
    {
        // Arrange
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdateAllPreferences(
            emailReady: false, emailFailed: false, emailRetry: true,
            pushReady: false, pushFailed: false, pushRetry: true,
            inAppReady: false, inAppFailed: false, inAppRetry: true);

        var user = new UserBuilder()
            .WithEmail("test@example.com")
            .WithDisplayName("Test User")
            .Build();

        var pdfDoc = new PdfDocumentBuilder()
            .WithId(_pdfDocumentId)
            .WithGameId(_gameId)
            .WithFileName(TestFileName)
            .WithUploadedBy(_userId)
            .Build();

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var evt = new PdfRetryInitiatedEvent(
            _pdfDocumentId,
            2,
            _userId);

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n =>
                n.UserId == _userId &&
                n.Type == NotificationType.PdfUploadCompleted &&
                n.Severity == NotificationSeverity.Info &&
                n.Title == "PDF Retry Started"),
            It.IsAny<CancellationToken>()), Times.Once);

        _emailService.Verify(e => e.SendPdfRetryEmailAsync(
            "test@example.com",
            "Test User",
            TestFileName,
            2,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PdfRetry_AllChannelsDisabled_DoesNotSendNotifications()
    {
        // Arrange
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdateAllPreferences(
            emailReady: false, emailFailed: false, emailRetry: false,
            pushReady: false, pushFailed: false, pushRetry: false,
            inAppReady: false, inAppFailed: false, inAppRetry: false);

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);

        var evt = new PdfRetryInitiatedEvent(
            _pdfDocumentId,
            1,
            _userId);

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.IsAny<Notification>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _emailService.Verify(e => e.SendPdfRetryEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_PdfDocumentNotFound_DoesNotSendNotifications()
    {
        // Arrange
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdateEmailPreferences(onReady: true, onFailed: false, onRetry: false);

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        var evt = new PdfStateChangedEvent(
            _pdfDocumentId,
            PdfProcessingState.Indexing,
            PdfProcessingState.Ready,
            _userId);

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.IsAny<Notification>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _emailService.Verify(e => e.SendPdfReadyEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UserNotFound_DoesNotSendNotifications()
    {
        // Arrange
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdateEmailPreferences(onReady: true, onFailed: false, onRetry: false);

        var pdfDoc = new PdfDocumentBuilder()
            .WithId(_pdfDocumentId)
            .WithGameId(_gameId)
            .WithFileName(TestFileName)
            .WithUploadedBy(_userId)
            .Build();

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var evt = new PdfStateChangedEvent(
            _pdfDocumentId,
            PdfProcessingState.Indexing,
            PdfProcessingState.Ready,
            _userId);

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(
            It.IsAny<Notification>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _emailService.Verify(e => e.SendPdfReadyEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
