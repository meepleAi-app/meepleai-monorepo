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

namespace Api.Tests.BoundedContexts.UserNotifications.Application.EventHandlers;

[Trait("Category", TestCategories.Unit)]
public sealed class PdfNotificationEventHandlerTests
{
    private readonly Mock<INotificationDispatcher> _dispatcher;
    private readonly Mock<IPdfDocumentRepository> _pdfRepo;
    private readonly Mock<ILogger<PdfNotificationEventHandler>> _logger;
    private readonly PdfNotificationEventHandler _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();
    private const string TestFileName = "test-document.pdf";

    public PdfNotificationEventHandlerTests()
    {
        _dispatcher = new Mock<INotificationDispatcher>();
        _pdfRepo = new Mock<IPdfDocumentRepository>();
        _logger = new Mock<ILogger<PdfNotificationEventHandler>>();

        _sut = new PdfNotificationEventHandler(_dispatcher.Object, _pdfRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_PdfStateChangedToReady_DispatchesNotification()
    {
        var pdfDoc = new PdfDocumentBuilder().WithId(_pdfDocumentId).WithGameId(_gameId).WithFileName(TestFileName).WithUploadedBy(_userId).Build();
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>())).ReturnsAsync(pdfDoc);

        var evt = new PdfStateChangedEvent(_pdfDocumentId, PdfProcessingState.Indexing, PdfProcessingState.Ready, _userId);

        await _sut.Handle(evt, CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m =>
                m.Type == NotificationType.PdfUploadCompleted &&
                m.RecipientUserId == _userId &&
                m.Payload is PdfProcessingPayload),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PdfStateChangedToNonReady_DoesNotDispatch()
    {
        var evt = new PdfStateChangedEvent(_pdfDocumentId, PdfProcessingState.Pending, PdfProcessingState.Uploading, _userId);

        await _sut.Handle(evt, CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PdfFailed_DispatchesFailedNotification()
    {
        var pdfDoc = new PdfDocumentBuilder().WithId(_pdfDocumentId).WithGameId(_gameId).WithFileName(TestFileName).WithUploadedBy(_userId).Build();
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>())).ReturnsAsync(pdfDoc);

        var evt = new PdfFailedEvent(_pdfDocumentId, ErrorCategory.Network, PdfProcessingState.Extracting, "Network timeout", _userId);

        await _sut.Handle(evt, CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m => m.Type == NotificationType.ProcessingFailed && m.RecipientUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PdfRetry_DispatchesRetryNotification()
    {
        var pdfDoc = new PdfDocumentBuilder().WithId(_pdfDocumentId).WithGameId(_gameId).WithFileName(TestFileName).WithUploadedBy(_userId).Build();
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>())).ReturnsAsync(pdfDoc);

        var evt = new PdfRetryInitiatedEvent(_pdfDocumentId, 2, _userId);

        await _sut.Handle(evt, CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<NotificationMessage>(m => m.Type == NotificationType.PdfUploadCompleted && m.RecipientUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PdfNotFound_DoesNotDispatch()
    {
        _pdfRepo.Setup(r => r.GetByIdAsync(_pdfDocumentId, It.IsAny<CancellationToken>())).ReturnsAsync((PdfDocument?)null);

        var evt = new PdfStateChangedEvent(_pdfDocumentId, PdfProcessingState.Indexing, PdfProcessingState.Ready, _userId);

        await _sut.Handle(evt, CancellationToken.None);

        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
