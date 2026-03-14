using Api.BoundedContexts.DocumentProcessing.Application.Handlers;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Tests for GetPdfTextQueryHandler.
/// ISSUE-1818: Migrated to FluentAssertions for improved readability.
/// Tests PDF extracted text retrieval.
/// NOTE: Uses DbContext directly - simplified tests due to mocking complexity.
/// ISSUE-1674: Convert to integration tests or refactor handler to use repository.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetPdfTextQueryHandlerTests
{
    private readonly Mock<ILogger<GetPdfTextQueryHandler>> _loggerMock;

    public GetPdfTextQueryHandlerTests()
    {
        _loggerMock = new Mock<ILogger<GetPdfTextQueryHandler>>();
    }
    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        // Act
        var handler = new GetPdfTextQueryHandler(
            dbContext,
            _loggerMock.Object);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () =>
            new GetPdfTextQueryHandler(
                null!,
                _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        // Act & Assert
        Action act = () =>
            new GetPdfTextQueryHandler(
                dbContext,
                null!);

        act.Should().Throw<ArgumentNullException>();
    }
    [Fact]
    public void Query_HasCorrectPdfIdProperty()
    {
        // Arrange
        var pdfId = Guid.NewGuid();

        // Act
        var query = new GetPdfTextQuery(pdfId);

        // Assert
        query.PdfId.Should().Be(pdfId);
    }
    [Fact]
    public void PdfTextResult_ConstructsCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var fileName = "rulebook.pdf";
        var extractedText = "Game rules: Setup\n1. Place the board...";
        var processingState = "Ready";
        var processedAt = DateTime.UtcNow;
        var pageCount = 24;
        var characterCount = 15000;

        // Act
        var result = new PdfTextResult(
            id,
            fileName,
            extractedText,
            processingState,
            processedAt,
            pageCount,
            characterCount,
            null);

        // Assert
        result.Id.Should().Be(id);
        result.FileName.Should().Be(fileName);
        result.ExtractedText.Should().Be(extractedText);
        result.ProcessingState.Should().Be(processingState);
        result.ProcessedAt.Should().Be(processedAt);
        result.PageCount.Should().Be(pageCount);
        result.CharacterCount.Should().Be(characterCount);
        result.ProcessingError.Should().BeNull();
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
            "Failed",
            null,
            null,
            null,
            errorMessage);

        // Assert
        result.ProcessingState.Should().Be("Failed");
        result.ProcessingError.Should().Be(errorMessage);
        result.ExtractedText.Should().BeNull();
        result.PageCount.Should().BeNull();
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
        result.ExtractedText.Should().BeNull();
        result.ProcessedAt.Should().BeNull();
        result.PageCount.Should().BeNull();
        result.CharacterCount.Should().BeNull();
        result.ProcessingError.Should().BeNull();
    }
    // NOTE: Full integration tests for Handle method should be in integration test suite
    // due to DbContext dependency complexity. See integration-tests.yml workflow.
}
