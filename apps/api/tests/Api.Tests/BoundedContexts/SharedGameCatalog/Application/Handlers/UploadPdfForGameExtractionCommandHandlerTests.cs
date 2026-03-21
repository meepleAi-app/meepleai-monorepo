using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Services.Pdf;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Unit tests for UploadPdfForGameExtractionCommandHandler.
/// Tests temporary PDF upload for wizard metadata extraction.
/// Issue #4154: Upload PDF Command for Game Metadata Extraction Wizard
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class UploadPdfForGameExtractionCommandHandlerTests
{
    private readonly Mock<IBlobStorageService> _blobStorageServiceMock;
    private readonly Mock<ILogger<UploadPdfForGameExtractionCommandHandler>> _loggerMock;
    private readonly IOptions<PdfProcessingOptions> _pdfOptions;
    private readonly UploadPdfForGameExtractionCommandHandler _handler;

    public UploadPdfForGameExtractionCommandHandlerTests()
    {
        _blobStorageServiceMock = new Mock<IBlobStorageService>();
        _loggerMock = new Mock<ILogger<UploadPdfForGameExtractionCommandHandler>>();
        _pdfOptions = Options.Create(new PdfProcessingOptions
        {
            MaxFileSizeBytes = 104_857_600, // 100 MB (library upload limit)
            LargePdfThresholdBytes = 52_428_800 // 50 MB (wizard upload limit)
        });
        _handler = new UploadPdfForGameExtractionCommandHandler(
            _blobStorageServiceMock.Object,
            _loggerMock.Object,
            _pdfOptions);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var handler = new UploadPdfForGameExtractionCommandHandler(
            _blobStorageServiceMock.Object,
            _loggerMock.Object,
            _pdfOptions);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullBlobStorageService_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new UploadPdfForGameExtractionCommandHandler(null!, _loggerMock.Object, _pdfOptions);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("blobStorageService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new UploadPdfForGameExtractionCommandHandler(_blobStorageServiceMock.Object, null!, _pdfOptions);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullPdfOptions_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new UploadPdfForGameExtractionCommandHandler(_blobStorageServiceMock.Object, _loggerMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("pdfOptions");
    }

    #endregion

    #region Handle - Success Scenarios

    [Fact]
    public async Task Handle_WithValidPdf_ReturnsSuccessResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var mockPdfFile = CreateValidMockPdfFile("test-document.pdf", 1_048_576); // 1 MB
        var command = new UploadPdfForGameExtractionCommand(mockPdfFile, userId);

        var expectedFileId = "test-file-id";
        var expectedFilePath = "wizard-temp/test-file-id/test-document.pdf";

        _blobStorageServiceMock
            .Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(
                Success: true,
                FileId: expectedFileId,
                FilePath: expectedFilePath,
                FileSizeBytes: 1_048_576));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.FileId.Should().NotBeNull();
        result.FilePath.Should().Be(expectedFilePath);
        result.FileSizeBytes.Should().Be(1_048_576);
        result.ErrorMessage.Should().BeNull();

        _blobStorageServiceMock.Verify(
            b => b.StoreAsync(It.IsAny<Stream>(), "test-document.pdf", "wizard-temp", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithMaxSizePdf_ReturnsSuccessResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var mockPdfFile = CreateValidMockPdfFile("large-manual.pdf", 52_428_800); // Exactly 50 MB
        var command = new UploadPdfForGameExtractionCommand(mockPdfFile, userId);

        _blobStorageServiceMock
            .Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(
                Success: true,
                FileId: "large-file-id",
                FilePath: "wizard-temp/large-file-id/large-manual.pdf",
                FileSizeBytes: 52_428_800));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.FileSizeBytes.Should().Be(52_428_800);
    }

    #endregion

    #region Handle - Validation Failures

    [Fact]
    public async Task Handle_WithNullFile_ReturnsFailureResult()
    {
        // Arrange
        var command = new UploadPdfForGameExtractionCommand(null!, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.FileId.Should().BeNull();
        result.FilePath.Should().BeNull();
        result.ErrorMessage.Should().Be("No file provided. Please select a PDF file to upload.");

        _blobStorageServiceMock.Verify(
            b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyFile_ReturnsFailureResult()
    {
        // Arrange
        var mockPdfFile = CreateValidMockPdfFile("empty.pdf", 0);
        var command = new UploadPdfForGameExtractionCommand(mockPdfFile, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No file provided. Please select a PDF file to upload.");

        _blobStorageServiceMock.Verify(
            b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithFileTooLarge_ReturnsFailureResult()
    {
        // Arrange
        var mockPdfFile = CreateValidMockPdfFile("huge.pdf", 52_428_801); // 50MB + 1 byte
        var command = new UploadPdfForGameExtractionCommand(mockPdfFile, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("File is too large");
        // Accept both "50.0MB" and "50,0MB" (culture-invariant check)
        result.ErrorMessage.Should().MatchRegex(@"50[.,]0MB", "because the error message should contain file size");
        result.ErrorMessage.Should().Contain("Maximum size is 50MB");

        _blobStorageServiceMock.Verify(
            b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("application/msword")]
    [InlineData("image/png")]
    [InlineData("text/plain")]
    public async Task Handle_WithInvalidContentType_ReturnsFailureResult(string contentType)
    {
        // Arrange
        var mockPdfFile = CreateMockPdfFile("document.pdf", 1024, contentType);
        var command = new UploadPdfForGameExtractionCommand(mockPdfFile, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid file type");
        result.ErrorMessage.Should().Contain(contentType);

        _blobStorageServiceMock.Verify(
            b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithCorruptedPdf_ReturnsFailureResult()
    {
        // Arrange - File with enough size (>50 bytes) but invalid PDF header
        var invalidContent = "This is not a PDF file, just random text that makes it long enough" + new string('x', 100);
        var mockPdfFile = CreateInvalidPdfFile("corrupted.pdf", invalidContent);
        var command = new UploadPdfForGameExtractionCommand(mockPdfFile, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid PDF file");
        result.ErrorMessage.Should().Contain("Missing PDF header signature");

        _blobStorageServiceMock.Verify(
            b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithTruncatedPdf_ReturnsFailureResult()
    {
        // Arrange - PDF with valid header but missing EOF marker (>50 bytes)
        var truncatedContent = "%PDF-1.4\n" +
                              "1 0 obj<</Type/Catalog>>endobj\n" +
                              "xref\n0 1\n" +
                              "trailer<</Size 1>>\nstartxref\n" +
                              new string('x', 200); // Padding to exceed 50 bytes, but no %%EOF
        var mockPdfFile = CreateInvalidPdfFile("truncated.pdf", truncatedContent);
        var command = new UploadPdfForGameExtractionCommand(mockPdfFile, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid PDF file");
        result.ErrorMessage.Should().Contain("Missing EOF marker");

        _blobStorageServiceMock.Verify(
            b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithTooSmallFile_ReturnsFailureResult()
    {
        // Arrange - File smaller than 50 bytes
        var mockPdfFile = CreateInvalidPdfFile("tiny.pdf", "%PDF");
        var command = new UploadPdfForGameExtractionCommand(mockPdfFile, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("File is too small to be a valid PDF");

        _blobStorageServiceMock.Verify(
            b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Handle - Storage Failures

    [Fact]
    public async Task Handle_WhenStorageFails_ReturnsFailureResult()
    {
        // Arrange
        var mockPdfFile = CreateValidMockPdfFile("test.pdf", 1024);
        var command = new UploadPdfForGameExtractionCommand(mockPdfFile, Guid.NewGuid());

        _blobStorageServiceMock
            .Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(
                Success: false,
                FileId: null,
                FilePath: null,
                FileSizeBytes: 0,
                ErrorMessage: "S3 connection timeout"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.FileId.Should().BeNull();
        result.FilePath.Should().BeNull();
        result.ErrorMessage.Should().Be("S3 connection timeout");

        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to store temporary PDF")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenStorageThrowsException_ReturnsFailureResult()
    {
        // Arrange
        var mockPdfFile = CreateValidMockPdfFile("test.pdf", 1024);
        var command = new UploadPdfForGameExtractionCommand(mockPdfFile, Guid.NewGuid());

        _blobStorageServiceMock
            .Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk full"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.FileId.Should().BeNull();
        result.FilePath.Should().BeNull();
        result.ErrorMessage.Should().Be("An unexpected error occurred while storing the file. Please try again.");

        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error storing temporary PDF")),
                It.IsAny<IOException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Handle - Edge Cases

    [Fact]
    public async Task Handle_WithInvalidCharactersInFilename_SanitizesAndSucceeds()
    {
        // Arrange - Use filename with characters that ARE actually invalid for filenames
        var mockPdfFile = CreateValidMockPdfFile("rulebook<2024>:manual|v1.pdf", 1024);
        var command = new UploadPdfForGameExtractionCommand(mockPdfFile, Guid.NewGuid());

        string? capturedFilename = null;
        _blobStorageServiceMock
            .Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .Callback<Stream, string, string, CancellationToken>((stream, filename, gameId, ct) =>
            {
                capturedFilename = filename;
            })
            .ReturnsAsync(new BlobStorageResult(
                Success: true,
                FileId: "file-id",
                FilePath: "wizard-temp/file-id/sanitized-name.pdf",
                FileSizeBytes: 1024));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.FileId.Should().NotBeNull();

        // Verify invalid filename characters were removed by PathSecurity.SanitizeFilename
        // Original: "rulebook<2024>:manual|v1.pdf"
        // Expected: "rulebook2024manualv1.pdf" (< > : | removed)
        capturedFilename.Should().NotBeNull();
        capturedFilename.Should().NotContain("<");
        capturedFilename.Should().NotContain(">");
        capturedFilename.Should().NotContain(":");
        capturedFilename.Should().NotContain("|");
        capturedFilename.Should().EndWith(".pdf");
        capturedFilename.Should().Contain("rulebook");
        capturedFilename.Should().Contain("2024");
        capturedFilename.Should().Contain("manual");

        _blobStorageServiceMock.Verify(
            b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockPdfFile = CreateValidMockPdfFile("test.pdf", 1024);
        var command = new UploadPdfForGameExtractionCommand(mockPdfFile, Guid.NewGuid());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await _handler.Handle(command, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Handle - Filename Security

    [Theory]
    [InlineData("../../etc/passwd.pdf")]
    [InlineData("..\\..\\windows\\system32\\config.pdf")]
    [InlineData("C:\\Windows\\System32\\file.pdf")]
    public async Task Handle_WithPathTraversalAttempt_SanitizesAndSucceeds(string maliciousFileName)
    {
        // Arrange
        var mockPdfFile = CreateValidMockPdfFile(maliciousFileName, 1024);
        var command = new UploadPdfForGameExtractionCommand(mockPdfFile, Guid.NewGuid());

        string? capturedFilename = null;
        _blobStorageServiceMock
            .Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .Callback<Stream, string, string, CancellationToken>((stream, filename, gameId, ct) =>
            {
                capturedFilename = filename;
            })
            .ReturnsAsync(new BlobStorageResult(
                Success: true,
                FileId: "file-id",
                FilePath: "wizard-temp/file-id/sanitized.pdf",
                FileSizeBytes: 1024));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        // PathSecurity.SanitizeFilename removes path separators (/, \, :) making the filename safe
        // e.g., "../../etc/passwd.pdf" becomes "etcpasswd.pdf"
        // e.g., "C:\Windows\System32\file.pdf" becomes "CWindowsSystem32file.pdf"
        result.Success.Should().BeTrue("because path traversal characters are removed during sanitization");
        result.FileId.Should().NotBeNull();

        // Verify sanitized filename doesn't contain path traversal characters
        capturedFilename.Should().NotBeNull();
        capturedFilename.Should().NotContain("/");
        capturedFilename.Should().NotContain("\\");
        capturedFilename.Should().NotContain(":");
        capturedFilename.Should().NotContain("..");
        capturedFilename.Should().EndWith(".pdf");

        _blobStorageServiceMock.Verify(
            b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a valid mock PDF IFormFile with realistic PDF structure.
    /// </summary>
    private static IFormFile CreateValidMockPdfFile(string fileName, long fileSize)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.SetupGet(f => f.FileName).Returns(fileName);
        mockFile.SetupGet(f => f.Length).Returns(fileSize);
        mockFile.SetupGet(f => f.ContentType).Returns("application/pdf");

        // Create a minimal valid PDF structure
        var pdfContent = "%PDF-1.4\n" +
                        "1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n" +
                        "2 0 obj<</Type/Pages/Count 1/Kids[3 0 R]>>endobj\n" +
                        "3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R>>endobj\n" +
                        "xref\n0 4\n0000000000 65535 f\n" +
                        "0000000009 00000 n\n0000000056 00000 n\n0000000115 00000 n\n" +
                        "trailer<</Size 4/Root 1 0 R>>\nstartxref\n198\n%%EOF";

        var pdfBytes = System.Text.Encoding.ASCII.GetBytes(pdfContent);
        var stream = new MemoryStream(pdfBytes);

        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);

        return mockFile.Object;
    }

    /// <summary>
    /// Creates a mock PDF file with custom content type.
    /// </summary>
    private static IFormFile CreateMockPdfFile(string fileName, long fileSize, string contentType)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.SetupGet(f => f.FileName).Returns(fileName);
        mockFile.SetupGet(f => f.Length).Returns(fileSize);
        mockFile.SetupGet(f => f.ContentType).Returns(contentType);

        var pdfContent = "%PDF-1.4\ntest content\n%%EOF";
        var pdfBytes = System.Text.Encoding.ASCII.GetBytes(pdfContent);

        // Return NEW stream for each call to allow multiple reads (validation + storage)
        mockFile.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(pdfBytes));

        return mockFile.Object;
    }

    /// <summary>
    /// Creates an invalid PDF file with custom content (for structure validation tests).
    /// </summary>
    private static IFormFile CreateInvalidPdfFile(string fileName, string content)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.SetupGet(f => f.FileName).Returns(fileName);
        mockFile.SetupGet(f => f.Length).Returns(content.Length);
        mockFile.SetupGet(f => f.ContentType).Returns("application/pdf");

        var contentBytes = System.Text.Encoding.ASCII.GetBytes(content);

        // Return NEW stream for each call to allow multiple reads (validation + storage)
        mockFile.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(contentBytes));

        return mockFile.Object;
    }

    #endregion
}
