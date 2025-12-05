using Api.BoundedContexts.DocumentProcessing.Application.Handlers;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Comprehensive tests for GetPdfDocumentsByGameQueryHandler.
/// Tests document retrieval by game ID, list mapping, and filtering.
/// ISSUE-1818: Migrated to FluentAssertions for improved readability.
/// </summary>
public class GetPdfDocumentsByGameQueryHandlerTests
{
    private readonly Mock<IPdfDocumentRepository> _documentRepositoryMock;
    private readonly GetPdfDocumentsByGameQueryHandler _handler;

    public GetPdfDocumentsByGameQueryHandlerTests()
    {
        _documentRepositoryMock = new Mock<IPdfDocumentRepository>();
        _handler = new GetPdfDocumentsByGameQueryHandler(_documentRepositoryMock.Object);
    }
    [Fact]
    public async Task Handle_WithMultipleDocuments_ReturnsAllMappedDtos()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var documents = new List<PdfDocument>
        {
            new PdfDocumentBuilder()
                .WithGameId(gameId)
                .WithFileName("rulebook-en.pdf")
                .ThatIsCompleted(pageCount: 24)
                .Build(),
            new PdfDocumentBuilder()
                .WithGameId(gameId)
                .WithFileName("quick-start.pdf")
                .ThatIsCompleted(pageCount: 4)
                .Build(),
            new PdfDocumentBuilder()
                .WithGameId(gameId)
                .WithFileName("reference-card.pdf")
                .ThatIsProcessing()
                .Build()
        };

        _documentRepositoryMock
            .Setup(r => r.FindByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        var query = new GetPdfDocumentsByGameQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(3);

        result[0].FileName.Should().Be("rulebook-en.pdf");
        result[0].GameId.Should().Be(gameId);
        result[0].ProcessingStatus.Should().Be("completed");
        result[0].PageCount.Should().Be(24);

        result[1].FileName.Should().Be("quick-start.pdf");
        result[1].PageCount.Should().Be(4);

        result[2].FileName.Should().Be("reference-card.pdf");
        result[2].ProcessingStatus.Should().Be("processing");
        result[2].PageCount.Should().BeNull();

        _documentRepositoryMock.Verify(
            r => r.FindByGameIdAsync(gameId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithSingleDocument_ReturnsSingleElementList()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var documents = new List<PdfDocument>
        {
            new PdfDocumentBuilder()
                .WithGameId(gameId)
                .WithFileName("rulebook.pdf")
                .ThatIsCompleted(pageCount: 32)
                .Build()
        };

        _documentRepositoryMock
            .Setup(r => r.FindByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        var query = new GetPdfDocumentsByGameQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainSingle();
        result[0].FileName.Should().Be("rulebook.pdf");
        result[0].PageCount.Should().Be(32);
    }

    [Fact]
    public async Task Handle_WithNoDocuments_ReturnsEmptyList()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var documents = new List<PdfDocument>();

        _documentRepositoryMock
            .Setup(r => r.FindByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        var query = new GetPdfDocumentsByGameQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithMixedStatuses_MapsAllCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var documents = new List<PdfDocument>
        {
            new PdfDocumentBuilder()
                .WithGameId(gameId)
                .WithFileName("pending-doc.pdf")
                .Build(), // Pending status

            new PdfDocumentBuilder()
                .WithGameId(gameId)
                .WithFileName("processing-doc.pdf")
                .ThatIsProcessing()
                .Build(),

            new PdfDocumentBuilder()
                .WithGameId(gameId)
                .WithFileName("completed-doc.pdf")
                .ThatIsCompleted(pageCount: 10)
                .Build(),

            new PdfDocumentBuilder()
                .WithGameId(gameId)
                .WithFileName("failed-doc.pdf")
                .ThatIsFailed("Error occurred")
                .Build()
        };

        _documentRepositoryMock
            .Setup(r => r.FindByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        var query = new GetPdfDocumentsByGameQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Count.Should().Be(4);
        result[0].ProcessingStatus.Should().Be("pending");
        result[1].ProcessingStatus.Should().Be("processing");
        result[2].ProcessingStatus.Should().Be("completed");
        result[3].ProcessingStatus.Should().Be("failed");
    }

    [Fact]
    public async Task Handle_WithVaryingFileSizes_MapsCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var documents = new List<PdfDocument>
        {
            new PdfDocumentBuilder()
                .WithGameId(gameId)
                .WithFileName("small.pdf")
                .ThatIsSmall() // 100 KB
                .ThatIsCompleted(pageCount: 2)
                .Build(),

            new PdfDocumentBuilder()
                .WithGameId(gameId)
                .WithFileName("large.pdf")
                .ThatIsLarge() // 10 MB
                .ThatIsCompleted(pageCount: 150)
                .Build()
        };

        _documentRepositoryMock
            .Setup(r => r.FindByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        var query = new GetPdfDocumentsByGameQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Count.Should().Be(2);
        result[0].FileSizeBytes.Should().Be(100 * 1024); // 100 KB
        result[1].FileSizeBytes.Should().Be(PdfUploadTestConstants.FileSizes.TestMaxBytes); // 10 MB
    }
    [Fact]
    public async Task Handle_WithEmptyGameId_ReturnsEmptyList()
    {
        // Arrange
        var gameId = Guid.Empty;
        var documents = new List<PdfDocument>();

        _documentRepositoryMock
            .Setup(r => r.FindByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        var query = new GetPdfDocumentsByGameQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentGameId = Guid.NewGuid();
        var documents = new List<PdfDocument>();

        _documentRepositoryMock
            .Setup(r => r.FindByGameIdAsync(nonExistentGameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents);

        var query = new GetPdfDocumentsByGameQuery(nonExistentGameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var documents = new List<PdfDocument>
        {
            new PdfDocumentBuilder()
                .WithGameId(gameId)
                .Build()
        };

        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _documentRepositoryMock
            .Setup(r => r.FindByGameIdAsync(gameId, cancellationToken))
            .ReturnsAsync(documents);

        var query = new GetPdfDocumentsByGameQuery(gameId);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainSingle();

        _documentRepositoryMock.Verify(
            r => r.FindByGameIdAsync(gameId, cancellationToken),
            Times.Once);
    }
}