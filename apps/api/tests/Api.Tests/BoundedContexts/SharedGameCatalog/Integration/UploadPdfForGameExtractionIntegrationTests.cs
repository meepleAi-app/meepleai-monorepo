using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Services.Pdf;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

/// <summary>
/// Integration tests for UploadPdfForGameExtractionCommandHandler.
/// Tests real storage operations with local blob storage service.
/// Issue #4154: Upload PDF Command for Game Metadata Extraction Wizard
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class UploadPdfForGameExtractionIntegrationTests : IAsyncLifetime
{
    private string? _testDataDirectory;
    private IBlobStorageService? _blobStorageService;
    private UploadPdfForGameExtractionCommandHandler? _handler;

    public async ValueTask InitializeAsync()
    {
        // Create temp directory for test PDFs
        _testDataDirectory = Path.Combine(Path.GetTempPath(), "meepleai-wizard-test-" + Guid.NewGuid());
        Directory.CreateDirectory(_testDataDirectory);

        // Create real local blob storage service
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PdfProcessing:UploadDirectory"] = _testDataDirectory,
            ["Storage:Provider"] = "local"
        });
        var configuration = configBuilder.Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Create local storage service via factory
        _blobStorageService = BlobStorageServiceFactory.Create(services.BuildServiceProvider());

        // Create handler with real storage
        var loggerMock = new Mock<ILogger<UploadPdfForGameExtractionCommandHandler>>();
        var pdfOptions = Options.Create(new PdfProcessingOptions
        {
            MaxFileSizeBytes = 104_857_600, // 100 MB
            LargePdfThresholdBytes = 52_428_800 // 50 MB (used by wizard)
        });
        _handler = new UploadPdfForGameExtractionCommandHandler(
            _blobStorageService,
            loggerMock.Object,
            pdfOptions);

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        // Cleanup test directory
        if (_testDataDirectory != null && Directory.Exists(_testDataDirectory))
        {
            try
            {
                Directory.Delete(_testDataDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        await Task.CompletedTask;
    }

    [Fact]
    public async Task Handle_WithValidPdf_StoresFileSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var mockPdfFile = CreateValidMockPdfFile("integration-test.pdf", 2048);
        var command = new UploadPdfForGameExtractionCommand(mockPdfFile, userId);

        // Act
        var result = await _handler!.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        if (!result.Success)
        {
            throw new Xunit.Sdk.XunitException($"Upload failed: {result.ErrorMessage}");
        }
        result.Success.Should().BeTrue();
        result.FileId.Should().NotBeNull();
        result.FilePath.Should().NotBeNullOrEmpty();
        result.FileSizeBytes.Should().Be(2048);
        result.ErrorMessage.Should().BeNull();

        // Verify file exists in storage
        // BlobStorageService.Exists() expects the fileId (GUID), not the full filename
        // The FilePath is: {uploadDir}\{gameId}\{fileId}_{sanitizedFilename}.pdf
        // Extract fileId by splitting the filename on underscore
        var filename = Path.GetFileName(result.FilePath!);
        var fileId = filename.Split('_')[0]; // Extract GUID part before underscore

        (await _blobStorageService!.ExistsAsync(fileId, "wizard-temp")).Should().BeTrue();

        // Verify file can be retrieved
        using var retrievedStream = await _blobStorageService.RetrieveAsync(fileId, "wizard-temp");
        retrievedStream.Should().NotBeNull();
        retrievedStream!.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_WithLargePdf_StoresFileSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var mockPdfFile = CreateValidMockPdfFile("large-rulebook.pdf", 10_485_760); // 10 MB
        var command = new UploadPdfForGameExtractionCommand(mockPdfFile, userId);

        // Act
        var result = await _handler!.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.FileSizeBytes.Should().Be(10_485_760);

        // Verify file exists - extract fileId from path
        var filename = Path.GetFileName(result.FilePath!);
        var fileId = filename.Split('_')[0];

        (await _blobStorageService!.ExistsAsync(fileId, "wizard-temp")).Should().BeTrue();

        // Cleanup
        await _blobStorageService.DeleteAsync(fileId, "wizard-temp");
    }

    [Fact]
    public async Task Handle_MultipleUploads_StoresAllFilesIndependently()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var file1 = CreateValidMockPdfFile("file1.pdf", 1024);
        var file2 = CreateValidMockPdfFile("file2.pdf", 2048);
        var file3 = CreateValidMockPdfFile("file3.pdf", 4096);

        var command1 = new UploadPdfForGameExtractionCommand(file1, userId);
        var command2 = new UploadPdfForGameExtractionCommand(file2, userId);
        var command3 = new UploadPdfForGameExtractionCommand(file3, userId);

        // Act
        var result1 = await _handler!.Handle(command1, CancellationToken.None);
        var result2 = await _handler.Handle(command2, CancellationToken.None);
        var result3 = await _handler.Handle(command3, CancellationToken.None);

        // Assert
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();

        // Extract fileIds from storage paths (format: {guid}_{sanitizedFilename})
        var filename1 = Path.GetFileName(result1.FilePath!);
        var fileId1 = filename1.Split('_')[0];

        var filename2 = Path.GetFileName(result2.FilePath!);
        var fileId2 = filename2.Split('_')[0];

        var filename3 = Path.GetFileName(result3.FilePath!);
        var fileId3 = filename3.Split('_')[0];

        // Verify all files exist independently
        (await _blobStorageService!.ExistsAsync(fileId1, "wizard-temp")).Should().BeTrue();
        (await _blobStorageService.ExistsAsync(fileId2, "wizard-temp")).Should().BeTrue();
        (await _blobStorageService.ExistsAsync(fileId3, "wizard-temp")).Should().BeTrue();

        // Verify unique file IDs
        result1.FileId!.Value.Should().NotBe(result2.FileId!.Value);
        result2.FileId!.Value.Should().NotBe(result3.FileId!.Value);
        result1.FileId!.Value.Should().NotBe(result3.FileId!.Value);

        // Cleanup
        await _blobStorageService.DeleteAsync(fileId1, "wizard-temp");
        await _blobStorageService.DeleteAsync(fileId2, "wizard-temp");
        await _blobStorageService.DeleteAsync(fileId3, "wizard-temp");
    }

    [Fact]
    public async Task Handle_WithSpecialCharactersInFilename_SanitizesAndStoresSuccessfully()
    {
        // Arrange - Use characters that ARE invalid for filenames
        var userId = Guid.NewGuid();
        var mockPdfFile = CreateValidMockPdfFile("rulebook<v2>:manual|2024.pdf", 1024);
        var command = new UploadPdfForGameExtractionCommand(mockPdfFile, userId);

        // Act
        var result = await _handler!.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.FileId.Should().NotBeNull();

        // Verify invalid filename characters were removed by PathSecurity.SanitizeFilename
        // Original: "rulebook<v2>:manual|2024.pdf"
        // Expected: "rulebookv2manual2024.pdf" (< > : | removed)
        // Check the filename only, not the full path (which contains colons in drive letters like D:\)
        var storedFilename = Path.GetFileName(result.FilePath!);
        storedFilename.Should().NotContain("<");
        storedFilename.Should().NotContain(">");
        storedFilename.Should().NotContain("|");
        // Colon check: filename portion should not contain colons (drive letters do, but filenames shouldn't)
        storedFilename.Split('_', 2)[1].Should().NotContain(":");

        // Verify file exists - extract fileId from path
        var filename = Path.GetFileName(result.FilePath!);
        var fileId = filename.Split('_')[0];

        (await _blobStorageService!.ExistsAsync(fileId, "wizard-temp")).Should().BeTrue();

        // Cleanup
        await _blobStorageService.DeleteAsync(fileId, "wizard-temp");
    }

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
                        "xref\n0 4\n0000000000 65535 f\n0000000009 00000 n\n" +
                        "0000000056 00000 n\n0000000115 00000 n\n" +
                        "trailer<</Size 4/Root 1 0 R>>\nstartxref\n198\n%%EOF";

        // Pad content to reach target file size
        if (fileSize > pdfContent.Length)
        {
            var padding = new string(' ', (int)(fileSize - pdfContent.Length));
            pdfContent = pdfContent.Replace("%%EOF", padding + "%%EOF");
        }

        var pdfBytes = System.Text.Encoding.ASCII.GetBytes(pdfContent);

        mockFile.Setup(f => f.OpenReadStream()).Returns(() =>
        {
            // Return a NEW stream for each call to allow multiple reads
            // (validation reads the stream once, storage reads it again)
            return new MemoryStream(pdfBytes);
        });

        return mockFile.Object;
    }

    #endregion
}