using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Integration;

/// <summary>
/// Pipeline integration tests covering notification dispatch via NotificationDispatcher.
/// Verifies event handlers correctly delegate to the dispatcher.
/// Issues #4416 (Push) and #4417 (Email Queue) — now unified via NotificationDispatcher.
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class NotificationPipelineIntegrationTests
{
    private readonly Mock<INotificationDispatcher> _dispatcher;
    private readonly Mock<IPdfDocumentRepository> _pdfRepo;
    private readonly PdfNotificationEventHandler _eventHandler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();
    private const string TestFileName = "rulebook.pdf";

    public NotificationPipelineIntegrationTests()
    {
        _dispatcher = new Mock<INotificationDispatcher>();
        _pdfRepo = new Mock<IPdfDocumentRepository>();

        _eventHandler = new PdfNotificationEventHandler(
            _dispatcher.Object,
            _pdfRepo.Object,
            Mock.Of<ILogger<PdfNotificationEventHandler>>());
    }

    private void SetupPdfDocument()
    {
        var pdfDoc = new PdfDocumentBuilder()
            .WithId(_pdfDocumentId)
            .WithGameId(_gameId)
            .WithFileName(TestFileName)
            .WithUploadedBy(_userId)
            .Build();

        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc);
    }

    [Fact]
    public async Task FullPipeline_PdfReady_DispatchesNotification()
    {
        // Arrange
        SetupPdfDocument();

        var evt = new PdfStateChangedEvent(
            _pdfDocumentId,
            PdfProcessingState.Indexing,
            PdfProcessingState.Ready,
            _userId);

        // Act
        await _eventHandler.Handle(evt, CancellationToken.None);

        // Assert: dispatcher called with correct notification type and payload
        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m =>
                m.Type == NotificationType.PdfUploadCompleted &&
                m.RecipientUserId == _userId &&
                m.Payload != null &&
                ((PdfProcessingPayload)m.Payload).FileName == TestFileName &&
                ((PdfProcessingPayload)m.Payload).Status == "Ready" &&
                m.DeepLinkPath == $"/documents/{_pdfDocumentId}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FullPipeline_PdfFailed_DispatchesFailedNotification()
    {
        // Arrange
        SetupPdfDocument();

        var evt = new PdfFailedEvent(
            _pdfDocumentId,
            ErrorCategory.Network,
            PdfProcessingState.Extracting,
            "Connection timeout",
            _userId);

        // Act
        await _eventHandler.Handle(evt, CancellationToken.None);

        // Assert: failed notification dispatched
        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m =>
                m.Type == NotificationType.ProcessingFailed &&
                m.RecipientUserId == _userId &&
                m.Payload != null &&
                ((PdfProcessingPayload)m.Payload).Status.Contains("Connection timeout")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FullPipeline_PdfRetry_DispatchesRetryNotification()
    {
        // Arrange
        SetupPdfDocument();

        var evt = new PdfRetryInitiatedEvent(_pdfDocumentId, 2, _userId);

        // Act
        await _eventHandler.Handle(evt, CancellationToken.None);

        // Assert: retry notification dispatched
        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m =>
                m.Type == NotificationType.PdfUploadCompleted &&
                m.RecipientUserId == _userId &&
                m.Payload != null &&
                ((PdfProcessingPayload)m.Payload).Status.Contains("Retry #2")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FullPipeline_NonReadyState_NoDispatch()
    {
        // Arrange: state change to non-ready
        var evt = new PdfStateChangedEvent(
            _pdfDocumentId,
            PdfProcessingState.Pending,
            PdfProcessingState.Uploading,
            _userId);

        // Act
        await _eventHandler.Handle(evt, CancellationToken.None);

        // Assert: no notification dispatched
        _dispatcher.Verify(d => d.DispatchAsync(
            It.IsAny<NotificationMessage>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FullPipeline_PdfNotFound_NoDispatch()
    {
        // Arrange: PDF not in repo
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        var evt = new PdfStateChangedEvent(
            _pdfDocumentId,
            PdfProcessingState.Indexing,
            PdfProcessingState.Ready,
            _userId);

        // Act
        await _eventHandler.Handle(evt, CancellationToken.None);

        // Assert: no notification dispatched
        _dispatcher.Verify(d => d.DispatchAsync(
            It.IsAny<NotificationMessage>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
