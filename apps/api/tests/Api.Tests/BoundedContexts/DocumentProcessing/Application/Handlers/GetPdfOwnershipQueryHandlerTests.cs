using Api.BoundedContexts.DocumentProcessing.Application.Handlers;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Comprehensive tests for GetPdfOwnershipQueryHandler.
/// Tests PDF ownership verification for authorization (SEC-02: Row-Level Security).
/// ISSUE-1818: Migrated to FluentAssertions for improved readability.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetPdfOwnershipQueryHandlerTests
{
    private readonly Mock<IPdfDocumentRepository> _documentRepositoryMock;
    private readonly Mock<ILogger<GetPdfOwnershipQueryHandler>> _loggerMock;
    private readonly GetPdfOwnershipQueryHandler _handler;

    public GetPdfOwnershipQueryHandlerTests()
    {
        _documentRepositoryMock = new Mock<IPdfDocumentRepository>();
        _loggerMock = new Mock<ILogger<GetPdfOwnershipQueryHandler>>();
        _handler = new GetPdfOwnershipQueryHandler(
            _documentRepositoryMock.Object,
            _loggerMock.Object);
    }
    [Fact]
    public async Task Handle_ExistingDocument_ReturnsOwnershipResult()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var document = new PdfDocumentBuilder()
            .WithId(pdfId)
            .WithGameId(gameId)
            .WithUploadedBy(userId)
            .ThatIsCompleted(pageCount: 20)
            .Build();

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var query = new GetPdfOwnershipQuery(pdfId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(pdfId);
        result.UploadedByUserId.Should().Be(userId);
        result.GameId.Should().Be(gameId);
        result.ProcessingState.Should().Be("Ready");

        _documentRepositoryMock.Verify(
            r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PendingDocument_ReturnsOwnershipWithPendingStatus()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var document = new PdfDocumentBuilder()
            .WithId(pdfId)
            .WithUploadedBy(userId)
            .Build(); // Default is pending

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var query = new GetPdfOwnershipQuery(pdfId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(pdfId);
        result.UploadedByUserId.Should().Be(userId);
        result.ProcessingState.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_ProcessingDocument_ReturnsOwnershipWithProcessingState()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var document = new PdfDocumentBuilder()
            .WithId(pdfId)
            .WithUploadedBy(userId)
            .ThatIsProcessing()
            .Build();

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var query = new GetPdfOwnershipQuery(pdfId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.ProcessingState.Should().Be("Uploading");
    }

    [Fact]
    public async Task Handle_FailedDocument_ReturnsOwnershipWithFailedStatus()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var document = new PdfDocumentBuilder()
            .WithId(pdfId)
            .WithUploadedBy(userId)
            .ThatIsFailed("Processing error")
            .Build();

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var query = new GetPdfOwnershipQuery(pdfId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.ProcessingState.Should().Be("Failed");
    }
    [Fact]
    public async Task Handle_NonExistentDocument_ReturnsNull()
    {
        // Arrange
        var pdfId = Guid.NewGuid();

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        var query = new GetPdfOwnershipQuery(pdfId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();

        // Verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains($"PDF {pdfId} not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyGuid_ReturnsNull()
    {
        // Arrange
        var pdfId = Guid.Empty;

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);

        var query = new GetPdfOwnershipQuery(pdfId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
    [Fact]
    public async Task Handle_RepositoryThrowsException_ReturnsNullAndLogsError()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var exception = new Exception("Database connection failed");

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var query = new GetPdfOwnershipQuery(pdfId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();

        // Verify error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains($"Error retrieving PDF ownership for {pdfId}")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var document = new PdfDocumentBuilder()
            .WithId(pdfId)
            .Build();

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(pdfId, cancellationToken))
            .ReturnsAsync(document);

        var query = new GetPdfOwnershipQuery(pdfId);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        _documentRepositoryMock.Verify(
            r => r.GetByIdAsync(pdfId, cancellationToken),
            Times.Once);
    }
}
