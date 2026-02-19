using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Services;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.EventHandlers;

[Trait("Category", TestCategories.Unit)]
public sealed class ProcessingJobNotificationEventHandlerTests
{
    private readonly Mock<INotificationPreferencesRepository> _preferencesRepo;
    private readonly Mock<INotificationRepository> _notificationRepo;
    private readonly Mock<IPdfDocumentRepository> _pdfRepo;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IMediator> _mediator;
    private readonly Mock<IPushNotificationService> _pushService;
    private readonly Mock<ILogger<ProcessingJobNotificationEventHandler>> _logger;
    private readonly ProcessingJobNotificationEventHandler _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _jobId = Guid.NewGuid();
    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private const string TestFileName = "rules-catan.pdf";

    public ProcessingJobNotificationEventHandlerTests()
    {
        _preferencesRepo = new Mock<INotificationPreferencesRepository>();
        _notificationRepo = new Mock<INotificationRepository>();
        _pdfRepo = new Mock<IPdfDocumentRepository>();
        _userRepo = new Mock<IUserRepository>();
        _mediator = new Mock<IMediator>();
        _pushService = new Mock<IPushNotificationService>();
        _logger = new Mock<ILogger<ProcessingJobNotificationEventHandler>>();

        _sut = new ProcessingJobNotificationEventHandler(
            _preferencesRepo.Object,
            _notificationRepo.Object,
            _pdfRepo.Object,
            _userRepo.Object,
            _mediator.Object,
            _pushService.Object,
            _logger.Object);
    }

    #region JobCompletedEvent Tests

