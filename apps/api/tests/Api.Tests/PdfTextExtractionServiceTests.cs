using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Runtime.InteropServices;
using Xunit;
using FluentAssertions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Tests for PDF text extraction service using Docnet.Core.
///
/// NOTE: Some tests require native PDF libraries:
/// - Linux: libgdiplus (installed in CI via .github/workflows/ci.yml)
/// - Windows: May require additional native dependencies
///
/// Tests that depend on PDF rendering are skipped on Windows to avoid local dev failures.
/// These tests pass in CI (Linux) where proper dependencies are installed.
/// </summary>
public class PdfTextExtractionServiceTests : IDisposable
{
    private readonly Mock<ILogger<PdfTextExtractionService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly PdfTextExtractionService _service;
    private readonly List<string> _tempFiles = new();
    private readonly ITestOutputHelper _output;

    public PdfTextExtractionServiceTests(ITestOutputHelper output)
    {
        _output = output;

        // Configure QuestPDF for testing (community license)
        QuestPDF.Settings.License = LicenseType.Community;

        _loggerMock = new Mock<ILogger<PdfTextExtractionService>>();
        _configurationMock = new Mock<IConfiguration>();

        // Default: no OCR service (testing standard extraction only)
        _service = new PdfTextExtractionService(_loggerMock.Object, _configurationMock.Object, ocrService: null);
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
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.pdf");
        _tempFiles.Add(path);
        return path;
    }

