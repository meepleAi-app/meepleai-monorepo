using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Services;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Unit tests for VectorDocumentIndexedEventHandler.
/// Issue #4942: Verifies multi-channel notification when KB indexing completes.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class VectorDocumentIndexedEventHandlerTests
{
    private readonly Mock<IVectorDocumentRepository> _vectorDocRepo;
    private readonly Mock<IPdfDocumentRepository> _pdfDocRepo;
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<INotificationRepository> _notificationRepo;
    private readonly Mock<INotificationPreferencesRepository> _preferencesRepo;
    private readonly Mock<IMediator> _mediator;
    private readonly Mock<IPushNotificationService> _pushService;
    private readonly Mock<ILogger<VectorDocumentIndexedEventHandler>> _logger;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _documentId = Guid.NewGuid();
    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private const string TestFileName = "twilight-imperium-rulebook.pdf";
    private const int TestChunkCount = 247;

    public VectorDocumentIndexedEventHandlerTests()
    {
        _vectorDocRepo = new Mock<IVectorDocumentRepository>();
        _pdfDocRepo = new Mock<IPdfDocumentRepository>();
        _userRepo = new Mock<IUserRepository>();
        _notificationRepo = new Mock<INotificationRepository>();
        _preferencesRepo = new Mock<INotificationPreferencesRepository>();
        _mediator = new Mock<IMediator>();
        _pushService = new Mock<IPushNotificationService>();
        _logger = new Mock<ILogger<VectorDocumentIndexedEventHandler>>();
    }

    private VectorDocumentIndexedEventHandler CreateHandler()
    {
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        return new VectorDocumentIndexedEventHandler(
            dbContext,
            _logger.Object,
            _vectorDocRepo.Object,
            _pdfDocRepo.Object,
            _userRepo.Object,
            _notificationRepo.Object,
            _preferencesRepo.Object,
            _mediator.Object,
            _pushService.Object);
    }

    private VectorDocumentIndexedEvent CreateEvent() =>
        new(_documentId, _gameId, TestChunkCount);

    private VectorDocument CreateVectorDocument() =>
        new(_documentId, _gameId, _pdfDocumentId, "it", TestChunkCount);

    private PdfDocument CreatePdfDocument() =>
        new PdfDocumentBuilder()
            .WithId(_pdfDocumentId)
            .WithGameId(_gameId)
            .WithFileName(TestFileName)
            .WithUploadedBy(_userId)
            .Build();

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

    #region VectorDocument Not Found

    [Fact]
    public async Task Handle_VectorDocumentNotFound_SkipsNotification()
    {
        // Arrange
        _vectorDocRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VectorDocument?)null);

        var sut = CreateHandler();
        var evt = CreateEvent();

        // Act
        await sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<EnqueueEmailCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region PDF Document Not Found

    [Fact]
    public async Task Handle_PdfDocumentNotFound_SkipsNotification()
    {
        // Arrange
        var vectorDoc = CreateVectorDocument();
        _vectorDocRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorDoc);
        _pdfDocRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        var sut = CreateHandler();
        var evt = CreateEvent();

        // Act
        await sut.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepo.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Notification Preferences

    [Fact]
    public async Task Handle_NoUserPreferences_SkipsNotification()
    {
        // Arrange
        var vectorDoc = CreateVectorDocument();
        var pdfDoc = CreatePdfDocument();

        _vectorDocRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorDoc);
        _pdfDocRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);
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
        var vectorDoc = CreateVectorDocument();
        var pdfDoc = CreatePdfDocument();
        var prefs = CreatePreferences(inAppReady: true);

        _vectorDocRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorDoc);
        _pdfDocRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);
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
        var vectorDoc = CreateVectorDocument();
        var pdfDoc = CreatePdfDocument();
        var prefs = CreatePreferences(inAppReady: false);

        _vectorDocRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorDoc);
        _pdfDocRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);
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
        var vectorDoc = CreateVectorDocument();
        var pdfDoc = CreatePdfDocument();
        var prefs = CreatePreferences(inAppReady: false, emailReady: true);
        var user = new UserBuilder()
            .WithEmail("player@example.com")
            .WithDisplayName("Test Player")
            .Build();

        _vectorDocRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorDoc);
        _pdfDocRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);
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
        var vectorDoc = CreateVectorDocument();
        var pdfDoc = CreatePdfDocument();
        var prefs = CreatePreferences(inAppReady: false, emailReady: true);
        var user = new UserBuilder().Build();

        _vectorDocRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorDoc);
        _pdfDocRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);
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

    [Fact]
    public async Task Handle_PushEnabled_PushFailure_DoesNotThrow()
    {
        // Arrange
        var vectorDoc = CreateVectorDocument();
        var pdfDoc = CreatePdfDocument();
        var prefs = CreatePreferences(inAppReady: false, pushReady: true);
        SetupPushSubscription(prefs);

        _vectorDocRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorDoc);
        _pdfDocRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);
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

    #region All Channels

    [Fact]
    public async Task Handle_AllChannelsEnabled_SendsAllNotifications()
    {
        // Arrange
        var vectorDoc = CreateVectorDocument();
        var pdfDoc = CreatePdfDocument();
        var prefs = CreatePreferences(inAppReady: true, emailReady: true, pushReady: true);
        SetupPushSubscription(prefs);
        var user = new UserBuilder()
            .WithEmail("player@example.com")
            .WithDisplayName("Test Player")
            .Build();

        _vectorDocRepo.Setup(r => r.GetByIdAsync(_documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorDoc);
        _pdfDocRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);
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
