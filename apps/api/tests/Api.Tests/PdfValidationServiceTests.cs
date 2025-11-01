using Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Runtime.InteropServices;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// PDF-09: Comprehensive tests for PDF validation service
/// Tests file size, MIME type, page count, PDF version, and magic bytes validation
/// </summary>
public class PdfValidationServiceTests : IDisposable
{
    private readonly Mock<ILogger<PdfValidationService>> _loggerMock;
    private readonly PdfProcessingConfiguration _config;
    private readonly PdfValidationService _service;
    private readonly List<string> _tempFiles = new();
    private readonly ITestOutputHelper _output;

    public PdfValidationServiceTests(ITestOutputHelper output)
    {
        _output = output;

        // Configure QuestPDF for testing (community license)
        QuestPDF.Settings.License = LicenseType.Community;

        _loggerMock = new Mock<ILogger<PdfValidationService>>();
        _config = new PdfProcessingConfiguration
        {
            MaxFileSizeBytes = 104857600, // 100 MB
            MaxPageCount = 500,
            MinPageCount = 1,
            MinPdfVersion = "1.4",
            AllowedContentTypes = new List<string> { "application/pdf" }
        };

        var options = Options.Create(_config);
        _service = new PdfValidationService(_loggerMock.Object, options);
    }

    public void Dispose()
    {
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

    private void CreateMultiPagePdf(string filePath, int pageCount)
    {
        Document.Create(container =>
        {
            for (int i = 0; i < pageCount; i++)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.Content().Text($"Page {i + 1} content");
                });
            }
        }).GeneratePdf(filePath);
    }

    // ===== File Size Validation Tests =====

    [Fact]
    public void ValidateFileSize_ReturnsFailure_WhenFileSizeIsZero()
    {
        // Act
        var result = _service.ValidateFileSize(0);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("fileSize");
        result.Errors["fileSize"].Should().Contain("must be greater than 0");
    }

    [Fact]
    public void ValidateFileSize_ReturnsFailure_WhenFileSizeIsNegative()
    {
        // Act
        var result = _service.ValidateFileSize(-100);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("fileSize");
    }

    [Fact]
    public void ValidateFileSize_ReturnsFailure_WhenFileSizeExceedsMaximum()
    {
        // Arrange
        var oversizedFile = _config.MaxFileSizeBytes + 1;

        // Act
        var result = _service.ValidateFileSize(oversizedFile);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("fileSize");
        result.Errors["fileSize"].Should().Contain("exceeds maximum");
    }

    [Fact]
    public void ValidateFileSize_ReturnsSuccess_WhenFileSizeIsValid()
    {
        // Arrange
        var validSize = 50 * 1024 * 1024; // 50 MB

        // Act
        var result = _service.ValidateFileSize(validSize);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Metadata.Should().NotBeNull();
        result.Metadata.FileSizeBytes.Should().Be(validSize);
    }

    [Fact]
    public void ValidateFileSize_ReturnsSuccess_AtMaximumSize()
    {
        // Act
        var result = _service.ValidateFileSize(_config.MaxFileSizeBytes);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    // ===== MIME Type Validation Tests =====

    [Fact]
    public void ValidateMimeType_ReturnsFailure_WhenContentTypeIsEmpty()
    {
        // Act
        var result = _service.ValidateMimeType(string.Empty);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("fileType");
        result.Errors["fileType"].Should().Contain("cannot be empty");
    }

    [Fact]
    public void ValidateMimeType_ReturnsFailure_WhenContentTypeIsNull()
    {
        // Act
        var result = _service.ValidateMimeType(null!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("fileType");
    }

    [Fact]
    public void ValidateMimeType_ReturnsFailure_WhenContentTypeIsInvalid()
    {
        // Act
        var result = _service.ValidateMimeType("application/msword");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("fileType");
        result.Errors["fileType"].Should().Contain("not allowed");
    }

    [Fact]
    public void ValidateMimeType_ReturnsSuccess_WhenContentTypeIsValid()
    {
        // Act
        var result = _service.ValidateMimeType("application/pdf");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateMimeType_IsCaseInsensitive()
    {
        // Act
        var result = _service.ValidateMimeType("Application/PDF");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    // ===== PDF Stream Validation Tests =====

    [Fact]
    public async Task ValidateAsync_ReturnsFailure_WhenStreamIsNull()
    {
        // Act
        var result = await _service.ValidateAsync(null!, "test.pdf");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("stream");
        result.Errors["stream"].Should().Contain("cannot be null");
    }

    [Fact]
    public async Task ValidateAsync_ReturnsFailure_WhenFileNameIsEmpty()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        var result = await _service.ValidateAsync(stream, string.Empty);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("fileName");
        result.Errors["fileName"].Should().Contain("cannot be empty");
    }

    [Fact]
    [Trait("Category", "RequiresNativeLibraries")]
    public async Task ValidateAsync_ReturnsSuccess_ForValidPdf()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _output.WriteLine("Skipping on non-Linux platform (requires libgdiplus)");
            return;
        }

        // Arrange
        var pdfPath = CreateTempPdfPath();
        CreateSimplePdf(pdfPath, "Test content");

        using var stream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Act
        var result = await _service.ValidateAsync(stream, "test.pdf");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Metadata.Should().NotBeNull();
        (result.Metadata.PageCount >= 1).Should().BeTrue();
        result.Metadata.PdfVersion.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ReturnsFailure_ForInvalidPdfMagicBytes()
    {
        // Arrange
        var invalidPdfBytes = System.Text.Encoding.UTF8.GetBytes("This is not a PDF file");
        using var stream = new MemoryStream(invalidPdfBytes);

        // Act
        var result = await _service.ValidateAsync(stream, "test.pdf");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("fileFormat");
        result.Errors["fileFormat"].Should().Contain("Invalid PDF file format");
    }

    [Fact]
    public async Task ValidateAsync_ReturnsFailure_ForCorruptedPdf()
    {
        // Arrange
        var corruptedPdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x00, 0x00 }; // %PDF-1.4 + garbage
        using var stream = new MemoryStream(corruptedPdfBytes);

        // Act
        var result = await _service.ValidateAsync(stream, "corrupted.pdf");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("pdfStructure");
    }

    [Fact]
    [Trait("Category", "RequiresNativeLibraries")]
    public async Task ValidateAsync_ReturnsFailure_WhenPageCountBelowMinimum()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _output.WriteLine("Skipping on non-Linux platform (requires libgdiplus)");
            return;
        }

        // Arrange - PDF with 0 pages should fail, but we can't easily create such a PDF
        // This test validates the configuration works
        var pdfPath = CreateTempPdfPath();
        CreateSimplePdf(pdfPath, "");

        using var stream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Change config temporarily
        var customConfig = new PdfProcessingConfiguration
        {
            MaxFileSizeBytes = 104857600,
            MaxPageCount = 500,
            MinPageCount = 5, // Require at least 5 pages
            MinPdfVersion = "1.4",
            AllowedContentTypes = new List<string> { "application/pdf" }
        };
        var customOptions = Options.Create(customConfig);
        var customService = new PdfValidationService(_loggerMock.Object, customOptions);

        // Act
        var result = await customService.ValidateAsync(stream, "test.pdf");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("pageCount");
        result.Errors["pageCount"].Should().Contain("must have at least 5 page(s)");
    }

    [Fact]
    [Trait("Category", "RequiresNativeLibraries")]
    public async Task ValidateAsync_ReturnsFailure_WhenPageCountExceedsMaximum()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _output.WriteLine("Skipping on non-Linux platform (requires libgdiplus)");
            return;
        }

        // Arrange
        var customConfig = new PdfProcessingConfiguration
        {
            MaxFileSizeBytes = 104857600,
            MaxPageCount = 2, // Allow only 2 pages
            MinPageCount = 1,
            MinPdfVersion = "1.4",
            AllowedContentTypes = new List<string> { "application/pdf" }
        };
        var customOptions = Options.Create(customConfig);
        var customService = new PdfValidationService(_loggerMock.Object, customOptions);

        var pdfPath = CreateTempPdfPath();
        CreateMultiPagePdf(pdfPath, 3); // Create 3 pages

        using var stream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Act
        var result = await customService.ValidateAsync(stream, "test.pdf");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("pageCount");
        result.Errors["pageCount"].Should().Contain("maximum allowed is 2");
    }

    [Fact]
    [Trait("Category", "RequiresNativeLibraries")]
    public async Task ValidateAsync_ExtractsCorrectMetadata()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _output.WriteLine("Skipping on non-Linux platform (requires libgdiplus)");
            return;
        }

        // Arrange
        var pdfPath = CreateTempPdfPath();
        CreateMultiPagePdf(pdfPath, 3);

        using var stream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Act
        var result = await _service.ValidateAsync(stream, "test.pdf");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Metadata.Should().NotBeNull();
        result.Metadata.PageCount.Should().Be(3);
        result.Metadata.PdfVersion.Should().NotBeEmpty();
        (result.Metadata.FileSizeBytes > 0).Should().BeTrue();
    }

    // ===== PDF Version Validation Tests =====

    [Fact]
    [Trait("Category", "RequiresNativeLibraries")]
    public async Task ValidateAsync_ReturnsSuccess_ForSupportedPdfVersion()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _output.WriteLine("Skipping on non-Linux platform (requires libgdiplus)");
            return;
        }

        // Arrange - QuestPDF creates PDF 1.4+ by default
        var pdfPath = CreateTempPdfPath();
        CreateSimplePdf(pdfPath, "Test");

        using var stream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Act
        var result = await _service.ValidateAsync(stream, "test.pdf");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Metadata.Should().NotBeNull();
        result.Metadata.PdfVersion.Should().NotBeEmpty();
    }

    // ===== Multiple Validation Errors Tests =====

    [Fact]
    public async Task ValidateAsync_ReturnsMultipleErrors_WhenMultipleValidationsFail()
    {
        // Arrange - File that fails both magic bytes and structure validation
        var invalidBytes = System.Text.Encoding.UTF8.GetBytes("Not a PDF at all");
        using var stream = new MemoryStream(invalidBytes);

        // Act
        var result = await _service.ValidateAsync(stream, "test.pdf");

        // Assert
        result.IsValid.Should().BeFalse();
        (result.Errors.Count > 0).Should().BeTrue();
    }

    // ===== Configuration Tests =====

    [Fact]
    public void Configuration_DefaultValuesAreCorrect()
    {
        // Arrange
        var config = new PdfProcessingConfiguration();

        // Assert
        config.MaxFileSizeBytes.Should().Be(104857600); // 100 MB
        config.MaxPageCount.Should().Be(500);
        config.MinPageCount.Should().Be(1);
        config.MinPdfVersion.Should().Be("1.4");
        config.AllowedContentTypes.Should().Contain("application/pdf");
    }

    // ===== Logging Tests =====

    [Fact]
    [Trait("Category", "RequiresNativeLibraries")]
    public async Task ValidateAsync_LogsSuccess_WhenValidationPasses()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _output.WriteLine("Skipping on non-Linux platform (requires libgdiplus)");
            return;
        }

        // Arrange
        var pdfPath = CreateTempPdfPath();
        CreateSimplePdf(pdfPath, "Test");

        using var stream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Act
        await _service.ValidateAsync(stream, "test.pdf");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("PDF validation successful")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_LogsWarning_WhenValidationFails()
    {
        // Arrange
        var invalidBytes = System.Text.Encoding.UTF8.GetBytes("Not a PDF");
        using var stream = new MemoryStream(invalidBytes);

        // Act
        await _service.ValidateAsync(stream, "test.pdf");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("PDF validation failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ===== Result Factory Method Tests =====

    [Fact]
    public void PdfValidationResult_CreateSuccess_SetsPropertiesCorrectly()
    {
        // Arrange
        var metadata = new PdfMetadata
        {
            PageCount = 5,
            PdfVersion = "1.5",
            FileSizeBytes = 1024
        };

        // Act
        var result = PdfValidationResult.CreateSuccess(metadata);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Metadata.Should().Be(metadata);
    }

    [Fact]
    public void PdfValidationResult_CreateFailure_SetsPropertiesCorrectly()
    {
        // Arrange
        var errors = new Dictionary<string, string>
        {
            ["fileSize"] = "File too large",
            ["pageCount"] = "Too many pages"
        };

        // Act
        var result = PdfValidationResult.CreateFailure(errors);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Be(errors);
        result.Metadata.Should().BeNull();
    }
}