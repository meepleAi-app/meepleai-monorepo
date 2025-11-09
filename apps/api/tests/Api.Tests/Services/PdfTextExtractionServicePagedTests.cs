using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Api.Tests.Services;

/// <summary>
/// BDD-style tests for page-aware PDF text extraction (AI-08)
/// These tests are written FIRST (RED phase) before implementation
/// </summary>
public class PdfTextExtractionServicePagedTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly Mock<ILogger<PdfTextExtractionService>> _loggerMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<IOcrService> _ocrServiceMock;
    private readonly PdfTextExtractionService _service;
    private readonly List<string> _tempFiles = new();

    public PdfTextExtractionServicePagedTests(ITestOutputHelper output)
    {
        _output = output;
        QuestPDF.Settings.License = LicenseType.Community;

        _loggerMock = new Mock<ILogger<PdfTextExtractionService>>();
        _configMock = new Mock<IConfiguration>();
        _ocrServiceMock = new Mock<IOcrService>();

        _service = new PdfTextExtractionService(
            _loggerMock.Object,
            _configMock.Object,
            _ocrServiceMock.Object
        );
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
        result.Error.Should().Match("*xtract*");  // Matches "Failed to extract"
        result.PageChunks.Should().BeEmpty();
        result.TotalPageCount.Should().Be(0);
    }

    /// <summary>
    /// BDD Scenario: PDF with blank pages is handled gracefully
    /// Given a PDF with 1 blank page (QuestPDF creates at least 1 page)
    /// When I call ExtractPagedTextAsync
    /// Then Success should be true
    /// And TotalPageCount should be 1
    /// And PageChunks should have 1 empty item
    /// </summary>
    // Note: QuestPDF creates at least 1 page for empty document
    [Fact]
    public async Task ExtractPagedTextAsync_EmptyPdf_HandledGracefully()
    {
        // Arrange - QuestPDF creates a 1-page PDF with blank content
        var blankPdfPath = CreateTestPdf(pageCount: 1, contentPerPage: new[] { "" });

        // Act
        var result = await _service.ExtractPagedTextAsync(blankPdfPath);

        // Assert
        result.Success.Should().BeTrue();
        result.TotalPageCount.Should().Be(1);
        result.PageChunks.Should().HaveCount(1);
        result.PageChunks[0].IsEmpty.Should().BeTrue();
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
        (result.PageChunks[0].Text.Length >= 2000).Should().BeTrue();
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
        result.Error.Should().Match("*ile path*");  // Matches "File path" or "file path"
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
        result.Error.Should().Contain("not found");
    }

    // Helper methods to create test PDFs
    private string CreateTestPdf(int pageCount, string[] contentPerPage)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"test_paged_{Guid.NewGuid()}.pdf");
        _tempFiles.Add(filePath);

        if (pageCount == 0)
        {
            // Create an empty PDF with no pages
            Document.Create(container => { }).GeneratePdf(filePath);
        }
        else
        {
            // Create a multi-page PDF with specified content
            Document.Create(container =>
            {
                for (int i = 0; i < pageCount && i < contentPerPage.Length; i++)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.Content().Text(contentPerPage[i]);
                    });
                }
            }).GeneratePdf(filePath);
        }

        return filePath;
    }

    private string CreateCorruptedPdf()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"test_corrupted_{Guid.NewGuid()}.pdf");
        _tempFiles.Add(filePath);

        // Write invalid PDF content
        File.WriteAllText(filePath, "This is not a valid PDF file at all");

        return filePath;
    }
}
