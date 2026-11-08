using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Docnet.Core;
using Docnet.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using QuestPDF.Fluent;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// Tests for DocnetPdfValidator adapter.
/// ISSUE-1818: Migrated to FluentAssertions for improved readability.
/// Tests both technical validation (magic bytes, Docnet parsing) and business rule delegation.
/// ISSUE-1818: Migrated to FluentAssertions for improved readability.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class DocnetPdfValidatorTests : IDisposable
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
                ["Pdf:MinPageCount"] = "1",
                ["Pdf:MaxPageCount"] = "500",
                ["Pdf:MinVersion"] = "1.0",
                ["Pdf:AllowedContentTypes:0"] = "application/pdf",
                ["Pdf:AllowedContentTypes:1"] = "application/x-pdf"
            })
            .Build();

        _domainService = new PdfValidationDomainService(configuration);
        // #4449: the size limit comes from PdfProcessingOptions. 50 MB here, below the real 100, keeps
        // the over-limit buffers of these tests at 51 MB.
        _validator = new DocnetPdfValidator(
            _mockLogger.Object,
            _domainService,
            Options.Create(new PdfProcessingOptions { MaxFileSizeBytes = 50L * 1024 * 1024 }));
    }
    [Fact]
    public async Task ValidateAsync_NullStream_ReturnsFailure()
    {
        // Arrange
        Stream? stream = null;

        // Act
        var result = await _validator.ValidateAsync(stream!, "test.pdf", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Keys.Should().Contain("stream");
        result.Errors["stream"].Should().ContainEquivalentOf("cannot be null");
    }

    [Fact]
    public async Task ValidateAsync_EmptyFileName_ReturnsFailure()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        var result = await _validator.ValidateAsync(stream, "", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Keys.Should().Contain("fileName");
        result.Errors["fileName"].Should().ContainEquivalentOf("cannot be empty");
    }

    [Fact]
    public async Task ValidateAsync_WhitespaceFileName_ReturnsFailure()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        var result = await _validator.ValidateAsync(stream, "   ", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Keys.Should().Contain("fileName");
    }
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
            result.Errors["fileFormat"].Should().NotContainEquivalentOf("PDF signature");
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
        result.IsValid.Should().BeFalse();
        result.Errors.Keys.Should().Contain("fileFormat");
        result.Errors["fileFormat"].Should().ContainEquivalentOf("PDF signature");
    }

    [Fact]
    public async Task ValidateAsync_EmptyStream_ReturnsFailure()
    {
        // Arrange: Empty stream
        using var stream = new MemoryStream();

        // Act
        var result = await _validator.ValidateAsync(stream, "test.pdf", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Keys.Should().Contain("fileFormat");
    }
    [Fact]
    public async Task ValidateAsync_ValidFileSize_PassesFileSizeCheck()
    {
        // Arrange: 1 MB file with PDF magic bytes
        var data = CreatePdfLikeBytes(1024 * 1024); // 1 MB
        using var stream = new MemoryStream(data);

        // Act
        var result = await _validator.ValidateAsync(stream, "test.pdf", TestCancellationToken);

        // Assert: Should not fail on file size
        result.Errors.Keys.Should().NotContain("fileSize");
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
        result.IsValid.Should().BeFalse();
        result.Errors.Keys.Should().Contain("fileSize");
        result.Errors["fileSize"].Should().ContainEquivalentOf("exceeds maximum");
    }
    [Fact]
    public async Task ValidateAsync_ValidMimeType_PassesMimeTypeCheck()
    {
        // Arrange: PDF file with .pdf extension
        var data = CreatePdfLikeBytes(1024);
        using var stream = new MemoryStream(data);

        // Act
        var result = await _validator.ValidateAsync(stream, "document.pdf", TestCancellationToken);

        // Assert: Should not fail on MIME type
        result.Errors.Keys.Should().NotContain("fileType");
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
        result.IsValid.Should().BeFalse();
        // Note: MIME type check delegates to domain service which checks content type
        // GetContentType() returns "application/octet-stream" for non-.pdf files
    }
    [Fact]
    public async Task ValidateAsync_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange: Invalid magic bytes + oversized file
        var invalidData = new byte[51 * 1024 * 1024]; // 51 MB of zeros (not PDF)
        using var stream = new MemoryStream(invalidData);

        // Act
        var result = await _validator.ValidateAsync(stream, "test.pdf", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().BeGreaterThanOrEqualTo(2); // At least magic bytes + file size errors
        result.Errors.Keys.Should().Contain("fileFormat");
        result.Errors.Keys.Should().Contain("fileSize");
    }
    [Fact]
    public async Task ExtractMetadataAsync_NullStream_ReturnsNull()
    {
        // Arrange
        Stream? stream = null;

        // Act
        var metadata = await _validator.ExtractMetadataAsync(stream!, TestCancellationToken);

        // Assert
        metadata.Should().BeNull();
    }

    [Fact]
    public async Task ExtractMetadataAsync_InvalidPdf_ReturnsNull()
    {
        // Arrange: Non-PDF data
        using var stream = new MemoryStream(new byte[] { 0x00, 0x00, 0x00 });

        // Act
        var metadata = await _validator.ExtractMetadataAsync(stream, TestCancellationToken);

        // Assert: Should return null for invalid PDF (Docnet parsing fails)
        metadata.Should().BeNull();
    }

    /// <summary>
    /// #4929: both entry points that open a PDF with Docnet must leave pdfium alive. Disposing <c>DocLib.Instance</c>
    /// calls <c>FPDF_DestroyLibrary</c>, so a document another caller (the extraction of a PDF) has open is then read
    /// from a destroyed library, and the process dies with an access violation (0xC0000005 in <c>FPDF_LoadPage</c>):
    /// not a failed test but a crashed test host, which is what this test used to do before the fix. The document is
    /// opened BEFORE the call and read AFTER it, so no timing is involved.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpeningAPdf_LeavesADocumentAnotherCallerHasOpenReadable(bool throughExtractMetadata)
    {
        var heldPdf = CreateTextPdf(pages: 4, token: "held");
        var otherPdf = CreateTextPdf(pages: 1, token: "other");
        using var heldReader = DocLib.Instance.GetDocReader(heldPdf, new PageDimensions(1080, 1920));
        using (var firstPage = heldReader.GetPageReader(0))
        {
            firstPage.GetText().Should().Contain("held0", "the document must read before the validation, or the test says nothing");
        }

        if (throughExtractMetadata)
        {
            using var otherStream = new MemoryStream(otherPdf);
            (await _validator.ExtractMetadataAsync(otherStream, TestCancellationToken)).Should().NotBeNull();
        }
        else
        {
            using var otherStream = new MemoryStream(otherPdf);
            (await _validator.ValidateAsync(otherStream, "other.pdf", TestCancellationToken)).IsValid.Should().BeTrue();
        }

        for (var page = 1; page < heldReader.GetPageCount(); page++)
        {
            using var reader = heldReader.GetPageReader(page);
            reader.GetText().Should().Contain($"held{page}", "page {0} of the document opened before the call", page);
        }
    }

    private static byte[] CreateTextPdf(int pages, string token) =>
        Document.Create(container =>
        {
            for (var page = 0; page < pages; page++)
            {
                var number = page;
                container.Page(p =>
                {
                    p.Margin(30);
                    p.Content().Text($"{token}{number} " + string.Join(' ', Enumerable.Range(0, 80).Select(i => $"word{i}")));
                });
            }
        }).GeneratePdf();

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
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
