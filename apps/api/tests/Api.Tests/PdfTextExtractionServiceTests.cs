using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Xunit;

namespace Api.Tests;

public class PdfTextExtractionServiceTests : IDisposable
{
    private readonly Mock<ILogger<PdfTextExtractionService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly PdfTextExtractionService _service;
    private readonly List<string> _tempFiles = new();

    public PdfTextExtractionServiceTests()
    {
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
        Assert.False(result.Success);
        Assert.Equal("File path is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsFailure_WhenFilePathIsEmpty()
    {
        // Act
        var result = await _service.ExtractTextAsync(string.Empty);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("File path is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsFailure_WhenFilePathIsWhitespace()
    {
        // Act
        var result = await _service.ExtractTextAsync("   ");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("File path is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsFailure_WhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pdf");

        // Act
        var result = await _service.ExtractTextAsync(nonExistentPath);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("File not found", result.ErrorMessage);
    }

    // === Successful Extraction Tests ===

    [Fact]
    public async Task ExtractTextAsync_ExtractsTextSuccessfully_FromSimplePdf()
    {
        // Arrange
        var pdfPath = CreateTempPdfPath();
        var expectedText = "This is a test PDF document with simple text.";
        CreateSimplePdf(pdfPath, expectedText);

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Contains("test PDF document", result.ExtractedText);
        Assert.True(result.PageCount > 0);
        Assert.True(result.CharacterCount > 0);
    }

    [Fact]
    public async Task ExtractTextAsync_ExtractsTextFromMultiplePages()
    {
        // Arrange
        var pdfPath = CreateTempPdfPath();
        CreateMultiPagePdf(pdfPath,
            "Page one content",
            "Page two content",
            "Page three content");

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Page one", result.ExtractedText);
        Assert.Contains("Page two", result.ExtractedText);
        Assert.Contains("Page three", result.ExtractedText);
        Assert.True(result.PageCount >= 1); // At least one page detected
    }

    [Fact]
    public async Task ExtractTextAsync_NormalizesWhitespace()
    {
        // Arrange
        var pdfPath = CreateTempPdfPath();
        var contentWithExtraSpaces = "This  has   extra    spaces";
        CreateSimplePdf(pdfPath, contentWithExtraSpaces);

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        Assert.True(result.Success);
        // After normalization, multiple spaces should be reduced
        Assert.DoesNotContain("   ", result.ExtractedText);
    }

    [Fact]
    public async Task ExtractTextAsync_HandlesEmptyPdf()
    {
        // Arrange
        var pdfPath = CreateTempPdfPath();
        CreateSimplePdf(pdfPath, ""); // Empty content

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(string.Empty, result.ExtractedText);
        Assert.Equal(0, result.PageCount);
        Assert.Equal(0, result.CharacterCount);
    }

    [Fact]
    public async Task ExtractTextAsync_HandlesWhitespaceOnlyPdf()
    {
        // Arrange
        var pdfPath = CreateTempPdfPath();
        CreateSimplePdf(pdfPath, "   \n\n   \t  ");

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        Assert.True(result.Success);
        // Whitespace should be normalized away
        Assert.True(string.IsNullOrWhiteSpace(result.ExtractedText));
    }

    [Fact]
    public async Task ExtractTextAsync_CalculatesCorrectCharacterCount()
    {
        // Arrange
        var pdfPath = CreateTempPdfPath();
        var content = "Hello World";
        CreateSimplePdf(pdfPath, content);

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.CharacterCount > 0);
        Assert.Equal(result.ExtractedText.Length, result.CharacterCount);
    }

    [Fact]
    public async Task ExtractTextAsync_LogsWarning_WhenNoTextExtracted()
    {
        // Arrange
        var pdfPath = CreateTempPdfPath();
        CreateSimplePdf(pdfPath, "");

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        Assert.True(result.Success);
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
    public async Task ExtractTextAsync_LogsInformation_OnSuccessfulExtraction()
    {
        // Arrange
        var pdfPath = CreateTempPdfPath();
        CreateSimplePdf(pdfPath, "Test content");

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert
        Assert.True(result.Success);
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
        Assert.False(result.Success);
        Assert.Contains("Extraction failed", result.ErrorMessage);
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
        Assert.False(result.Success);
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
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("test text", result.ExtractedText);
        Assert.Equal(5, result.PageCount);
        Assert.Equal(100, result.CharacterCount);
    }

    [Fact]
    public void PdfTextExtractionResult_CreateFailure_SetsPropertiesCorrectly()
    {
        // Act
        var result = PdfTextExtractionResult.CreateFailure("test error");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("test error", result.ErrorMessage);
        Assert.Equal(string.Empty, result.ExtractedText);
        Assert.Equal(0, result.PageCount);
        Assert.Equal(0, result.CharacterCount);
    }

    [Fact]
    public void PdfTextExtractionResult_Success_HasNullErrorMessage()
    {
        // Act
        var result = PdfTextExtractionResult.CreateSuccess("content", 1, 7);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void PdfTextExtractionResult_Failure_HasZeroStats()
    {
        // Act
        var result = PdfTextExtractionResult.CreateFailure("error");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(0, result.PageCount);
        Assert.Equal(0, result.CharacterCount);
        Assert.Equal(string.Empty, result.ExtractedText);
    }
}
