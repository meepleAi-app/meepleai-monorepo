using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Xunit;

namespace Api.Tests;

/// <summary>
/// AI-08: BDD-style unit tests for page-aware PDF text extraction
///
/// Feature: AI-08 - Page-aware PDF text extraction
/// As a Developer
/// I want to extract text from PDFs with accurate page numbers
/// So that RAG search results can display citation page numbers
///
/// IMPORTANT: This is TDD RED phase - tests WILL FAIL because:
/// - ExtractPagedTextAsync() method doesn't exist yet
/// - PagedExtractionResult class doesn't exist yet
/// - PagedTextChunk record doesn't exist yet
///
/// These tests define the contract for the new page-aware extraction feature.
/// </summary>
public class PdfTextExtractionServicePagedTests : IDisposable
{
    private readonly Mock<ILogger<PdfTextExtractionService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IOcrService> _ocrServiceMock;
    private readonly PdfTextExtractionService _service;
    private readonly List<string> _tempFiles = new();

    public PdfTextExtractionServicePagedTests()
    {
        // Configure QuestPDF for testing (community license)
        QuestPDF.Settings.License = LicenseType.Community;

        _loggerMock = new Mock<ILogger<PdfTextExtractionService>>();
        _configurationMock = new Mock<IConfiguration>();
        _ocrServiceMock = new Mock<IOcrService>();

        // Service with OCR mock for comprehensive testing
        _service = new PdfTextExtractionService(
            _loggerMock.Object,
            _configurationMock.Object,
            _ocrServiceMock.Object);
    }

    public void Dispose()
    {
        // Clean up temporary PDF files created during tests
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private string CreateTempPdfPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_paged_{Guid.NewGuid()}.pdf");
        _tempFiles.Add(path);
        return path;
    }

