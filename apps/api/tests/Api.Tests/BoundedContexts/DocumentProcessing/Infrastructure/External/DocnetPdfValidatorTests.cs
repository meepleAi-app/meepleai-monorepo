using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// Tests for DocnetPdfValidator adapter.
/// Tests both technical validation (magic bytes, Docnet parsing) and business rule delegation.
/// </summary>
public class DocnetPdfValidatorTests : IDisposable
{
    private readonly Mock<ILogger<DocnetPdfValidator>> _mockLogger;
    private readonly PdfValidationDomainService _domainService;
    private readonly DocnetPdfValidator _validator;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public DocnetPdfValidatorTests()
    {
        _mockLogger = new Mock<ILogger<DocnetPdfValidator>>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pdf:MaxFileSizeMb"] = "50",
                ["Pdf:MinPageCount"] = "1",
                ["Pdf:MaxPageCount"] = "500",
                ["Pdf:MinVersion"] = "1.0",
                ["Pdf:AllowedContentTypes:0"] = "application/pdf",
                ["Pdf:AllowedContentTypes:1"] = "application/x-pdf"
            })
            .Build();

        _domainService = new PdfValidationDomainService(configuration);
        _validator = new DocnetPdfValidator(_mockLogger.Object, _domainService);
    }

    #region Precondition Tests

    [Fact]
    public async Task ValidateAsync_NullStream_ReturnsFailure()
    {
        // Arrange
        Stream? stream = null;

        // Act
        var result = await _validator.ValidateAsync(stream!, "test.pdf", TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("stream", result.Errors.Keys);
        Assert.Contains("cannot be null", result.Errors["stream"]);
    }

    [Fact]
    public async Task ValidateAsync_EmptyFileName_ReturnsFailure()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        var result = await _validator.ValidateAsync(stream, "", TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("fileName", result.Errors.Keys);
        Assert.Contains("cannot be empty", result.Errors["fileName"]);
    }

    [Fact]
    public async Task ValidateAsync_WhitespaceFileName_ReturnsFailure()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        var result = await _validator.ValidateAsync(stream, "   ", TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("fileName", result.Errors.Keys);
    }

    #endregion

    #region Magic Bytes Validation Tests

    [Fact]
    public async Task ValidateAsync_ValidMagicBytes_PassesMagicBytesCheck()
    {
        // Arrange: Create stream with PDF magic bytes (%PDF-)
        var pdfMagicBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // %PDF-1.4
        using var stream = new MemoryStream(pdfMagicBytes);

        // Act
        var result = await _validator.ValidateAsync(stream, "test.pdf", TestCancellationToken);

        // Assert: Should not fail on magic bytes (may fail on other checks like file size)
        if (result.Errors.ContainsKey("fileFormat"))
        {
            Assert.DoesNotContain("PDF signature", result.Errors["fileFormat"]);
        }
    }

    [Fact]
    public async Task ValidateAsync_InvalidMagicBytes_ReturnsFailure()
    {
        // Arrange: Stream with wrong magic bytes
        var invalidBytes = new byte[] { 0x00, 0x00, 0x00, 0x00 };
        using var stream = new MemoryStream(invalidBytes);

        // Act
        var result = await _validator.ValidateAsync(stream, "test.pdf", TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("fileFormat", result.Errors.Keys);
        Assert.Contains("PDF signature", result.Errors["fileFormat"]);
    }

    [Fact]
    public async Task ValidateAsync_EmptyStream_ReturnsFailure()
    {
        // Arrange: Empty stream
        using var stream = new MemoryStream();

        // Act
        var result = await _validator.ValidateAsync(stream, "test.pdf", TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("fileFormat", result.Errors.Keys);
    }

    #endregion

    #region File Size Validation Tests

    [Fact]
    public async Task ValidateAsync_ValidFileSize_PassesFileSizeCheck()
    {
        // Arrange: 1 MB file with PDF magic bytes
        var data = CreatePdfLikeBytes(1024 * 1024); // 1 MB
        using var stream = new MemoryStream(data);

        // Act
        var result = await _validator.ValidateAsync(stream, "test.pdf", TestCancellationToken);

        // Assert: Should not fail on file size
        Assert.DoesNotContain("fileSize", result.Errors.Keys);
    }

    [Fact]
    public async Task ValidateAsync_OversizedFile_ReturnsFailure()
    {
        // Arrange: 51 MB file (over 50 MB limit)
        var data = CreatePdfLikeBytes(51 * 1024 * 1024); // 51 MB
        using var stream = new MemoryStream(data);

        // Act
        var result = await _validator.ValidateAsync(stream, "test.pdf", TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("fileSize", result.Errors.Keys);
        Assert.Contains("exceeds maximum", result.Errors["fileSize"]);
    }

    #endregion

    #region MIME Type Validation Tests

    [Fact]
    public async Task ValidateAsync_ValidMimeType_PassesMimeTypeCheck()
    {
        // Arrange: PDF file with .pdf extension
        var data = CreatePdfLikeBytes(1024);
        using var stream = new MemoryStream(data);

        // Act
        var result = await _validator.ValidateAsync(stream, "document.pdf", TestCancellationToken);

        // Assert: Should not fail on MIME type
        Assert.DoesNotContain("fileType", result.Errors.Keys);
    }

    [Fact]
    public async Task ValidateAsync_NonPdfExtension_ReturnsFailure()
    {
        // Arrange: File without .pdf extension
        var data = CreatePdfLikeBytes(1024);
        using var stream = new MemoryStream(data);

        // Act
        var result = await _validator.ValidateAsync(stream, "document.txt", TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        // Note: MIME type check delegates to domain service which checks content type
        // GetContentType() returns "application/octet-stream" for non-.pdf files
    }

    #endregion

    #region Integration Tests (multiple validation failures)

    [Fact]
    public async Task ValidateAsync_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange: Invalid magic bytes + oversized file
        var invalidData = new byte[51 * 1024 * 1024]; // 51 MB of zeros (not PDF)
        using var stream = new MemoryStream(invalidData);

        // Act
        var result = await _validator.ValidateAsync(stream, "test.pdf", TestCancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 2); // At least magic bytes + file size errors
        Assert.Contains("fileFormat", result.Errors.Keys);
        Assert.Contains("fileSize", result.Errors.Keys);
    }

    #endregion

    #region ExtractMetadataAsync Tests

    [Fact]
    public async Task ExtractMetadataAsync_NullStream_ReturnsNull()
    {
        // Arrange
        Stream? stream = null;

        // Act
        var metadata = await _validator.ExtractMetadataAsync(stream!, TestCancellationToken);

        // Assert
        Assert.Null(metadata);
    }

    [Fact]
    public async Task ExtractMetadataAsync_InvalidPdf_ReturnsNull()
    {
        // Arrange: Non-PDF data
        using var stream = new MemoryStream(new byte[] { 0x00, 0x00, 0x00 });

        // Act
        var metadata = await _validator.ExtractMetadataAsync(stream, TestCancellationToken);

        // Assert: Should return null for invalid PDF (Docnet parsing fails)
        Assert.Null(metadata);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a byte array with PDF magic bytes followed by zeros.
    /// Note: This is NOT a valid PDF, just has correct magic bytes for testing.
    /// </summary>
    private static byte[] CreatePdfLikeBytes(int size)
    {
        var data = new byte[size];
        var pdfMagicBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }; // %PDF-
        Array.Copy(pdfMagicBytes, data, Math.Min(pdfMagicBytes.Length, size));
        return data;
    }

    #endregion

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}