    private void CreateSimplePdf(string filePath, string content)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Text(content);
            });
        }).GeneratePdf(filePath);
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

    // === Validation Tests ===

    [Fact]
    public async Task ExtractTextAsync_ReturnsFailure_WhenFilePathIsNull()
    {
        // Act
        var result = await _service.ExtractTextAsync(null!);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().BeEquivalentTo("File path is required");
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsFailure_WhenFilePathIsEmpty()
    {
        // Act
        var result = await _service.ExtractTextAsync(string.Empty);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().BeEquivalentTo("File path is required");
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsFailure_WhenFilePathIsWhitespace()
    {
        // Act
        var result = await _service.ExtractTextAsync("   ");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().BeEquivalentTo("File path is required");
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsFailure_WhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pdf");

        // Act
        var result = await _service.ExtractTextAsync(nonExistentPath);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("File not found");
    }

    // === Successful Extraction Tests ===

    [Fact]
    [Trait("Category", "RequiresNativeLibraries")]
    public async Task ExtractTextAsync_ExtractsTextSuccessfully_FromSimplePdf()
    {
        // Skip on Windows if libgdiplus not available (Linux CI has it)
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _output.WriteLine("Skipping on non-Linux platform (requires libgdiplus)");
            return;
        }

        // Arrange
        var pdfPath = CreateTempPdfPath();
        var expectedText = "This is a test PDF document with simple text.";
        CreateSimplePdf(pdfPath, expectedText);

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.ExtractedText.Should().Contain("test PDF document");
        (result.PageCount > 0).Should().BeTrue();
        (result.CharacterCount > 0).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "RequiresNativeLibraries")]
    public async Task ExtractTextAsync_ExtractsTextFromMultiplePages()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _output.WriteLine("Skipping on non-Linux platform (requires libgdiplus)");
            return;
        }

        // Arrange
        var pdfPath = CreateTempPdfPath();
        CreateMultiPagePdf(pdfPath,
            "Page one content",
            "Page two content",
            "Page three content");

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        result.Success.Should().BeTrue();
        result.ExtractedText.Should().Contain("Page one");
        result.ExtractedText.Should().Contain("Page two");
        result.ExtractedText.Should().Contain("Page three");
        (result.PageCount >= 1).Should().BeTrue(); // At least one page detected
    }

    [Fact]
    [Trait("Category", "RequiresNativeLibraries")]
    public async Task ExtractTextAsync_NormalizesWhitespace()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _output.WriteLine("Skipping on non-Linux platform (requires libgdiplus)");
            return;
        }

        // Arrange
        var pdfPath = CreateTempPdfPath();
        var contentWithExtraSpaces = "This  has   extra    spaces";
        CreateSimplePdf(pdfPath, contentWithExtraSpaces);

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        result.Success.Should().BeTrue();
        // After normalization, multiple spaces should be reduced
        result.ExtractedText.Should().NotContain("   ");
    }

    [Fact]
    [Trait("Category", "RequiresNativeLibraries")]
    public async Task ExtractTextAsync_HandlesEmptyPdf()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _output.WriteLine("Skipping on non-Linux platform (requires libgdiplus)");
            return;
        }

        // Arrange
        var pdfPath = CreateTempPdfPath();
        CreateSimplePdf(pdfPath, ""); // Empty content

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        result.Success.Should().BeTrue();
        result.ExtractedText.Should().BeEquivalentTo(string.Empty);
        result.PageCount.Should().Be(0);
        result.CharacterCount.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "RequiresNativeLibraries")]
    public async Task ExtractTextAsync_HandlesWhitespaceOnlyPdf()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _output.WriteLine("Skipping on non-Linux platform (requires libgdiplus)");
            return;
        }

        // Arrange
        var pdfPath = CreateTempPdfPath();
        CreateSimplePdf(pdfPath, "   \n\n   \t  ");

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        result.Success.Should().BeTrue();
        // Whitespace should be normalized away
        string.IsNullOrWhiteSpace(result.ExtractedText).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "RequiresNativeLibraries")]
    public async Task ExtractTextAsync_CalculatesCorrectCharacterCount()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _output.WriteLine("Skipping on non-Linux platform (requires libgdiplus)");
            return;
        }

        // Arrange
        var pdfPath = CreateTempPdfPath();
        var content = "Hello World";
        CreateSimplePdf(pdfPath, content);

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        result.Success.Should().BeTrue();
        (result.CharacterCount > 0).Should().BeTrue();
        result.CharacterCount.Should().Be(result.ExtractedText.Length);
    }

    [Fact]
    [Trait("Category", "RequiresNativeLibraries")]
    public async Task ExtractTextAsync_LogsWarning_WhenNoTextExtracted()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _output.WriteLine("Skipping on non-Linux platform (requires libgdiplus)");
            return;
        }

        // Arrange
        var pdfPath = CreateTempPdfPath();
        CreateSimplePdf(pdfPath, "");

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        result.Success.Should().BeTrue();
        // Verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No text extracted")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "RequiresNativeLibraries")]
    public async Task ExtractTextAsync_LogsInformation_OnSuccessfulExtraction()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _output.WriteLine("Skipping on non-Linux platform (requires libgdiplus)");
            return;
        }

        // Arrange
        var pdfPath = CreateTempPdfPath();
        CreateSimplePdf(pdfPath, "Test content");

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        result.Success.Should().BeTrue();
        // Verify info log was written
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Extracted text from PDF")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // === Error Handling Tests ===

    [Fact]
    public async Task ExtractTextAsync_ReturnsFailure_WhenFileIsCorrupted()
    {
        // Arrange
        var pdfPath = CreateTempPdfPath();
        File.WriteAllText(pdfPath, "This is not a valid PDF file");

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to extract text from PDF");
    }

    [Fact]
    public async Task ExtractTextAsync_LogsError_WhenExtractionFails()
    {
        // Arrange
        var pdfPath = CreateTempPdfPath();
        File.WriteAllText(pdfPath, "Invalid PDF");

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        result.Success.Should().BeFalse();
        // Verify error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to extract text")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // === Result Factory Method Tests ===

    [Fact]
    public void PdfTextExtractionResult_CreateSuccess_SetsPropertiesCorrectly()
    {
        // Act
        var result = PdfTextExtractionResult.CreateSuccess("test text", 5, 100);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.ExtractedText.Should().BeEquivalentTo("test text");
        result.PageCount.Should().Be(5);
        result.CharacterCount.Should().Be(100);
    }

    [Fact]
    public void PdfTextExtractionResult_CreateFailure_SetsPropertiesCorrectly()
    {
        // Act
        var result = PdfTextExtractionResult.CreateFailure("test error");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().BeEquivalentTo("test error");
        result.ExtractedText.Should().BeEquivalentTo(string.Empty);
        result.PageCount.Should().Be(0);
        result.CharacterCount.Should().Be(0);
    }

    [Fact]
    public void PdfTextExtractionResult_Success_HasNullErrorMessage()
    {
        // Act
        var result = PdfTextExtractionResult.CreateSuccess("content", 1, 7);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void PdfTextExtractionResult_Failure_HasZeroStats()
    {
        // Act
        var result = PdfTextExtractionResult.CreateFailure("error");

        // Assert
        result.Success.Should().BeFalse();
        result.PageCount.Should().Be(0);
        result.CharacterCount.Should().Be(0);
        result.ExtractedText.Should().BeEquivalentTo(string.Empty);
    }
}
