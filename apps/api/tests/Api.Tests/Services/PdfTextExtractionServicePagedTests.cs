using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests.Services;

/// <summary>
/// BDD-style tests for page-aware PDF text extraction (AI-08)
/// These tests are written FIRST (RED phase) before implementation
/// </summary>
public class PdfTextExtractionServicePagedTests
{
    private readonly ITestOutputHelper _output;

    private readonly Mock<ILogger<PdfTextExtractionService>> _loggerMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<IOcrService> _ocrServiceMock;
    private readonly PdfTextExtractionService _service;

    public PdfTextExtractionServicePagedTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerMock = new Mock<ILogger<PdfTextExtractionService>>();
        _configMock = new Mock<IConfiguration>();
        _ocrServiceMock = new Mock<IOcrService>();

        _service = new PdfTextExtractionService(
            _loggerMock.Object,
            _configMock.Object,
            _ocrServiceMock.Object
        );
    }

    /// <summary>
    /// BDD Scenario: Extract text from multi-page PDF with accurate page numbers
    /// Given a PDF with 3 pages containing distinct content
    /// When I call ExtractPagedTextAsync
    /// Then Success should be true
    /// And TotalPageCount should be 3
    /// And PageChunks should contain 3 items with page numbers 1, 2, 3
    /// </summary>
    // Note: Requires test PDF generation - implement CreateTestPdf() helper
    [Fact]
    public async Task ExtractPagedTextAsync_MultiPagePdf_ReturnsAccuratePageNumbers()
    {
        // Arrange
        var testPdfPath = CreateTestPdf(pageCount: 3, contentPerPage: new[]
        {
            "Rules for Tic-Tac-Toe",
            "Setup: Draw a 3x3 grid",
            "Winning: Three in a row"
        });

        // Act
        var result = await _service.ExtractPagedTextAsync(testPdfPath);

        // Assert
        result.Success.Should().BeTrue();
        result.TotalPageCount.Should().Be(3);
        result.PageChunks.Count.Should().Be(3);

        result.PageChunks[0].PageNumber.Should().Be(1);
        result.PageChunks[0].Text.Should().Contain("Tic-Tac-Toe");

        result.PageChunks[1].PageNumber.Should().Be(2);
        result.PageChunks[1].Text.Should().Contain("3x3 grid");

        result.PageChunks[2].PageNumber.Should().Be(3);
        result.PageChunks[2].Text.Should().Contain("Three in a row");

        result.Error.Should().BeNull();
    }

    /// <summary>
    /// BDD Scenario: Handle empty pages gracefully
    /// Given a PDF where page 2 is blank
    /// When I extract paged text
    /// Then TotalPageCount should be 3
    /// And PageChunks should contain 3 items
    /// And PageChunks[1].IsEmpty should be true
    /// And PageChunks[1].PageNumber should be 2
    /// </summary>
    // Note: Requires test PDF generation - implement CreateTestPdf() helper
    [Fact]
    public async Task ExtractPagedTextAsync_EmptyPages_HandledGracefully()
    {
        // Arrange
        var testPdfPath = CreateTestPdf(pageCount: 3, contentPerPage: new[]
        {
            "Introduction",
            "",  // Empty page
            "Game Rules"
        });

        // Act
        var result = await _service.ExtractPagedTextAsync(testPdfPath);

        // Assert
        result.Success.Should().BeTrue();
        result.TotalPageCount.Should().Be(3);
        result.PageChunks.Count.Should().Be(3);

        result.PageChunks[0].IsEmpty.Should().BeFalse();
        result.PageChunks[0].PageNumber.Should().Be(1);

        result.PageChunks[1].IsEmpty.Should().BeTrue();
        result.PageChunks[1].PageNumber.Should().Be(2);

        result.PageChunks[2].IsEmpty.Should().BeFalse();
        result.PageChunks[2].PageNumber.Should().Be(3);
    }

    /// <summary>
    /// BDD Scenario: Extraction failure returns structured error
    /// Given a corrupted PDF file
    /// When I call ExtractPagedTextAsync
    /// Then Success should be false
    /// And Error should contain "PDF extraction failed"
    /// And PageChunks should be empty
    /// And TotalPageCount should be 0
    /// </summary>
    // Note: Requires test PDF generation - implement CreateCorruptedPdf() helper
    [Fact]
    public async Task ExtractPagedTextAsync_CorruptedPdf_ReturnsStructuredError()
    {
        // Arrange
        var corruptedPdfPath = CreateCorruptedPdf();

        // Act
        var result = await _service.ExtractPagedTextAsync(corruptedPdfPath);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error, StringComparison.OrdinalIgnoreCase.Should().Contain("PDF extraction failed");
        result.PageChunks.Should().BeEmpty();
        result.TotalPageCount.Should().Be(0);
    }

    /// <summary>
    /// BDD Scenario: Empty PDF (0 pages) is handled gracefully
    /// Given a PDF with 0 pages
    /// When I call ExtractPagedTextAsync
    /// Then Success should be true
    /// And TotalPageCount should be 0
    /// And PageChunks should be empty
    /// </summary>
    // Note: Requires test PDF generation - implement CreateTestPdf() helper
    [Fact]
    public async Task ExtractPagedTextAsync_EmptyPdf_HandledGracefully()
    {
        // Arrange
        var emptyPdfPath = CreateTestPdf(pageCount: 0, contentPerPage: Array.Empty<string>());

        // Act
        var result = await _service.ExtractPagedTextAsync(emptyPdfPath);

        // Assert
        result.Success.Should().BeTrue();
        result.TotalPageCount.Should().Be(0);
        result.PageChunks.Should().BeEmpty();
        result.Error.Should().BeNull();
    }

    /// <summary>
    /// BDD Scenario: Large page produces multiple chunks
    /// Given a page with 2000 characters of continuous text
    /// When I extract the page
    /// Then the page should be captured as a single PagedTextChunk
    /// And PageNumber should be accurate
    /// Note: Actual chunking to 512-char pieces happens in TextChunkingService
    /// </summary>
    // Note: Requires test PDF generation - implement CreateTestPdf() helper
    [Fact]
    public async Task ExtractPagedTextAsync_LargePage_CapturedCorrectly()
    {
        // Arrange
        var largeContent = new string('X', 2000);  // 2000 characters
        var testPdfPath = CreateTestPdf(pageCount: 1, contentPerPage: new[] { largeContent });

        // Act
        var result = await _service.ExtractPagedTextAsync(testPdfPath);

        // Assert
        result.Success.Should().BeTrue();
        result.TotalPageCount.Should().Be(1);
        result.PageChunks.Should().ContainSingle();
        result.PageChunks[0].PageNumber.Should().Be(1);
        result.PageChunks[0].Text.Length >= 2000.Should().BeTrue();
    }

    /// <summary>
    /// BDD Scenario: Very small page is processed correctly
    /// Given a page with only "Checkmate." (10 characters)
    /// When I extract the page
    /// Then 1 PagedTextChunk should be created
    /// And the chunk should have the correct page number
    /// </summary>
    // Note: Requires test PDF generation - implement CreateTestPdf() helper
    [Fact]
    public async Task ExtractPagedTextAsync_SmallPage_ProcessedCorrectly()
    {
        // Arrange
        var testPdfPath = CreateTestPdf(pageCount: 1, contentPerPage: new[] { "Checkmate." });

        // Act
        var result = await _service.ExtractPagedTextAsync(testPdfPath);

        // Assert
        result.Success.Should().BeTrue();
        result.TotalPageCount.Should().Be(1);
        result.PageChunks.Should().ContainSingle();
        result.PageChunks[0].PageNumber.Should().Be(1);
        result.PageChunks[0].Text.Should().Contain("Checkmate");
    }

    /// <summary>
    /// BDD Scenario: Null file path returns structured error
    /// Given a null file path
    /// When I call ExtractPagedTextAsync
    /// Then Success should be false
    /// And Error should contain "file path is required"
    /// </summary>
    [Fact]
    public async Task ExtractPagedTextAsync_NullFilePath_ReturnsError()
    {
        // Arrange
        string? nullPath = null;

        // Act
        var result = await _service.ExtractPagedTextAsync(nullPath!);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error, StringComparison.OrdinalIgnoreCase.Should().Contain("file path");
    }

    /// <summary>
    /// BDD Scenario: Non-existent file path returns structured error
    /// Given a file path that doesn't exist
    /// When I call ExtractPagedTextAsync
    /// Then Success should be false
    /// And Error should contain "file not found"
    /// </summary>
    [Fact]
    public async Task ExtractPagedTextAsync_NonExistentFile_ReturnsError()
    {
        // Arrange
        var nonExistentPath = "C:\\DoesNotExist\\fake.pdf";

        // Act
        var result = await _service.ExtractPagedTextAsync(nonExistentPath);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error, StringComparison.OrdinalIgnoreCase.Should().Contain("not found");
    }

    // Helper methods to create test PDFs
    // Note: In real implementation, use a library like QuestPDF or iText7 to generate PDFs
    // For now, these are placeholders that will need real PDF generation logic

    private string CreateTestPdf(int pageCount, string[] contentPerPage)
    {
        // TODO: Implement real PDF generation using QuestPDF or similar
        // This is a placeholder - actual implementation needed
        throw new NotImplementedException("Test PDF generation not implemented - GREEN phase task");
    }

    private string CreateCorruptedPdf()
    {
        // TODO: Create a file with .pdf extension but invalid content
        throw new NotImplementedException("Corrupted PDF generation not implemented - GREEN phase task");
    }
}