    [Fact]
    public async Task Handle_JobCompleted_AllChannelsEnabled_SendsAllNotifications()
    {
        // Arrange
        var prefs = CreatePreferences(inAppReady: true, emailReady: true, pushReady: true);
        SetupPushSubscription(prefs);
        var user = CreateUser();
        var pdfDoc = CreatePdfDocument();

        SetupMocks(prefs, user, pdfDoc);

        var evt = new JobCompletedEvent(_jobId, _pdfDocumentId, _userId, TimeSpan.FromMinutes(2));

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert - in-app
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n =>
                n.UserId == _userId &&
                n.Type == NotificationType.ProcessingJobCompleted &&
                n.Severity == NotificationSeverity.Success &&
                n.Title == "Processing Complete"),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert - email
        _mediator.Verify(m => m.Send(
            It.Is<EnqueueEmailCommand>(c =>
                c.UserId == _userId &&
                c.TemplateName == "processing_job_completed"),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert - push
        _pushService.Verify(p => p.SendPushNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            "MeepleAI - PDF Ready",
            It.Is<string>(s => s.Contains(TestFileName)),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_JobCompleted_NoPreferences_DoesNothing()
    {
        // Arrange
        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreferences?)null);

        var evt = new JobCompletedEvent(_jobId, _pdfDocumentId, _userId, TimeSpan.FromMinutes(1));

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<EnqueueEmailCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _pushService.Verify(p => p.SendPushNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_JobCompleted_UserNotFound_DoesNothing()
    {
        // Arrange
        var prefs = CreatePreferences(inAppReady: true);
        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePdfDocument());
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var evt = new JobCompletedEvent(_jobId, _pdfDocumentId, _userId, TimeSpan.FromMinutes(1));

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_JobCompleted_PushEnabled_NoSubscription_SkipsPush()
    {
        // Arrange - push enabled but no subscription set
        var prefs = CreatePreferences(inAppReady: true, pushReady: true);
        var user = CreateUser();
        var pdfDoc = CreatePdfDocument();
        SetupMocks(prefs, user, pdfDoc);

        var evt = new JobCompletedEvent(_jobId, _pdfDocumentId, _userId, TimeSpan.FromMinutes(1));

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert - in-app still sent
        _notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);

        // Assert - push NOT sent because no subscription
        _pushService.Verify(p => p.SendPushNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_JobCompleted_OnlyInAppEnabled_SendsOnlyInApp()
    {
        // Arrange
        var prefs = CreatePreferences(inAppReady: true, emailReady: false, pushReady: false);
        var user = CreateUser();
        var pdfDoc = CreatePdfDocument();
        SetupMocks(prefs, user, pdfDoc);

        var evt = new JobCompletedEvent(_jobId, _pdfDocumentId, _userId, TimeSpan.FromSeconds(30));

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<EnqueueEmailCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _pushService.Verify(p => p.SendPushNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_JobCompleted_PdfNotFound_DoesNothing()
    {
        // Arrange
        var prefs = CreatePreferences(inAppReady: true);
        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser());

        var evt = new JobCompletedEvent(_jobId, _pdfDocumentId, _userId, TimeSpan.FromMinutes(1));

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_JobCompleted_DurationFormattedCorrectly()
    {
        // Arrange
        var prefs = CreatePreferences(inAppReady: true);
        var user = CreateUser();
        var pdfDoc = CreatePdfDocument();
        SetupMocks(prefs, user, pdfDoc);

        var evt = new JobCompletedEvent(_jobId, _pdfDocumentId, _userId, TimeSpan.FromMinutes(3).Add(TimeSpan.FromSeconds(15)));

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert - message should contain formatted duration
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n => n.Message.Contains("3m 15s")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region JobFailedEvent Tests

    [Fact]
    public async Task Handle_JobFailed_AllChannelsEnabled_SendsAllNotifications()
    {
        // Arrange
        var prefs = CreatePreferences(inAppFailed: true, emailFailed: true, pushFailed: true);
        SetupPushSubscription(prefs);
        var user = CreateUser();
        var pdfDoc = CreatePdfDocument();
        SetupMocks(prefs, user, pdfDoc);
        SetupAdminUsers();

        var evt = new JobFailedEvent(_jobId, _pdfDocumentId, _userId, "Out of memory", ProcessingStepType.Extract, 0);

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert - in-app for uploader
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n =>
                n.UserId == _userId &&
                n.Type == NotificationType.ProcessingJobFailed &&
                n.Severity == NotificationSeverity.Error &&
                n.Message.Contains("Out of memory")),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        // Assert - email
        _mediator.Verify(m => m.Send(
            It.Is<EnqueueEmailCommand>(c =>
                c.UserId == _userId &&
                c.TemplateName == "processing_job_failed" &&
                c.ErrorMessage!.Contains("Out of memory")),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert - push
        _pushService.Verify(p => p.SendPushNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            "MeepleAI - Processing Failed",
            It.Is<string>(s => s.Contains("Out of memory")),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_JobFailed_NotifiesAdminUsers()
    {
        // Arrange
        var prefs = CreatePreferences(inAppFailed: true);
        var user = CreateUser();
        var pdfDoc = CreatePdfDocument();
        SetupMocks(prefs, user, pdfDoc);

        var admin1Id = Guid.NewGuid();
        var admin2Id = Guid.NewGuid();
        var admins = new List<User>
        {
            new UserBuilder().WithId(admin1Id).AsAdmin().WithEmail("admin1@meepleai.dev").Build(),
            new UserBuilder().WithId(admin2Id).AsAdmin().WithEmail("admin2@meepleai.dev").Build(),
        };
        _userRepo.Setup(r => r.GetAdminUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(admins);

        var evt = new JobFailedEvent(_jobId, _pdfDocumentId, _userId, "Connection timeout", null, 0);

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert - uploader notification
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n => n.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert - admin notifications (2 admins, neither is the uploader)
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n => n.UserId == admin1Id),
            It.IsAny<CancellationToken>()), Times.Once);
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n => n.UserId == admin2Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_JobFailed_AdminIsUploader_SkipsDuplicate()
    {
        // Arrange
        var prefs = CreatePreferences(inAppFailed: true);
        var user = CreateUser();
        var pdfDoc = CreatePdfDocument();
        SetupMocks(prefs, user, pdfDoc);

        // Admin who is also the uploader
        var admins = new List<User>
        {
            new UserBuilder().WithId(_userId).AsAdmin().WithEmail("uploader-admin@meepleai.dev").Build(),
        };
        _userRepo.Setup(r => r.GetAdminUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(admins);

        var evt = new JobFailedEvent(_jobId, _pdfDocumentId, _userId, "Parse error", ProcessingStepType.Chunk, 1);

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert - only 1 notification (uploader), admin duplicate skipped
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n => n.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_JobFailed_IncludesStepNameInMessage()
    {
        // Arrange
        var prefs = CreatePreferences(inAppFailed: true);
        var user = CreateUser();
        var pdfDoc = CreatePdfDocument();
        SetupMocks(prefs, user, pdfDoc);
        SetupAdminUsers();

        var evt = new JobFailedEvent(_jobId, _pdfDocumentId, _userId, "Timeout", ProcessingStepType.Embed, 0);

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert - message includes step name
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<Notification>(n =>
                n.UserId == _userId &&
                n.Message.Contains("Embed") &&
                n.Message.Contains("Timeout")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_JobFailed_EmailEnqueueFails_StillSendsPushAndAdmin()
    {
        // Arrange
        var prefs = CreatePreferences(inAppFailed: true, emailFailed: true, pushFailed: true);
        SetupPushSubscription(prefs);
        var user = CreateUser();
        var pdfDoc = CreatePdfDocument();
        SetupMocks(prefs, user, pdfDoc);
        SetupAdminUsers();

        _mediator.Setup(m => m.Send(It.IsAny<EnqueueEmailCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        var evt = new JobFailedEvent(_jobId, _pdfDocumentId, _userId, "Error", null, 0);

        // Act
        await _sut.Handle(evt, CancellationToken.None);

        // Assert - push still called despite email failure
        _pushService.Verify(p => p.SendPushNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert - admin notifications still created
        _userRepo.Verify(r => r.GetAdminUsersAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Helper Methods

    private NotificationPreferences CreatePreferences(
        bool inAppReady = false, bool inAppFailed = false,
        bool emailReady = false, bool emailFailed = false,
        bool pushReady = false, bool pushFailed = false)
    {
        var prefs = new NotificationPreferences(_userId);
        prefs.UpdateAllPreferences(
            emailReady: emailReady, emailFailed: emailFailed, emailRetry: false,
            pushReady: pushReady, pushFailed: pushFailed, pushRetry: false,
            inAppReady: inAppReady, inAppFailed: inAppFailed, inAppRetry: false);
        return prefs;
    }

    private static void SetupPushSubscription(NotificationPreferences prefs)
    {
        prefs.UpdatePushSubscription("https://push.example.com/sub", "p256dh_key", "auth_key");
    }

    private User CreateUser()
    {
        return new UserBuilder()
            .WithId(_userId)
            .WithEmail("uploader@example.com")
            .WithDisplayName("Test Uploader")
            .Build();
    }

    private PdfDocument CreatePdfDocument()
    {
        return new PdfDocumentBuilder()
            .WithId(_pdfDocumentId)
            .WithFileName(TestFileName)
            .Build();
    }

    private void SetupMocks(NotificationPreferences prefs, User user, PdfDocument pdfDoc)
    {
        _preferencesRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);
        _userRepo.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mediator.Setup(m => m.Send(It.IsAny<EnqueueEmailCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
    }

    private void SetupAdminUsers()
    {
        _userRepo.Setup(r => r.GetAdminUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());
    }

    #endregion
}
