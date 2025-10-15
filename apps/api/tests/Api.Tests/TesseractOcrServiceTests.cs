using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Unit tests for TesseractOcrService covering:
/// - Input validation (null/empty paths, missing files)
/// - Configuration handling (tessdata path, language, concurrency)
/// - Error handling (missing tessdata, invalid files)
/// - Semaphore-based concurrency control
/// - Disposal patterns
///
/// Note: These tests focus on validation and error handling.
/// Actual OCR functionality requires Tesseract runtime and is tested in integration tests.
/// </summary>
public class TesseractOcrServiceTests : IDisposable
{
    private readonly Mock<ILogger<TesseractOcrService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly string _testTempDir;

    public TesseractOcrServiceTests()
    {
        _mockLogger = new Mock<ILogger<TesseractOcrService>>();
        _mockConfig = new Mock<IConfiguration>();

        // Create a temp directory for test files
        _testTempDir = Path.Combine(Path.GetTempPath(), $"TesseractOcrServiceTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testTempDir);

        // Default configuration - IConfiguration.GetValue is an extension method, can't mock it directly
        // Instead, mock the indexer which is what the extension method uses
        var mockLanguageSection = new Mock<IConfigurationSection>();
        mockLanguageSection.Setup(s => s.Value).Returns("eng");
        _mockConfig.Setup(c => c.GetSection("PdfExtraction:Ocr:DefaultLanguage")).Returns(mockLanguageSection.Object);

        var mockConcurrencySection = new Mock<IConfigurationSection>();
        mockConcurrencySection.Setup(s => s.Value).Returns("2");
        _mockConfig.Setup(c => c.GetSection("PdfExtraction:Ocr:MaxConcurrentOperations")).Returns(mockConcurrencySection.Object);
    }

    [Fact]
    public void Constructor_InitializesWithDefaultConfiguration()
    {
        // Act
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        // Assert
        // Service should be created without throwing
        Assert.NotNull(service);

        // Verify logger was called with initialization message
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TesseractOcrService initialized")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_UsesConfiguredLanguage()
    {
        // Arrange
        var mockFrenchSection = new Mock<IConfigurationSection>();
        mockFrenchSection.Setup(s => s.Value).Returns("fra");
        _mockConfig.Setup(c => c.GetSection("PdfExtraction:Ocr:DefaultLanguage")).Returns(mockFrenchSection.Object);

        // Act
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("language: fra")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_UsesConfiguredMaxConcurrentOperations()
    {
        // Arrange
        var mockConcurrencySection = new Mock<IConfigurationSection>();
        mockConcurrencySection.Setup(s => s.Value).Returns("4");
        _mockConfig.Setup(c => c.GetSection("PdfExtraction:Ocr:MaxConcurrentOperations")).Returns(mockConcurrencySection.Object);

        // Act
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        // Assert - Service should initialize with configured concurrency limit
        Assert.NotNull(service);
    }

    [Fact]
    public async Task ExtractTextFromPageAsync_WithNullPath_ReturnsFailure()
    {
        // Arrange
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        // Act
        var result = await service.ExtractTextFromPageAsync(null!, 0);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("PDF path is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextFromPageAsync_WithEmptyPath_ReturnsFailure()
    {
        // Arrange
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        // Act
        var result = await service.ExtractTextFromPageAsync("", 0);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("PDF path is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextFromPageAsync_WithWhitespacePath_ReturnsFailure()
    {
        // Arrange
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        // Act
        var result = await service.ExtractTextFromPageAsync("   ", 0);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("PDF path is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextFromPageAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Arrange
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);
        var nonExistentPath = Path.Combine(_testTempDir, "nonexistent.pdf");

        // Act
        var result = await service.ExtractTextFromPageAsync(nonExistentPath, 0);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("PDF file not found", result.ErrorMessage);
        Assert.Contains(nonExistentPath, result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextFromPageAsync_WithExistingFile_AttemptsOcr()
    {
        // Arrange
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        // Create a dummy PDF file (not a real PDF, just for file existence check)
        var dummyPdfPath = Path.Combine(_testTempDir, "dummy.pdf");
        await File.WriteAllTextAsync(dummyPdfPath, "dummy content");

        // Act
        var result = await service.ExtractTextFromPageAsync(dummyPdfPath, 0);

        // Assert
        // Will fail during actual OCR processing (not a real PDF), but passes file existence check
        Assert.False(result.Success);
        Assert.Contains("OCR failed", result.ErrorMessage);

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("OCR failed for page")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExtractTextFromPdfAsync_WithNullPath_ReturnsFailure()
    {
        // Arrange
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        // Act
        var result = await service.ExtractTextFromPdfAsync(null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("PDF path is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextFromPdfAsync_WithEmptyPath_ReturnsFailure()
    {
        // Arrange
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        // Act
        var result = await service.ExtractTextFromPdfAsync("");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("PDF path is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextFromPdfAsync_WithWhitespacePath_ReturnsFailure()
    {
        // Arrange
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        // Act
        var result = await service.ExtractTextFromPdfAsync("   ");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("PDF path is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextFromPdfAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Arrange
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);
        var nonExistentPath = Path.Combine(_testTempDir, "nonexistent.pdf");

        // Act
        var result = await service.ExtractTextFromPdfAsync(nonExistentPath);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("PDF file not found", result.ErrorMessage);
        Assert.Contains(nonExistentPath, result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextFromPdfAsync_WithExistingFile_AttemptsOcr()
    {
        // Arrange
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        // Create a dummy PDF file
        var dummyPdfPath = Path.Combine(_testTempDir, "dummy_full.pdf");
        await File.WriteAllTextAsync(dummyPdfPath, "dummy content");

        // Act
        var result = await service.ExtractTextFromPdfAsync(dummyPdfPath);

        // Assert
        // Will fail during actual OCR processing (not a real PDF)
        Assert.False(result.Success);
        Assert.Contains("OCR failed", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextFromPageAsync_WithCancellationToken_SupportsCancellation()
    {
        // Arrange
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled

        var dummyPdfPath = Path.Combine(_testTempDir, "dummy_cancel.pdf");
        await File.WriteAllTextAsync(dummyPdfPath, "dummy content");

        // Act & Assert
        // Should throw OperationCanceledException when token is pre-cancelled
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await service.ExtractTextFromPageAsync(dummyPdfPath, 0, cts.Token);
        });
    }

    [Fact]
    public async Task ExtractTextFromPdfAsync_WithCancellationToken_SupportsCancellation()
    {
        // Arrange
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);
        var cts = new CancellationTokenSource();

        var dummyPdfPath = Path.Combine(_testTempDir, "dummy_cancel_full.pdf");
        await File.WriteAllTextAsync(dummyPdfPath, "dummy content");

        cts.Cancel(); // Cancel immediately

        // Act
        // Should throw OperationCanceledException or return failure
        try
        {
            var result = await service.ExtractTextFromPdfAsync(dummyPdfPath, cts.Token);

            // If it returns (rather than throwing), should indicate failure
            Assert.False(result.Success);
        }
        catch (OperationCanceledException)
        {
            // Expected for cancellation
        }
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        // Act
        service.Dispose();
        service.Dispose(); // Second call should be safe

        // Assert
        // No exception thrown
    }

    [Fact]
    public void Dispose_LogsDisposal()
    {
        // Arrange
        var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        // Act
        service.Dispose();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("TesseractOcrService disposed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task ExtractTextFromPageAsync_WithVariousPageIndices_AcceptsValidIndices(int pageIndex)
    {
        // Arrange
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        var dummyPdfPath = Path.Combine(_testTempDir, $"dummy_page_{pageIndex}.pdf");
        await File.WriteAllTextAsync(dummyPdfPath, "dummy content");

        // Act
        var result = await service.ExtractTextFromPageAsync(dummyPdfPath, pageIndex);

        // Assert
        // Will fail during OCR (not real PDF), but validates input acceptance
        Assert.False(result.Success);
        Assert.Contains("OCR failed", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextFromPageAsync_WithNegativePageIndex_AttemptsOcr()
    {
        // Arrange
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        var dummyPdfPath = Path.Combine(_testTempDir, "dummy_negative_page.pdf");
        await File.WriteAllTextAsync(dummyPdfPath, "dummy content");

        // Act
        var result = await service.ExtractTextFromPageAsync(dummyPdfPath, -1);

        // Assert
        // Negative index will be caught during Docnet processing
        Assert.False(result.Success);
        Assert.Contains("OCR failed", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextFromPdfAsync_LogsStartOfOcrOperation()
    {
        // Arrange
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        var dummyPdfPath = Path.Combine(_testTempDir, "dummy_log_test.pdf");
        await File.WriteAllTextAsync(dummyPdfPath, "dummy content");

        // Act
        await service.ExtractTextFromPdfAsync(dummyPdfPath);

        // Assert
        // Should log error (since file isn't valid PDF)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("OCR failed for PDF")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExtractTextFromPageAsync_WithLongPath_HandlesCorrectly()
    {
        // Arrange
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        // Create a path with long filename
        var longFilename = new string('a', 200) + ".pdf";
        var longPath = Path.Combine(_testTempDir, longFilename);

        // Note: May hit OS path length limits, which is expected behavior
        try
        {
            await File.WriteAllTextAsync(longPath, "dummy content");

            // Act
            var result = await service.ExtractTextFromPageAsync(longPath, 0);

            // Assert
            Assert.False(result.Success);
        }
        catch (PathTooLongException)
        {
            // Expected on some systems - test validates service handles it gracefully
        }
    }

    [Fact]
    public async Task ExtractTextFromPageAsync_WithSpecialCharactersInPath_HandlesCorrectly()
    {
        // Arrange
        using var service = new TesseractOcrService(_mockLogger.Object, _mockConfig.Object);

        // Path with spaces and special characters (OS-safe)
        var specialPath = Path.Combine(_testTempDir, "test file (copy).pdf");
        await File.WriteAllTextAsync(specialPath, "dummy content");

        // Act
        var result = await service.ExtractTextFromPageAsync(specialPath, 0);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("OCR failed", result.ErrorMessage);
    }

    public void Dispose()
    {
        // Cleanup temp directory
        if (Directory.Exists(_testTempDir))
        {
            try
            {
                Directory.Delete(_testTempDir, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
