using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.EventHandlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PrivatePdfAssociatedEventHandlerTests
{
    private readonly Mock<IProcessingJobRepository> _jobRepoMock = new();
    private readonly Mock<IPdfDocumentRepository> _pdfRepoMock = new();
    private readonly Mock<IQueueStreamService> _streamServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 4, 7, 12, 0, 0, TimeSpan.Zero));
    private readonly Mock<ILogger<PrivatePdfAssociatedEventHandler>> _loggerMock = new();

    private PrivatePdfAssociatedEventHandler CreateSut() => new(
        _jobRepoMock.Object,
        _pdfRepoMock.Object,
        _streamServiceMock.Object,
        _unitOfWorkMock.Object,
        _timeProvider,
        _loggerMock.Object);

    private static PrivatePdfAssociatedEvent CreateEvent(
        Guid? pdfDocumentId = null,
        Guid? userId = null) =>
        new(
            libraryEntryId: Guid.NewGuid(),
            userId: userId ?? Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: pdfDocumentId ?? Guid.NewGuid());

    [Fact]
    public async Task Handle_WhenNewPdf_CreatesJobAndPublishesSseEvent()
    {
        // Arrange
        var notification = CreateEvent();
        var pdfDocument = CreatePdfDocument(notification.PdfDocumentId);

        _jobRepoMock
            .Setup(r => r.ExistsByPdfDocumentIdAsync(notification.PdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _pdfRepoMock
            .Setup(r => r.GetByIdAsync(notification.PdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDocument);

        _jobRepoMock
            .Setup(r => r.CountByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        QueueStreamEvent? capturedSseEvent = null;
        _streamServiceMock
            .Setup(s => s.PublishJobEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()))
            .Callback<QueueStreamEvent, CancellationToken>((e, _) => capturedSseEvent = e)
            .Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        await sut.Handle(notification, CancellationToken.None);

        // Assert
        _jobRepoMock.Verify(
            r => r.AddAsync(It.Is<ProcessingJob>(j =>
                j.PdfDocumentId == notification.PdfDocumentId &&
                j.UserId == notification.UserId &&
                j.Status == JobStatus.Queued),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        capturedSseEvent.Should().NotBeNull();
        capturedSseEvent!.Type.Should().Be(QueueStreamEventType.JobQueued);
        capturedSseEvent.Data.Should().BeOfType<JobQueuedData>();

        var data = (JobQueuedData)capturedSseEvent.Data!;
        data.PdfDocumentId.Should().Be(notification.PdfDocumentId);
        data.UserId.Should().Be(notification.UserId);
    }

    [Fact]
    public async Task Handle_WhenJobAlreadyExists_SkipsCreation()
    {
        // Arrange
        var notification = CreateEvent();

        _jobRepoMock
            .Setup(r => r.ExistsByPdfDocumentIdAsync(notification.PdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = CreateSut();

        // Act
        await sut.Handle(notification, CancellationToken.None);

        // Assert
        _jobRepoMock.Verify(
            r => r.AddAsync(It.IsAny<ProcessingJob>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);

        _streamServiceMock.Verify(
            s => s.PublishJobEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPdfDocumentNotFound_LogsWarningAndSkips()
    {
        // Arrange
        var notification = CreateEvent();

        _jobRepoMock
            .Setup(r => r.ExistsByPdfDocumentIdAsync(notification.PdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _pdfRepoMock
            .Setup(r => r.GetByIdAsync(notification.PdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        var sut = CreateSut();

        // Act
        await sut.Handle(notification, CancellationToken.None);

        // Assert
        _jobRepoMock.Verify(
            r => r.AddAsync(It.IsAny<ProcessingJob>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);

        _streamServiceMock.Verify(
            s => s.PublishJobEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Creates a minimal PdfDocument for test purposes.
    /// The handler only checks for non-null existence, so the exact ID does not matter.
    /// </summary>
    private static PdfDocument CreatePdfDocument(Guid id)
    {
        return new PdfDocument(
            id: id,
            gameId: Guid.Empty,
            fileName: new FileName("test-rules.pdf"),
            filePath: "/uploads/test-rules.pdf",
            fileSize: new FileSize(1024),
            uploadedByUserId: Guid.NewGuid());
    }
}