    private void CreateMultiPagePdf(string filePath, params string[] pageContents)
    {
        Document.Create(container =>
        {
            foreach (var content in pageContents)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.Content().Text(content);
                });
            }
        }).GeneratePdf(filePath);
    }

    // ===== Core Page-Aware Extraction Tests =====

    /// <summary>
    /// Scenario: Extract text from multi-page PDF with accurate page numbers
    ///   Given a PDF with 3 pages containing distinct text
    ///   When I call ExtractPagedTextAsync
    ///   Then Success should be true
    ///   And TotalPageCount should be 3
    ///   And PageChunks should have 3 items
    ///   And page numbers should be 1, 2, 3 respectively
    ///   And each PageChunk should contain the correct page text
    /// </summary>
    [Fact]
    public async Task ExtractPagedTextAsync_MultiPagePdf_ReturnsAccuratePageNumbers()
    {
        // Arrange: Create a 3-page PDF with distinct content
        var pdfPath = CreateTempPdfPath();
        CreateMultiPagePdf(pdfPath,
            "Page one: Setup instructions for the game.",
            "Page two: How to play the game step by step.",
            "Page three: Winning conditions and scoring rules.");

        // Act: Extract text with page information
        var result = await _service.ExtractPagedTextAsync(pdfPath);

        // Assert: Verify page-aware extraction
        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(3, result.TotalPageCount);
        Assert.NotNull(result.PageChunks);
        Assert.Equal(3, result.PageChunks.Count);

        // Verify page 1
        Assert.Equal(1, result.PageChunks[0].PageNumber);
        Assert.Contains("Page one", result.PageChunks[0].Text);
        Assert.Contains("Setup instructions", result.PageChunks[0].Text);
        Assert.False(result.PageChunks[0].IsEmpty);

        // Verify page 2
        Assert.Equal(2, result.PageChunks[1].PageNumber);
        Assert.Contains("Page two", result.PageChunks[1].Text);
        Assert.Contains("How to play", result.PageChunks[1].Text);
        Assert.False(result.PageChunks[1].IsEmpty);

        // Verify page 3
        Assert.Equal(3, result.PageChunks[2].PageNumber);
        Assert.Contains("Page three", result.PageChunks[2].Text);
        Assert.Contains("Winning conditions", result.PageChunks[2].Text);
        Assert.False(result.PageChunks[2].IsEmpty);
    }

    /// <summary>
    /// Scenario: Handle empty pages gracefully
    ///   Given a PDF where page 2 is blank
    ///   When I call ExtractPagedTextAsync
    ///   Then Success should be true
    ///   And TotalPageCount should be 3
    ///   And PageChunks[1].IsEmpty should be true
    ///   And PageChunks[0] and PageChunks[2] should have content
    ///   And all page numbers should be correct (1, 2, 3)
    /// </summary>
    [Fact]
    public async Task ExtractPagedTextAsync_EmptyPages_HandledGracefully()
    {
        // Arrange: Create PDF with blank middle page
        var pdfPath = CreateTempPdfPath();
        CreateMultiPagePdf(pdfPath,
            "First page has content.",
            "",  // Empty page 2
            "Third page also has content.");

        // Act
        var result = await _service.ExtractPagedTextAsync(pdfPath);

        // Assert: Empty page should be tracked with correct page number
        Assert.True(result.Success);
        Assert.Equal(3, result.TotalPageCount);
        Assert.Equal(3, result.PageChunks.Count);

        // Page 1: Has content
        Assert.Equal(1, result.PageChunks[0].PageNumber);
        Assert.False(result.PageChunks[0].IsEmpty);
        Assert.Contains("First page", result.PageChunks[0].Text);

        // Page 2: Empty
        Assert.Equal(2, result.PageChunks[1].PageNumber);
        Assert.True(result.PageChunks[1].IsEmpty);
        Assert.True(string.IsNullOrWhiteSpace(result.PageChunks[1].Text));

        // Page 3: Has content
        Assert.Equal(3, result.PageChunks[2].PageNumber);
        Assert.False(result.PageChunks[2].IsEmpty);
        Assert.Contains("Third page", result.PageChunks[2].Text);
    }

    /// <summary>
    /// Scenario: Extraction failure returns structured error
    ///   Given a corrupted PDF file
    ///   When I call ExtractPagedTextAsync
    ///   Then Success should be false
    ///   And Error should contain descriptive message
    ///   And PageChunks should be null or empty
    ///   And TotalPageCount should be 0
    /// </summary>
    [Fact]
    public async Task ExtractPagedTextAsync_CorruptedPdf_ReturnsStructuredError()
    {
        // Arrange: Create invalid PDF file
        var pdfPath = CreateTempPdfPath();
        File.WriteAllText(pdfPath, "This is not a valid PDF file at all");

        // Act
        var result = await _service.ExtractPagedTextAsync(pdfPath);

        // Assert: Failure with structured error
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("failed", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.TotalPageCount);
        Assert.True(result.PageChunks == null || result.PageChunks.Count == 0);
    }

    // ===== Edge Cases =====

    /// <summary>
    /// Scenario: Single-page PDF returns correct page number
    ///   Given a PDF with only 1 page
    ///   When I call ExtractPagedTextAsync
    ///   Then TotalPageCount should be 1
    ///   And PageChunks should have exactly 1 item
    ///   And that item should have PageNumber = 1
    /// </summary>
    [Fact]
    public async Task ExtractPagedTextAsync_SinglePagePdf_ReturnsPageNumberOne()
    {
        // Arrange: Single page PDF
        var pdfPath = CreateTempPdfPath();
        CreateMultiPagePdf(pdfPath, "Only one page of content.");

        // Act
        var result = await _service.ExtractPagedTextAsync(pdfPath);

        // Assert: Single page with number 1
        Assert.True(result.Success);
        Assert.Equal(1, result.TotalPageCount);
        Assert.Single(result.PageChunks);
        Assert.Equal(1, result.PageChunks[0].PageNumber);
        Assert.Contains("Only one page", result.PageChunks[0].Text);
    }

    /// <summary>
    /// Scenario: Large PDF maintains accurate page numbers
    ///   Given a PDF with 10 pages
    ///   When I call ExtractPagedTextAsync
    ///   Then TotalPageCount should be 10
    ///   And page numbers should range from 1 to 10
    ///   And no page should have number 0 or > 10
    /// </summary>
    [Fact]
    public async Task ExtractPagedTextAsync_LargePdf_MaintainsAccuratePageNumbers()
    {
        // Arrange: 10-page PDF
        var pdfPath = CreateTempPdfPath();
        var pages = Enumerable.Range(1, 10)
            .Select(i => $"Content for page {i}")
            .ToArray();
        CreateMultiPagePdf(pdfPath, pages);

        // Act
        var result = await _service.ExtractPagedTextAsync(pdfPath);

        // Assert: All 10 pages with correct numbers
        Assert.True(result.Success);
        Assert.Equal(10, result.TotalPageCount);
        Assert.Equal(10, result.PageChunks.Count);

        // Verify each page has sequential number
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(i + 1, result.PageChunks[i].PageNumber);
            Assert.Contains($"page {i + 1}", result.PageChunks[i].Text);
        }

        // No page should have invalid page number
        Assert.DoesNotContain(result.PageChunks, chunk => chunk.PageNumber <= 0);
        Assert.DoesNotContain(result.PageChunks, chunk => chunk.PageNumber > 10);
    }

    // ===== Validation Tests =====

    /// <summary>
    /// Scenario: Null file path returns failure
    ///   Given a null file path
    ///   When I call ExtractPagedTextAsync
    ///   Then Success should be false
    ///   And Error should mention file path is required
    /// </summary>
    [Fact]
    public async Task ExtractPagedTextAsync_NullFilePath_ReturnsFailure()
    {
        // Act
        var result = await _service.ExtractPagedTextAsync(null!);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scenario: Empty file path returns failure
    ///   Given an empty string file path
    ///   When I call ExtractPagedTextAsync
    ///   Then Success should be false
    /// </summary>
    [Fact]
    public async Task ExtractPagedTextAsync_EmptyFilePath_ReturnsFailure()
    {
        // Act
        var result = await _service.ExtractPagedTextAsync(string.Empty);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    /// <summary>
    /// Scenario: Non-existent file returns failure
    ///   Given a file path that doesn't exist
    ///   When I call ExtractPagedTextAsync
    ///   Then Success should be false
    ///   And Error should mention file not found
    /// </summary>
    [Fact]
    public async Task ExtractPagedTextAsync_NonExistentFile_ReturnsFailure()
    {
        // Arrange: Non-existent path
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.pdf");

        // Act
        var result = await _service.ExtractPagedTextAsync(nonExistentPath);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ===== Result Structure Tests =====

    /// <summary>
    /// Scenario: PagedExtractionResult success factory method sets properties correctly
    ///   Given successful page extraction
    ///   When I create a success result
    ///   Then all properties should be set correctly
    /// </summary>
    [Fact]
    public void PagedExtractionResult_CreateSuccess_SetsPropertiesCorrectly()
    {
        // Arrange: Sample page chunks
        var pageChunks = new List<PagedTextChunk>
        {
            new PagedTextChunk("Page 1 content", 1, 0, "Page 1 content".Length - 1),
            new PagedTextChunk("Page 2 content", 2, 0, "Page 2 content".Length - 1),
            new PagedTextChunk("", 3, 0, 0)  // Empty page
        };

        // Act: Create success result
        var result = PagedExtractionResult.CreateSuccess(pageChunks, 3);

        // Assert: Verify all properties
        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(3, result.TotalPageCount);
        Assert.NotNull(result.PageChunks);
        Assert.Equal(3, result.PageChunks.Count);
        Assert.Same(pageChunks, result.PageChunks);
    }

    /// <summary>
    /// Scenario: PagedExtractionResult failure factory method sets properties correctly
    ///   Given an extraction error
    ///   When I create a failure result
    ///   Then Success should be false and error message set
    /// </summary>
    [Fact]
    public void PagedExtractionResult_CreateFailure_SetsPropertiesCorrectly()
    {
        // Act: Create failure result
        var result = PagedExtractionResult.CreateFailure("Test extraction error");

        // Assert: Verify failure properties
        Assert.False(result.Success);
        Assert.Equal("Test extraction error", result.Error);
        Assert.Equal(0, result.TotalPageCount);
        Assert.True(result.PageChunks == null || result.PageChunks.Count == 0);
    }

    /// <summary>
    /// Scenario: PagedTextChunk record properties work correctly
    ///   Given page chunk data
    ///   When I create a PagedTextChunk
    ///   Then properties should be accessible
    /// </summary>
    [Fact]
    public void PagedTextChunk_Properties_WorkCorrectly()
    {
        // Act: Create page chunk
        var text = "Sample text content";
        var chunk = new PagedTextChunk(text, 5, 0, text.Length - 1);

        // Assert: Verify properties
        Assert.Equal(5, chunk.PageNumber);
        Assert.Equal("Sample text content", chunk.Text);
        Assert.False(chunk.IsEmpty);

        // Empty chunk
        var emptyChunk = new PagedTextChunk("", 7, 0, 0);
        Assert.Equal(7, emptyChunk.PageNumber);
        Assert.True(emptyChunk.IsEmpty);
    }

    // ===== Logging Tests =====

    /// <summary>
    /// Scenario: Successful extraction logs appropriate information
    ///   Given a valid multi-page PDF
    ///   When extraction completes successfully
    ///   Then information log should be written with page count
    /// </summary>
    [Fact]
    public async Task ExtractPagedTextAsync_SuccessfulExtraction_LogsInformation()
    {
        // Arrange
        var pdfPath = CreateTempPdfPath();
        CreateMultiPagePdf(pdfPath, "Page 1", "Page 2");

        // Act
        var result = await _service.ExtractPagedTextAsync(pdfPath);

        // Assert: Verify logging occurred
        Assert.True(result.Success);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Extracted") &&
                    v.ToString()!.Contains("page")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Scenario: Extraction failure logs error
    ///   Given a corrupted PDF
    ///   When extraction fails
    ///   Then error log should be written
    /// </summary>
    [Fact]
    public async Task ExtractPagedTextAsync_ExtractionFailure_LogsError()
    {
        // Arrange: Corrupted PDF
        var pdfPath = CreateTempPdfPath();
        File.WriteAllText(pdfPath, "Invalid PDF");

        // Act
        var result = await _service.ExtractPagedTextAsync(pdfPath);

        // Assert: Verify error logging
        Assert.False(result.Success);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ===== OCR Fallback Integration Tests =====

    /// <summary>
    /// Scenario: OCR fallback preserves page numbers
    ///   Given a low-quality PDF that triggers OCR
    ///   When OCR fallback is used
    ///   Then page numbers should still be accurate
    ///   And each page should be processed separately
    /// </summary>
    [Fact(Skip = "OUT OF SCOPE: Requires OCR paged implementation - future enhancement")]
    public async Task ExtractPagedTextAsync_OcrFallback_PreservesPageNumbers()
    {
        // TODO: Implement when paged OCR extraction is added (requires OcrExtractionResult, OcrPageResult, and ExtractPagedTextFromPdfAsync)
        await Task.CompletedTask;
    }
}
