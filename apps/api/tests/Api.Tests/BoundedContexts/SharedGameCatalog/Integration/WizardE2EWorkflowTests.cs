using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Infrastructure.ExternalServices.BoardGameGeek;
using Api.Infrastructure.ExternalServices.BoardGameGeek.Models;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

/// <summary>
/// End-to-end wizard workflow tests validating the complete chain:
/// Upload PDF -> Extract Metadata -> Enrich from BGG -> Import Game.
/// Issue #4160: Backend - Wizard Integration Tests
/// </summary>
[Trait("Category", TestCategories.E2E)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4160")]
public class WizardE2EWorkflowTests
{
    private readonly Mock<IBlobStorageService> _blobStorageMock;
    private readonly Mock<IPdfTextExtractor> _pdfExtractorMock;
    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly Mock<IBggApiClient> _bggApiClientMock;
    private readonly IOptions<PdfProcessingOptions> _pdfOptions;
    private readonly Guid _testUserId = Guid.NewGuid();

    public WizardE2EWorkflowTests()
    {
        _blobStorageMock = new Mock<IBlobStorageService>();
        _pdfExtractorMock = new Mock<IPdfTextExtractor>();
        _llmServiceMock = new Mock<ILlmService>();
        _bggApiClientMock = new Mock<IBggApiClient>();
        _pdfOptions = Options.Create(new PdfProcessingOptions
        {
            MaxFileSizeBytes = 104_857_600,
            LargePdfThresholdBytes = 52_428_800
        });
    }

    [Fact]
    public async Task FullWorkflow_UploadExtractEnrichImport_Succeeds()
    {
        // === STEP 1: Upload PDF ===
        var uploadHandler = new UploadPdfForGameExtractionCommandHandler(
            _blobStorageMock.Object,
            NullLogger<UploadPdfForGameExtractionCommandHandler>.Instance,
            _pdfOptions);

        var mockPdfFile = CreateValidMockPdfFile("catan-rules.pdf", 2_097_152); // 2 MB
        // Handler extracts fileId from filename: split on '_', take first part
        var uploadedFilePath = "file-abc_20260216.pdf";

        _blobStorageMock
            .Setup(b => b.StoreAsync(It.IsAny<Stream>(), "catan-rules.pdf", "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(
                Success: true,
                FileId: "file-abc",
                FilePath: uploadedFilePath,
                FileSizeBytes: 2_097_152));

        var uploadResult = await uploadHandler.Handle(
            new UploadPdfForGameExtractionCommand(mockPdfFile, _testUserId),
            CancellationToken.None);

        uploadResult.Success.Should().BeTrue("Step 1: Upload should succeed");
        uploadResult.FilePath.Should().Be(uploadedFilePath);

        // === STEP 2: Extract Metadata ===
        var extractHandler = new ExtractGameMetadataFromPdfQueryHandler(
            _blobStorageMock.Object,
            _pdfExtractorMock.Object,
            _llmServiceMock.Object,
            NullLogger<ExtractGameMetadataFromPdfQueryHandler>.Instance);

        _blobStorageMock
            .Setup(b => b.RetrieveAsync("file-abc", "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("PDF content for Catan rules")));

        _pdfExtractorMock
            .Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.CreateSuccess(
                "Catan Rules\nPlayers: 3-4\nAge: 10+\nTime: 60-120 minutes\nYear: 1995",
                pageCount: 8,
                characterCount: 4500,
                ocrTriggered: false,
                quality: ExtractionQuality.High));

        _llmServiceMock
            .Setup(l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameMetadataDto
            {
                Title = "Catan",
                Year = 1995,
                MinPlayers = 3,
                MaxPlayers = 4,
                PlayingTime = 90,
                MinAge = 10,
                Description = "Trade, build, settle on the island of Catan"
            });

        var extractResult = await extractHandler.Handle(
            new ExtractGameMetadataFromPdfQuery(uploadResult.FilePath!, _testUserId),
            CancellationToken.None);

        extractResult.Title.Should().Be("Catan", "Step 2: Should extract title from PDF");
        extractResult.Year.Should().Be(1995);
        extractResult.MinPlayers.Should().Be(3);
        extractResult.MaxPlayers.Should().Be(4);
        extractResult.ConfidenceScore.Should().BeGreaterThan(0.5, "Step 2: High quality extraction should have good confidence");

        // === STEP 3: Enrich from BGG ===
        var enrichHandler = new EnrichGameMetadataFromBggCommandHandler(
            _bggApiClientMock.Object,
            NullLogger<EnrichGameMetadataFromBggCommandHandler>.Instance);

        _bggApiClientMock
            .Setup(b => b.GetGameDetailsAsync(13, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BggGameDetails
            {
                BggId = 13,
                Title = "The Settlers of Catan",
                YearPublished = 1995,
                MinPlayers = 3,
                MaxPlayers = 4,
                PlayingTimeMinutes = 120,
                Description = "Classic trading and building game",
                ComplexityRating = 2.34m,
                ImageUrl = "https://example.com/catan.jpg",
                ThumbnailUrl = "https://example.com/catan-thumb.jpg",
                AverageRating = 7.2m,
                RankPosition = 150
            });

        var enrichResult = await enrichHandler.Handle(
            new EnrichGameMetadataFromBggCommand(extractResult, 13, _testUserId),
            CancellationToken.None);

        enrichResult.Title.Should().Be("The Settlers of Catan", "Step 3: BGG title should be preferred");
        enrichResult.BggEnrichmentSucceeded.Should().BeTrue();
        enrichResult.BggId.Should().Be(13);
        enrichResult.ComplexityRating.Should().Be(2.34m);
        enrichResult.ImageUrl.Should().NotBeNullOrEmpty("Step 3: Should have BGG image");

        // PlayingTime conflict: PDF=90 vs BGG=120
        enrichResult.Conflicts.Should().ContainSingle(c => c.FieldName == "PlayingTime",
            "Step 3: Should detect PlayingTime conflict between PDF (90) and BGG (120)");

        // === STEP 4: Import Game (verify command can be constructed) ===
        // ImportGameFromBggCommand requires full DB infrastructure, so we verify
        // the enrichment output chains correctly to import command parameters
        var importCommand = new ImportGameFromBggCommand(enrichResult.BggId, _testUserId);
        importCommand.BggId.Should().Be(13);
        importCommand.UserId.Should().Be(_testUserId);
    }

    [Fact]
    public async Task FullWorkflow_WithBggFallback_ContinuesWithPdfDataOnly()
    {
        // === STEP 1: Upload ===
        var uploadHandler = new UploadPdfForGameExtractionCommandHandler(
            _blobStorageMock.Object,
            NullLogger<UploadPdfForGameExtractionCommandHandler>.Instance,
            _pdfOptions);

        var mockPdfFile = CreateValidMockPdfFile("obscure-game.pdf", 1_048_576);

        _blobStorageMock
            .Setup(b => b.StoreAsync(It.IsAny<Stream>(), "obscure-game.pdf", "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(
                Success: true, FileId: "file-xyz",
                FilePath: "file-xyz_20260216.pdf",
                FileSizeBytes: 1_048_576));

        var uploadResult = await uploadHandler.Handle(
            new UploadPdfForGameExtractionCommand(mockPdfFile, _testUserId),
            CancellationToken.None);

        uploadResult.Success.Should().BeTrue();

        // === STEP 2: Extract ===
        var extractHandler = new ExtractGameMetadataFromPdfQueryHandler(
            _blobStorageMock.Object, _pdfExtractorMock.Object,
            _llmServiceMock.Object,
            NullLogger<ExtractGameMetadataFromPdfQueryHandler>.Instance);

        _blobStorageMock
            .Setup(b => b.RetrieveAsync("file-xyz", "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[100]));

        _pdfExtractorMock
            .Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.CreateSuccess(
                "Obscure Board Game\nPlayers: 2-5\nTime: 45 min",
                pageCount: 4, characterCount: 2000, ocrTriggered: true,
                quality: ExtractionQuality.Medium));

        _llmServiceMock
            .Setup(l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameMetadataDto
            {
                Title = "Obscure Board Game",
                MinPlayers = 2,
                MaxPlayers = 5,
                PlayingTime = 45,
                ConfidenceScore = 0.65
            });

        var extractResult = await extractHandler.Handle(
            new ExtractGameMetadataFromPdfQuery(uploadResult.FilePath!, _testUserId),
            CancellationToken.None);

        // === STEP 3: BGG Enrichment fails (timeout) ===
        var enrichHandler = new EnrichGameMetadataFromBggCommandHandler(
            _bggApiClientMock.Object,
            NullLogger<EnrichGameMetadataFromBggCommandHandler>.Instance);

        _bggApiClientMock
            .Setup(b => b.GetGameDetailsAsync(99999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("BGG API unreachable"));

        var enrichResult = await enrichHandler.Handle(
            new EnrichGameMetadataFromBggCommand(extractResult, 99999, _testUserId),
            CancellationToken.None);

        // Verify graceful fallback
        enrichResult.BggEnrichmentSucceeded.Should().BeFalse("BGG enrichment should fail gracefully");
        enrichResult.EnrichmentWarning.Should().Contain("timed out");
        enrichResult.Title.Should().Be("Obscure Board Game", "Should preserve PDF title on BGG failure");
        enrichResult.MinPlayers.Should().Be(2, "Should preserve PDF data");
        enrichResult.MaxPlayers.Should().Be(5, "Should preserve PDF data");
        enrichResult.PlayingTime.Should().Be(45, "Should preserve PDF data");
        enrichResult.Conflicts.Should().BeEmpty("No conflicts when only PDF data available");
    }

    [Fact]
    public async Task FullWorkflow_ExtractionFailsGracefully_ReturnsEmptyMetadata()
    {
        // === STEP 1: Upload succeeds ===
        var uploadHandler = new UploadPdfForGameExtractionCommandHandler(
            _blobStorageMock.Object,
            NullLogger<UploadPdfForGameExtractionCommandHandler>.Instance,
            _pdfOptions);

        var mockPdfFile = CreateValidMockPdfFile("scanned-image.pdf", 5_000_000);

        _blobStorageMock
            .Setup(b => b.StoreAsync(It.IsAny<Stream>(), "scanned-image.pdf", "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(
                Success: true, FileId: "file-img",
                FilePath: "file-img_20260216.pdf",
                FileSizeBytes: 5_000_000));

        var uploadResult = await uploadHandler.Handle(
            new UploadPdfForGameExtractionCommand(mockPdfFile, _testUserId),
            CancellationToken.None);

        uploadResult.Success.Should().BeTrue();

        // === STEP 2: Extraction fails (OCR failure) ===
        var extractHandler = new ExtractGameMetadataFromPdfQueryHandler(
            _blobStorageMock.Object, _pdfExtractorMock.Object,
            _llmServiceMock.Object,
            NullLogger<ExtractGameMetadataFromPdfQueryHandler>.Instance);

        _blobStorageMock
            .Setup(b => b.RetrieveAsync("file-img", "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[100]));

        _pdfExtractorMock
            .Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.CreateFailure("OCR service unavailable"));

        var extractResult = await extractHandler.Handle(
            new ExtractGameMetadataFromPdfQuery(uploadResult.FilePath!, _testUserId),
            CancellationToken.None);

        // Verify graceful degradation
        extractResult.Title.Should().BeNull("OCR failure should return empty metadata");
        extractResult.ConfidenceScore.Should().Be(0.0, "Failed extraction should have zero confidence");
    }

    [Fact]
    public async Task FullWorkflow_LlmParsingFails_ReturnsEmptyMetadata()
    {
        // === Upload + Blob retrieval succeeds ===
        var extractHandler = new ExtractGameMetadataFromPdfQueryHandler(
            _blobStorageMock.Object, _pdfExtractorMock.Object,
            _llmServiceMock.Object,
            NullLogger<ExtractGameMetadataFromPdfQueryHandler>.Instance);

        _blobStorageMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[100]));

        _pdfExtractorMock
            .Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.CreateSuccess(
                "Some text", 3, 1500, false, ExtractionQuality.Medium));

        // LLM returns null (parsing failure)
        _llmServiceMock
            .Setup(l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameMetadataDto?)null);

        var result = await extractHandler.Handle(
            new ExtractGameMetadataFromPdfQuery("file-abc_timestamp.pdf", _testUserId),
            CancellationToken.None);

        result.ConfidenceScore.Should().Be(0.0, "LLM failure should return empty metadata");
        result.Title.Should().BeNull();
    }

    #region Helper Methods

    private static IFormFile CreateValidMockPdfFile(string fileName, long fileSize)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.SetupGet(f => f.FileName).Returns(fileName);
        mockFile.SetupGet(f => f.Length).Returns(fileSize);
        mockFile.SetupGet(f => f.ContentType).Returns("application/pdf");

        var pdfContent = "%PDF-1.4\n" +
                        "1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n" +
                        "2 0 obj<</Type/Pages/Count 1/Kids[3 0 R]>>endobj\n" +
                        "3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R>>endobj\n" +
                        "xref\n0 4\n0000000000 65535 f\n" +
                        "0000000009 00000 n\n0000000056 00000 n\n0000000115 00000 n\n" +
                        "trailer<</Size 4/Root 1 0 R>>\nstartxref\n198\n%%EOF";

        var pdfBytes = System.Text.Encoding.ASCII.GetBytes(pdfContent);
        mockFile.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(pdfBytes));

        return mockFile.Object;
    }

    #endregion
}
