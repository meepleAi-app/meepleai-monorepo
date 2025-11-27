using Api.BoundedContexts.DocumentProcessing.Application.Handlers;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Infrastructure;
using Api.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Tests for GetPdfTextQueryHandler.
/// Tests PDF extracted text retrieval.
/// NOTE: Uses DbContext directly - simplified tests due to mocking complexity.
/// TODO: Convert to integration tests or refactor handler to use repository.
/// </summary>
public class GetPdfTextQueryHandlerTests
{
    private readonly Mock<ILogger<GetPdfTextQueryHandler>> _loggerMock;

    public GetPdfTextQueryHandlerTests()
    {
        _loggerMock = new Mock<ILogger<GetPdfTextQueryHandler>>();
    }

    #region Construction Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var dbContext = DbContextHelper.CreateInMemoryDbContext();

        // Act
        var handler = new GetPdfTextQueryHandler(
            dbContext,
            _loggerMock.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GetPdfTextQueryHandler(
                null!,
                _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var dbContext = DbContextHelper.CreateInMemoryDbContext();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GetPdfTextQueryHandler(
                dbContext,
                null!));
    }

    #endregion

    #region Query Tests

    [Fact]
    public void Query_HasCorrectPdfIdProperty()
    {
        // Arrange
        var pdfId = Guid.NewGuid();

        // Act
        var query = new GetPdfTextQuery(pdfId);

        // Assert
        Assert.Equal(pdfId, query.PdfId);
    }

    #endregion

    #region Result Tests

    [Fact]
    public void PdfTextResult_ConstructsCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var fileName = "rulebook.pdf";
        var extractedText = "Game rules: Setup\n1. Place the board...";
        var processingStatus = "completed";
        var processedAt = DateTime.UtcNow;
        var pageCount = 24;
        var characterCount = 15000;

        // Act
        var result = new PdfTextResult(
            id,
            fileName,
            extractedText,
            processingStatus,
            processedAt,
            pageCount,
            characterCount,
            null);

        // Assert
        Assert.Equal(id, result.Id);
        Assert.Equal(fileName, result.FileName);
        Assert.Equal(extractedText, result.ExtractedText);
        Assert.Equal(processingStatus, result.ProcessingStatus);
        Assert.Equal(processedAt, result.ProcessedAt);
        Assert.Equal(pageCount, result.PageCount);
        Assert.Equal(characterCount, result.CharacterCount);
        Assert.Null(result.ProcessingError);
    }

    [Fact]
    public void PdfTextResult_WithError_IncludesErrorMessage()
    {
        // Arrange
        var id = Guid.NewGuid();
        var fileName = "failed.pdf";
        var errorMessage = "Extraction failed: Corrupted PDF";

        // Act
        var result = new PdfTextResult(
            id,
            fileName,
            null,
            "failed",
            null,
            null,
            null,
            errorMessage);

        // Assert
        Assert.Equal("failed", result.ProcessingStatus);
        Assert.Equal(errorMessage, result.ProcessingError);
        Assert.Null(result.ExtractedText);
        Assert.Null(result.PageCount);
    }

    [Fact]
    public void PdfTextResult_WithNullValues_AllowsNulls()
    {
        // Arrange
        var id = Guid.NewGuid();
        var fileName = "pending.pdf";

        // Act
        var result = new PdfTextResult(
            id,
            fileName,
            null,
            "pending",
            null,
            null,
            null,
            null);

        // Assert
        Assert.Null(result.ExtractedText);
        Assert.Null(result.ProcessedAt);
        Assert.Null(result.PageCount);
        Assert.Null(result.CharacterCount);
        Assert.Null(result.ProcessingError);
    }

    #endregion

    // NOTE: Full integration tests for Handle method should be in integration test suite
    // due to DbContext dependency complexity. See integration-tests.yml workflow.
}

