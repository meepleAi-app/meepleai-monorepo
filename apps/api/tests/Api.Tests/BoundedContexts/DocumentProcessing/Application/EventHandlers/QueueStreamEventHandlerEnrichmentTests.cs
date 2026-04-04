using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.EventHandlers;

/// <summary>
/// Tests that JobCompleted and JobFailed SSE stream handlers enrich DTOs
/// with game info from PdfDocuments and SharedGameDocuments tables.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class QueueStreamEventHandlerEnrichmentTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IQueueStreamService> _streamServiceMock = new();

    public QueueStreamEventHandlerEnrichmentTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"EnrichmentTests_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(options, Mock.Of<IMediator>(), Mock.Of<IDomainEventCollector>());
    }

    public void Dispose() => _db.Dispose();

    #region JobCompletedStreamHandler enrichment

    [Fact]
    public async Task JobCompletedStreamHandler_EnrichesWithGameInfo_WhenSharedGameDocumentExists()
    {
        // Arrange
        var pdfDocId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfDocId,
            FileName = "catan-rules.pdf",
            FilePath = "/uploads/catan-rules.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow
        });

        _db.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Catan",
            Description = "Classic trading game"
        });

        _db.SharedGameDocuments.Add(new SharedGameDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            PdfDocumentId = pdfDocId,
            DocumentType = 0,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        });

        await _db.SaveChangesAsync();

        var handler = new JobCompletedStreamHandler(
            _streamServiceMock.Object, _db, Mock.Of<ILogger<JobCompletedStreamHandler>>());

        QueueStreamEvent? captured = null;
        _streamServiceMock
            .Setup(s => s.PublishJobEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()))
            .Callback<QueueStreamEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var notification = new JobCompletedEvent(Guid.NewGuid(), pdfDocId, Guid.NewGuid(), TimeSpan.FromSeconds(30));

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        var data = captured!.Data.Should().BeOfType<JobCompletedData>().Which;
        data.TotalDurationSeconds.Should().Be(30);
        data.FileName.Should().Be("catan-rules.pdf");
        data.SharedGameId.Should().Be(sharedGameId);
        data.GameName.Should().Be("Catan");
    }

    [Fact]
    public async Task JobCompletedStreamHandler_HandlesNullGameInfo_WhenNoSharedGameDocument()
    {
        // Arrange
        var pdfDocId = Guid.NewGuid();

        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfDocId,
            FileName = "orphan-doc.pdf",
            FilePath = "/uploads/orphan-doc.pdf",
            FileSizeBytes = 512,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var handler = new JobCompletedStreamHandler(
            _streamServiceMock.Object, _db, Mock.Of<ILogger<JobCompletedStreamHandler>>());

        QueueStreamEvent? captured = null;
        _streamServiceMock
            .Setup(s => s.PublishJobEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()))
            .Callback<QueueStreamEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var notification = new JobCompletedEvent(Guid.NewGuid(), pdfDocId, Guid.NewGuid(), TimeSpan.FromSeconds(15));

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        var data = captured!.Data.Should().BeOfType<JobCompletedData>().Which;
        data.TotalDurationSeconds.Should().Be(15);
        data.FileName.Should().Be("orphan-doc.pdf");
        data.SharedGameId.Should().BeNull();
        data.GameName.Should().BeNull();
    }

    [Fact]
    public async Task JobCompletedStreamHandler_HandlesNullFileName_WhenPdfDocumentNotFound()
    {
        // Arrange — no PDF document or shared game in DB
        var handler = new JobCompletedStreamHandler(
            _streamServiceMock.Object, _db, Mock.Of<ILogger<JobCompletedStreamHandler>>());

        QueueStreamEvent? captured = null;
        _streamServiceMock
            .Setup(s => s.PublishJobEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()))
            .Callback<QueueStreamEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var notification = new JobCompletedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(10));

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        var data = captured!.Data.Should().BeOfType<JobCompletedData>().Which;
        data.TotalDurationSeconds.Should().Be(10);
        data.FileName.Should().BeNull();
        data.SharedGameId.Should().BeNull();
        data.GameName.Should().BeNull();
    }

    #endregion

    #region JobFailedStreamHandler enrichment

    [Fact]
    public async Task JobFailedStreamHandler_EnrichesWithGameInfo_WhenSharedGameDocumentExists()
    {
        // Arrange
        var pdfDocId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfDocId,
            FileName = "terraforming-mars.pdf",
            FilePath = "/uploads/terraforming-mars.pdf",
            FileSizeBytes = 2048,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow
        });

        _db.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Terraforming Mars",
            Description = "Sci-fi engine builder"
        });

        _db.SharedGameDocuments.Add(new SharedGameDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            PdfDocumentId = pdfDocId,
            DocumentType = 0,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        });

        await _db.SaveChangesAsync();

        var handler = new JobFailedStreamHandler(
            _streamServiceMock.Object, _db, Mock.Of<ILogger<JobFailedStreamHandler>>());

        QueueStreamEvent? captured = null;
        _streamServiceMock
            .Setup(s => s.PublishJobEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()))
            .Callback<QueueStreamEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var notification = new JobFailedEvent(
            Guid.NewGuid(), pdfDocId, Guid.NewGuid(),
            "OCR failed", ProcessingStepType.Extract, 1);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        var data = captured!.Data.Should().BeOfType<JobFailedData>().Which;
        data.Error.Should().Be("OCR failed");
        data.FailedAtStep.Should().Be("Extract");
        data.RetryCount.Should().Be(1);
        data.FileName.Should().Be("terraforming-mars.pdf");
        data.SharedGameId.Should().Be(sharedGameId);
        data.GameName.Should().Be("Terraforming Mars");
    }

    [Fact]
    public async Task JobFailedStreamHandler_HandlesNullGameInfo_WhenNoSharedGameDocument()
    {
        // Arrange
        var pdfDocId = Guid.NewGuid();

        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfDocId,
            FileName = "user-upload.pdf",
            FilePath = "/uploads/user-upload.pdf",
            FileSizeBytes = 768,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var handler = new JobFailedStreamHandler(
            _streamServiceMock.Object, _db, Mock.Of<ILogger<JobFailedStreamHandler>>());

        QueueStreamEvent? captured = null;
        _streamServiceMock
            .Setup(s => s.PublishJobEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()))
            .Callback<QueueStreamEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var notification = new JobFailedEvent(
            Guid.NewGuid(), pdfDocId, Guid.NewGuid(),
            "Chunk failed", ProcessingStepType.Chunk, 0);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        var data = captured!.Data.Should().BeOfType<JobFailedData>().Which;
        data.Error.Should().Be("Chunk failed");
        data.FileName.Should().Be("user-upload.pdf");
        data.SharedGameId.Should().BeNull();
        data.GameName.Should().BeNull();
    }

    #endregion
}
