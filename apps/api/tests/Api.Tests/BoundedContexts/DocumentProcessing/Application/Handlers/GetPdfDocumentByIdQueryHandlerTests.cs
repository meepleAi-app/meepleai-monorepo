using Api.BoundedContexts.DocumentProcessing.Application.Handlers;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Comprehensive tests for GetPdfDocumentByIdQueryHandler.
/// Tests document retrieval, mapping to DTO, and null handling.
/// ISSUE-1818: Migrated to FluentAssertions for improved readability.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetPdfDocumentByIdQueryHandlerTests
{
    private readonly Mock<IPdfDocumentRepository> _documentRepositoryMock;
    private readonly GetPdfDocumentByIdQueryHandler _handler;

    public GetPdfDocumentByIdQueryHandlerTests()
    {
        _documentRepositoryMock = new Mock<IPdfDocumentRepository>();
        _handler = new GetPdfDocumentByIdQueryHandler(_documentRepositoryMock.Object);
    }
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
        result.Should().NotBeNull();
        result.Id.Should().Be(documentId);
        result.GameId.Should().Be(gameId);
        result.FileName.Should().Be("catan-rules.pdf");
        result.FilePath.Should().Be("/uploads/catan-rules.pdf");
        result.FileSizeBytes.Should().Be(2 * 1024 * 1024);
        result.ProcessingStatus.Should().Be("completed");
        result.PageCount.Should().Be(24);
        result.ProcessedAt.Should().NotBeNull();

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
        result.Should().NotBeNull();
        result.Id.Should().Be(documentId);
        result.ProcessingStatus.Should().Be("pending");
        result.PageCount.Should().BeNull();
        result.ProcessedAt.Should().BeNull();
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
        result.Should().NotBeNull();
        result.ProcessingStatus.Should().Be("processing");
        result.PageCount.Should().BeNull();
        result.ProcessedAt.Should().BeNull();
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
        result.Should().NotBeNull();
        result.ProcessingStatus.Should().Be("failed");
        result.PageCount.Should().BeNull();
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
        result.Should().NotBeNull();
        result.FileSizeBytes.Should().Be(fileSizeBytes);
        result.PageCount.Should().Be(200);
    }
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
        result.Should().BeNull();

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
        result.Should().BeNull();
    }
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
        result.Should().NotBeNull();
        _documentRepositoryMock.Verify(
            r => r.GetByIdAsync(documentId, cancellationToken),
            Times.Once);
    }
}