using Api.BoundedContexts.DocumentProcessing.Application.Handlers;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Comprehensive tests for GetPdfDocumentByIdQueryHandler.
/// Tests document retrieval, mapping to DTO, and null handling.
/// </summary>
public class GetPdfDocumentByIdQueryHandlerTests
{
    private readonly Mock<IPdfDocumentRepository> _documentRepositoryMock;
    private readonly GetPdfDocumentByIdQueryHandler _handler;

    public GetPdfDocumentByIdQueryHandlerTests()
    {
        _documentRepositoryMock = new Mock<IPdfDocumentRepository>();
        _handler = new GetPdfDocumentByIdQueryHandler(_documentRepositoryMock.Object);
    }

    #region Happy Path Tests

    [Fact]
    public async Task Handle_ExistingDocument_ReturnsMappedDto()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var document = new PdfDocumentBuilder()
            .WithId(documentId)
            .WithGameId(gameId)
            .WithFileName("catan-rules.pdf")
            .WithFilePath("/uploads/catan-rules.pdf")
            .WithFileSize(2 * 1024 * 1024) // 2 MB
            .ThatIsCompleted(pageCount: 24)
            .Build();

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var query = new GetPdfDocumentByIdQuery(documentId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(documentId, result.Id);
        Assert.Equal(gameId, result.GameId);
        Assert.Equal("catan-rules.pdf", result.FileName);
        Assert.Equal("/uploads/catan-rules.pdf", result.FilePath);
        Assert.Equal(2 * 1024 * 1024, result.FileSizeBytes);
        Assert.Equal("completed", result.ProcessingStatus);
        Assert.Equal(24, result.PageCount);
        Assert.NotNull(result.ProcessedAt);

        _documentRepositoryMock.Verify(
            r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PendingDocument_ReturnsDtoWithNullPageCount()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = new PdfDocumentBuilder()
            .WithId(documentId)
            .WithFileName("new-upload.pdf")
            .Build(); // Default is pending status

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var query = new GetPdfDocumentByIdQuery(documentId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(documentId, result.Id);
        Assert.Equal("pending", result.ProcessingStatus);
        Assert.Null(result.PageCount);
        Assert.Null(result.ProcessedAt);
    }

    [Fact]
    public async Task Handle_ProcessingDocument_ReturnsDtoWithProcessingStatus()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = new PdfDocumentBuilder()
            .WithId(documentId)
            .WithFileName("processing-doc.pdf")
            .ThatIsProcessing()
            .Build();

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var query = new GetPdfDocumentByIdQuery(documentId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("processing", result.ProcessingStatus);
        Assert.Null(result.PageCount);
        Assert.Null(result.ProcessedAt);
    }

    [Fact]
    public async Task Handle_FailedDocument_ReturnsDtoWithFailedStatus()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = new PdfDocumentBuilder()
            .WithId(documentId)
            .WithFileName("failed-doc.pdf")
            .ThatIsFailed("Extraction error")
            .Build();

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var query = new GetPdfDocumentByIdQuery(documentId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("failed", result.ProcessingStatus);
        Assert.Null(result.PageCount);
    }

    [Fact]
    public async Task Handle_LargeDocument_ReturnsDtoWithCorrectFileSize()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var fileSizeBytes = 50 * 1024 * 1024; // 50 MB
        var document = new PdfDocumentBuilder()
            .WithId(documentId)
            .WithFileSize(fileSizeBytes)
            .ThatIsCompleted(pageCount: 200)
            .Build();

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var query = new GetPdfDocumentByIdQuery(documentId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(fileSizeBytes, result.FileSizeBytes);
        Assert.Equal(200, result.PageCount);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_NonExistentDocument_ReturnsNull()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        var query = new GetPdfDocumentByIdQuery(documentId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);

        _documentRepositoryMock.Verify(
            r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyGuid_ReturnsNull()
    {
        // Arrange
        var documentId = Guid.Empty;

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        var query = new GetPdfDocumentByIdQuery(documentId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = new PdfDocumentBuilder()
            .WithId(documentId)
            .Build();

        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId, cancellationToken))
            .ReturnsAsync(document);

        var query = new GetPdfDocumentByIdQuery(documentId);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        Assert.NotNull(result);
        _documentRepositoryMock.Verify(
            r => r.GetByIdAsync(documentId, cancellationToken),
            Times.Once);
    }

    #endregion
}

