using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Application.Handlers;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Integration;

/// <summary>
/// Pipeline integration tests covering multi-channel notification delivery.
/// Wires real handlers together with mocked external services.
/// Issues #4416 (Push) and #4417 (Email Queue).
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class NotificationPipelineIntegrationTests
{
    private readonly Mock<INotificationPreferencesRepository> _preferencesRepo;
    private readonly Mock<INotificationRepository> _notificationRepo;
    private readonly Mock<IPdfDocumentRepository> _pdfRepo;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IPushNotificationService> _pushService;
    private readonly Mock<IEmailQueueRepository> _emailQueueRepo;
    private readonly Mock<IEmailTemplateService> _emailTemplateService;
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly PdfNotificationEventHandler _eventHandler;
    private readonly EnqueueEmailCommandHandler _enqueueHandler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();
    private const string TestFileName = "rulebook.pdf";
    private const string TestEmail = "player@example.com";
    private const string TestDisplayName = "Test Player";

    public NotificationPipelineIntegrationTests()
    {
        _preferencesRepo = new Mock<INotificationPreferencesRepository>();
        _notificationRepo = new Mock<INotificationRepository>();
        _pdfRepo = new Mock<IPdfDocumentRepository>();
        _userRepo = new Mock<IUserRepository>();
        _pushService = new Mock<IPushNotificationService>();
        _emailQueueRepo = new Mock<IEmailQueueRepository>();
        _emailTemplateService = new Mock<IEmailTemplateService>();
        _unitOfWork = new Mock<IUnitOfWork>();

        // Wire real EnqueueEmailCommandHandler
        _enqueueHandler = new EnqueueEmailCommandHandler(
            _emailQueueRepo.Object,
            _emailTemplateService.Object,
            _unitOfWork.Object,
            Mock.Of<ILogger<EnqueueEmailCommandHandler>>());

        // Create a mediator mock that delegates EnqueueEmailCommand to the real handler
        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<EnqueueEmailCommand>(), It.IsAny<CancellationToken>()))
            .Returns<EnqueueEmailCommand, CancellationToken>((cmd, ct) => _enqueueHandler.Handle(cmd, ct));

        _eventHandler = new PdfNotificationEventHandler(
            _preferencesRepo.Object,
            _notificationRepo.Object,
            _pdfRepo.Object,
            _userRepo.Object,
            mediatorMock.Object,
            _pushService.Object,
            Mock.Of<ILogger<PdfNotificationEventHandler>>());
    }

    private void SetupUserAndDocument()
    {
        var user = new UserBuilder()
            .WithEmail(TestEmail)
            .WithDisplayName(TestDisplayName)
            .Build();

        var pdfDoc = new PdfDocumentBuilder()
            .WithId(_pdfDocumentId)
            .WithGameId(_gameId)
            .WithFileName(TestFileName)
            .WithUploadedBy(_userId)
            .Build();

        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Default: no throttling
        _emailQueueRepo
            .Setup(r => r.ExistsSimilarRecentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _emailQueueRepo
            .Setup(r => r.GetRecentCountByUserIdAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Template rendering
        _emailTemplateService
            .Setup(s => s.RenderDocumentReady(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("<html>Document Ready</html>");
        _emailTemplateService
            .Setup(s => s.RenderDocumentFailed(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("<html>Document Failed</html>");
        _emailTemplateService
            .Setup(s => s.RenderRetryAvailable(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns("<html>Retry Available</html>");
    }

    #region Email Pipeline

    [Fact]
    public async Task FullEmailPipeline_EventToDelivery_SuccessFlow()
    {
        // Arrange: event → prefs check → enqueue → template render → persist
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdateAllPreferences(
            emailReady: true, emailFailed: false, emailRetry: false,
            pushReady: false, pushFailed: false, pushRetry: false,
            inAppReady: false, inAppFailed: false, inAppRetry: false);

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        SetupUserAndDocument();

        var evt = new PdfStateChangedEvent(
            _pdfDocumentId,
            PdfProcessingState.Indexing,
            PdfProcessingState.Ready,
            _userId);

        // Act: fire event which triggers enqueue pipeline
        await _eventHandler.Handle(evt, CancellationToken.None);

        // Assert: email was rendered with correct template
        _emailTemplateService.Verify(s => s.RenderDocumentReady(
            TestDisplayName, TestFileName, It.IsAny<string>()), Times.Once);

        // Assert: email queue item was persisted
        _emailQueueRepo.Verify(r => r.AddAsync(
            It.Is<EmailQueueItem>(e =>
                e.To == TestEmail &&
                e.UserId == _userId &&
                e.HtmlBody == "<html>Document Ready</html>"),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert: unit of work was saved (pipeline completed)
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FullEmailPipeline_ThrottleBlocks_NoDelivery()
    {
        // Arrange: dedup returns true (duplicate email within window)
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdateAllPreferences(
            emailReady: true, emailFailed: false, emailRetry: false,
            pushReady: false, pushFailed: false, pushRetry: false,
            inAppReady: false, inAppFailed: false, inAppRetry: false);

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        SetupUserAndDocument();

        // Override: mark as duplicate
        _emailQueueRepo
            .Setup(r => r.ExistsSimilarRecentAsync(_userId, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var evt = new PdfStateChangedEvent(
            _pdfDocumentId,
            PdfProcessingState.Indexing,
            PdfProcessingState.Ready,
            _userId);

        // Act
        await _eventHandler.Handle(evt, CancellationToken.None);

        // Assert: email was NOT persisted to queue (throttled)
        _emailQueueRepo.Verify(r => r.AddAsync(
            It.IsAny<EmailQueueItem>(),
            It.IsAny<CancellationToken>()), Times.Never);

        // Assert: template was NOT rendered (short-circuited before render)
        _emailTemplateService.Verify(s => s.RenderDocumentReady(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task FullEmailPipeline_SendFailure_QueueItemCreatedForRetry()
    {
        // Arrange: email enqueue succeeds (send happens in separate job)
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdateAllPreferences(
            emailReady: false, emailFailed: true, emailRetry: false,
            pushReady: false, pushFailed: false, pushRetry: false,
            inAppReady: false, inAppFailed: false, inAppRetry: false);

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        SetupUserAndDocument();

        var evt = new PdfFailedEvent(
            _pdfDocumentId,
            ErrorCategory.Network,
            PdfProcessingState.Extracting,
            "Connection timeout",
            _userId);

        // Act
        await _eventHandler.Handle(evt, CancellationToken.None);

        // Assert: email queue item created with Pending status (ready for processor job)
        _emailQueueRepo.Verify(r => r.AddAsync(
            It.Is<EmailQueueItem>(e =>
                e.To == TestEmail &&
                e.UserId == _userId &&
                e.Status == EmailQueueStatus.Pending &&
                e.RetryCount == 0 &&
                e.MaxRetries == 3),
            It.IsAny<CancellationToken>()), Times.Once);

        _emailTemplateService.Verify(s => s.RenderDocumentFailed(
            TestDisplayName, TestFileName, "Connection timeout"), Times.Once);
    }

    #endregion

    #region Push Pipeline

    [Fact]
    public async Task PushNotificationPipeline_EventToDelivery()
    {
        // Arrange: push enabled with valid subscription
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdateAllPreferences(
            emailReady: false, emailFailed: false, emailRetry: false,
            pushReady: true, pushFailed: false, pushRetry: false,
            inAppReady: false, inAppFailed: false, inAppRetry: false);
        prefs.UpdatePushSubscription("https://fcm.googleapis.com/push/abc", "p256dh_key", "auth_key");

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        SetupUserAndDocument();

        var evt = new PdfStateChangedEvent(
            _pdfDocumentId,
            PdfProcessingState.Indexing,
            PdfProcessingState.Ready,
            _userId);

        // Act
        await _eventHandler.Handle(evt, CancellationToken.None);

        // Assert: push service called with correct subscription data
        _pushService.Verify(p => p.SendPushNotificationAsync(
            "https://fcm.googleapis.com/push/abc",
            "p256dh_key",
            "auth_key",
            "PDF Ready",
            It.Is<string>(b => b.Contains(TestFileName)),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert: no email was sent (only push)
        _emailQueueRepo.Verify(r => r.AddAsync(
            It.IsAny<EmailQueueItem>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PushNotificationPipeline_NoSubscription_SkipsPush()
    {
        // Arrange: push enabled in prefs but NO subscription registered
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdateAllPreferences(
            emailReady: false, emailFailed: false, emailRetry: false,
            pushReady: true, pushFailed: false, pushRetry: false,
            inAppReady: false, inAppFailed: false, inAppRetry: false);
        // No call to UpdatePushSubscription → HasPushSubscription = false

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        SetupUserAndDocument();

        var evt = new PdfStateChangedEvent(
            _pdfDocumentId,
            PdfProcessingState.Indexing,
            PdfProcessingState.Ready,
            _userId);

        // Act
        await _eventHandler.Handle(evt, CancellationToken.None);

        // Assert: push NOT called because no subscription
        _pushService.Verify(p => p.SendPushNotificationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Multi-Channel

    [Fact]
    public async Task MultiChannelDelivery_AllChannelsEnabled()
    {
        // Arrange: all three channels enabled
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdateAllPreferences(
            emailReady: true, emailFailed: false, emailRetry: false,
            pushReady: true, pushFailed: false, pushRetry: false,
            inAppReady: true, inAppFailed: false, inAppRetry: false);
        prefs.UpdatePushSubscription("https://push.example.com/ep", "p256dh", "auth");

        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        SetupUserAndDocument();

        var evt = new PdfStateChangedEvent(
            _pdfDocumentId,
            PdfProcessingState.Indexing,
            PdfProcessingState.Ready,
            _userId);

        // Act
        await _eventHandler.Handle(evt, CancellationToken.None);

        // Assert: in-app notification created
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n =>
                n.UserId == _userId &&
                n.Type == NotificationType.PdfUploadCompleted &&
                n.Severity == NotificationSeverity.Success),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert: email enqueued via real handler
        _emailQueueRepo.Verify(r => r.AddAsync(
            It.Is<EmailQueueItem>(e =>
                e.To == TestEmail &&
                e.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert: push notification sent
        _pushService.Verify(p => p.SendPushNotificationAsync(
            "https://push.example.com/ep",
            "p256dh",
            "auth",
            "PDF Ready",
            It.Is<string>(b => b.Contains(TestFileName)),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
