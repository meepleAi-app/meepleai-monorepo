using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Handlers;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Tests for GetPdfMetricsQueryHandler.
/// Issue #4219: Duration metrics and ETA calculation tests.
/// Issue #4212: Updated for data-driven ETA calculation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetPdfMetricsQueryHandlerTests
{
    private readonly Mock<IPdfDocumentRepository> _repositoryMock;
    private readonly Mock<IProcessingMetricsService> _metricsServiceMock;
    private readonly Mock<ILogger<GetPdfMetricsQueryHandler>> _loggerMock;

    public GetPdfMetricsQueryHandlerTests()
    {
        _repositoryMock = new Mock<IPdfDocumentRepository>();
        _metricsServiceMock = new Mock<IProcessingMetricsService>();
        _loggerMock = new Mock<ILogger<GetPdfMetricsQueryHandler>>();

        // Issue #4212: Default mock behavior for ETA calculation (returns null = use fallback)
        _metricsServiceMock
            .Setup(s => s.CalculateETAAsync(
                It.IsAny<Guid>(),
                It.IsAny<PdfProcessingState>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TimeSpan?)null);
    }

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var handler = new GetPdfMetricsQueryHandler(
            _repositoryMock.Object,
            _metricsServiceMock.Object,
            _loggerMock.Object);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () =>
            new GetPdfMetricsQueryHandler(
                null!,
                _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("repository");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () =>
            new GetPdfMetricsQueryHandler(
                _repositoryMock.Object,
                null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Query_HasCorrectDocumentIdProperty()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        // Act
        var query = new GetPdfMetricsQuery(documentId);

        // Assert
        query.DocumentId.Should().Be(documentId);
    }

    [Fact]
    public async Task Handle_WithExistingDocument_ReturnsMetrics()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var document = new PdfDocument(
            id: documentId,
            gameId: gameId,
            fileName: new FileName("test.pdf"),
            filePath: "/path/test.pdf",
            fileSize: new FileSize(1024000),
            uploadedByUserId: userId
        );

        // Simulate state transitions with timing
        document.TransitionTo(PdfProcessingState.Uploading);
        await Task.Delay(10); // Small delay for timing
        document.TransitionTo(PdfProcessingState.Extracting);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var handler = new GetPdfMetricsQueryHandler(
            _repositoryMock.Object,
            _metricsServiceMock.Object,
            _loggerMock.Object);

        var query = new GetPdfMetricsQuery(documentId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.DocumentId.Should().Be(documentId);
        result.CurrentState.Should().Be(PdfProcessingState.Extracting);
        result.ProgressPercentage.Should().Be(30); // Extracting = 30%
        result.RetryCount.Should().Be(0);
        result.StateDurations.Should().ContainKey("Uploading");
        result.EstimatedTimeRemaining.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithNonExistentDocument_ThrowsNotFoundException()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        var handler = new GetPdfMetricsQueryHandler(
            _repositoryMock.Object,
            _metricsServiceMock.Object,
            _loggerMock.Object);

        var query = new GetPdfMetricsQuery(documentId);

        // Act
        Func<Task> act = async () => await handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"PDF document {documentId} not found");
    }

    [Fact]
    public async Task Handle_WithCompletedDocument_ReturnsZeroETA()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var document = new PdfDocument(
            id: documentId,
            gameId: gameId,
            fileName: new FileName("complete.pdf"),
            filePath: "/path/complete.pdf",
            fileSize: new FileSize(5120000),
            uploadedByUserId: userId
        );

        // Transition to Ready state
        document.TransitionTo(PdfProcessingState.Uploading);
        document.TransitionTo(PdfProcessingState.Extracting);
        document.TransitionTo(PdfProcessingState.Chunking);
        document.TransitionTo(PdfProcessingState.Embedding);
        document.TransitionTo(PdfProcessingState.Indexing);
        document.TransitionTo(PdfProcessingState.Ready);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var handler = new GetPdfMetricsQueryHandler(
            _repositoryMock.Object,
            _metricsServiceMock.Object,
            _loggerMock.Object);

        var query = new GetPdfMetricsQuery(documentId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.ProgressPercentage.Should().Be(100);
        result.EstimatedTimeRemaining.Should().Be(TimeSpan.Zero);
        result.TotalDuration.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithFailedDocument_ReturnsZeroProgress()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var document = new PdfDocument(
            id: documentId,
            gameId: gameId,
            fileName: new FileName("failed.pdf"),
            filePath: "/path/failed.pdf",
            fileSize: new FileSize(2048000),
            uploadedByUserId: userId
        );

        document.MarkAsFailed("Test error", ErrorCategory.Service, PdfProcessingState.Extracting);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var handler = new GetPdfMetricsQueryHandler(
            _repositoryMock.Object,
            _metricsServiceMock.Object,
            _loggerMock.Object);

        var query = new GetPdfMetricsQuery(documentId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.CurrentState.Should().Be(PdfProcessingState.Failed);
        result.ProgressPercentage.Should().Be(0);
        result.EstimatedTimeRemaining.Should().Be(TimeSpan.Zero);
    }
}
