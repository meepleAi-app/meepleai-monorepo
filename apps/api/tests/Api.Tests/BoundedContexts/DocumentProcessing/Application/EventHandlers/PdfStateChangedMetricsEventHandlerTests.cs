using Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.EventHandlers;

[Trait("Category", TestCategories.Unit)]
public sealed class PdfStateChangedMetricsEventHandlerTests
{
    private readonly Mock<IProcessingMetricsService> _metricsServiceMock;
    private readonly Mock<IPdfDocumentRepository> _pdfRepositoryMock;
    private readonly PdfStateChangedMetricsEventHandler _handler;

    public PdfStateChangedMetricsEventHandlerTests()
    {
        _metricsServiceMock = new Mock<IProcessingMetricsService>();
        _pdfRepositoryMock = new Mock<IPdfDocumentRepository>();
        _handler = new PdfStateChangedMetricsEventHandler(
            _metricsServiceMock.Object,
            _pdfRepositoryMock.Object,
            NullLogger<PdfStateChangedMetricsEventHandler>.Instance);
    }

    [Fact]
    public async Task Handle_CompletedStep_RecordsMetric()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var pdf = CreateTestPdf(pdfId);

        _pdfRepositoryMock
            .Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdf);

        var domainEvent = new PdfStateChangedEvent(
            pdfId,
            PdfProcessingState.Extracting,
            PdfProcessingState.Chunking,
            Guid.NewGuid());

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        _metricsServiceMock.Verify(
            s => s.RecordStepDurationAsync(
                pdfId,
                PdfProcessingState.Extracting,
                It.IsAny<TimeSpan>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NonProcessingTransition_SkipsMetricRecording()
    {
        // Arrange
        var pdfId = Guid.NewGuid();

        var domainEvent = new PdfStateChangedEvent(
            pdfId,
            PdfProcessingState.Pending,
            PdfProcessingState.Uploading,
            Guid.NewGuid());

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert - Should not record metric for Pending → Uploading
        _metricsServiceMock.Verify(
            s => s.RecordStepDurationAsync(
                It.IsAny<Guid>(),
                It.IsAny<PdfProcessingState>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_FailedTransition_SkipsMetricRecording()
    {
        // Arrange
        var pdfId = Guid.NewGuid();

        var domainEvent = new PdfStateChangedEvent(
            pdfId,
            PdfProcessingState.Chunking,
            PdfProcessingState.Failed,
            Guid.NewGuid());

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert - Should not record metric for Failed transition
        _metricsServiceMock.Verify(
            s => s.RecordStepDurationAsync(
                It.IsAny<Guid>(),
                It.IsAny<PdfProcessingState>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_PdfNotFound_DoesNotThrow()
    {
        // Arrange
        var pdfId = Guid.NewGuid();

        _pdfRepositoryMock
            .Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument?)null);

        var domainEvent = new PdfStateChangedEvent(
            pdfId,
            PdfProcessingState.Extracting,
            PdfProcessingState.Chunking,
            Guid.NewGuid());

        // Act
        var act = async () => await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert - Should not throw, just log warning
        await act.Should().NotThrowAsync();
    }

    private static Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument CreateTestPdf(Guid id)
    {
        var pdf = new Api.BoundedContexts.DocumentProcessing.Domain.Entities.PdfDocument(
            id,
            Guid.NewGuid(),
            new FileName("test.pdf"),
            "/tmp/test.pdf",
            new FileSize(1024000),
            Guid.NewGuid());

        // Transition through states to set timestamps
        pdf.TransitionTo(PdfProcessingState.Uploading);
        pdf.TransitionTo(PdfProcessingState.Extracting);
        pdf.TransitionTo(PdfProcessingState.Chunking);

        return pdf;
    }
}
