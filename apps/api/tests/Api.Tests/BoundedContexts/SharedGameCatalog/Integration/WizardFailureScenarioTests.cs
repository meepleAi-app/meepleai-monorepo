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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

/// <summary>
/// Wizard failure scenario tests covering storage failures, invalid inputs,
/// BGG API errors, and edge cases in the extraction pipeline.
/// Issue #4160: Backend - Wizard Integration Tests
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4160")]
public class WizardFailureScenarioTests
{
    private readonly Mock<IBlobStorageService> _blobStorageMock;
    private readonly Mock<IPdfTextExtractor> _pdfExtractorMock;
    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly Mock<IBggApiClient> _bggApiClientMock;
    private readonly IOptions<PdfProcessingOptions> _pdfOptions;
    private readonly Guid _testUserId = Guid.NewGuid();

    public WizardFailureScenarioTests()
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

    #region Upload Failure Scenarios

    [Fact]
    public async Task Upload_StorageFailure_ReturnsFailureResult()
    {
        var handler = new UploadPdfForGameExtractionCommandHandler(
            _blobStorageMock.Object,
            NullLogger<UploadPdfForGameExtractionCommandHandler>.Instance,
            _pdfOptions);

        var mockFile = CreateValidMockPdfFile("game-rules.pdf", 1_000_000);

        _blobStorageMock
            .Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(
                Success: false,
                FileId: null,
                FilePath: null,
                FileSizeBytes: 0,
                ErrorMessage: "Disk full"));

        var result = await handler.Handle(
            new UploadPdfForGameExtractionCommand(mockFile, _testUserId),
            CancellationToken.None);

        result.Success.Should().BeFalse("Storage failure should return failure result");
        result.ErrorMessage.Should().Contain("Disk full");
    }

    [Fact]
    public async Task Upload_StorageThrowsException_ReturnsFailureResult()
    {
        var handler = new UploadPdfForGameExtractionCommandHandler(
            _blobStorageMock.Object,
            NullLogger<UploadPdfForGameExtractionCommandHandler>.Instance,
            _pdfOptions);

        var mockFile = CreateValidMockPdfFile("game-rules.pdf", 1_000_000);

        _blobStorageMock
            .Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Network connection lost"));

        var result = await handler.Handle(
            new UploadPdfForGameExtractionCommand(mockFile, _testUserId),
            CancellationToken.None);

        result.Success.Should().BeFalse("Storage exception should return failure result");
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Upload_FileTooLarge_ReturnsValidationError()
    {
        var handler = new UploadPdfForGameExtractionCommandHandler(
            _blobStorageMock.Object,
            NullLogger<UploadPdfForGameExtractionCommandHandler>.Instance,
            _pdfOptions);

        // Exceeds LargePdfThresholdBytes (50 MB)
        var mockFile = CreateValidMockPdfFile("huge-manual.pdf", 60_000_000);

        var result = await handler.Handle(
            new UploadPdfForGameExtractionCommand(mockFile, _testUserId),
            CancellationToken.None);

        result.Success.Should().BeFalse("File exceeding size limit should fail validation");
        result.ErrorMessage.Should().Contain("too large");
    }

    [Fact]
    public async Task Upload_InvalidContentType_ReturnsValidationError()
    {
        var handler = new UploadPdfForGameExtractionCommandHandler(
            _blobStorageMock.Object,
            NullLogger<UploadPdfForGameExtractionCommandHandler>.Instance,
            _pdfOptions);

        var mockFile = new Mock<IFormFile>();
        mockFile.SetupGet(f => f.FileName).Returns("document.docx");
        mockFile.SetupGet(f => f.Length).Returns(500_000);
        mockFile.SetupGet(f => f.ContentType).Returns("application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        var result = await handler.Handle(
            new UploadPdfForGameExtractionCommand(mockFile.Object, _testUserId),
            CancellationToken.None);

        result.Success.Should().BeFalse("Non-PDF content type should fail validation");
        result.ErrorMessage.Should().Contain("Only PDF");
    }

    [Fact]
    public async Task Upload_NullFile_ReturnsValidationError()
    {
        var handler = new UploadPdfForGameExtractionCommandHandler(
            _blobStorageMock.Object,
            NullLogger<UploadPdfForGameExtractionCommandHandler>.Instance,
            _pdfOptions);

        var mockFile = new Mock<IFormFile>();
        mockFile.SetupGet(f => f.Length).Returns(0);
        mockFile.SetupGet(f => f.FileName).Returns("empty.pdf");
        mockFile.SetupGet(f => f.ContentType).Returns("application/pdf");

        var result = await handler.Handle(
            new UploadPdfForGameExtractionCommand(mockFile.Object, _testUserId),
            CancellationToken.None);

        result.Success.Should().BeFalse("Empty file should fail validation");
        result.ErrorMessage.Should().Contain("No file");
    }

    [Fact]
    public async Task Upload_InvalidPdfStructure_ReturnsValidationError()
    {
        var handler = new UploadPdfForGameExtractionCommandHandler(
            _blobStorageMock.Object,
            NullLogger<UploadPdfForGameExtractionCommandHandler>.Instance,
            _pdfOptions);

        var mockFile = new Mock<IFormFile>();
        mockFile.SetupGet(f => f.FileName).Returns("fake.pdf");
        mockFile.SetupGet(f => f.Length).Returns(1_000);
        mockFile.SetupGet(f => f.ContentType).Returns("application/pdf");
        // Not a real PDF - missing %PDF- header
        var fakeContent = System.Text.Encoding.ASCII.GetBytes("This is not a PDF file at all, just plain text.");
        mockFile.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(fakeContent));

        var result = await handler.Handle(
            new UploadPdfForGameExtractionCommand(mockFile.Object, _testUserId),
            CancellationToken.None);

        result.Success.Should().BeFalse("Invalid PDF structure should fail validation");
        result.ErrorMessage.Should().Contain("PDF");
    }

    #endregion

    #region Extraction Failure Scenarios

    [Fact]
    public async Task Extract_BlobRetrievalReturnsNull_ReturnsEmptyMetadata()
    {
        var handler = new ExtractGameMetadataFromPdfQueryHandler(
            _blobStorageMock.Object, _pdfExtractorMock.Object,
            _llmServiceMock.Object,
            NullLogger<ExtractGameMetadataFromPdfQueryHandler>.Instance);

        _blobStorageMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        var result = await handler.Handle(
            new ExtractGameMetadataFromPdfQuery("file-missing_20260216.pdf", _testUserId),
            CancellationToken.None);

        result.Title.Should().BeNull("Missing blob should return empty metadata");
        result.ConfidenceScore.Should().Be(0.0);
    }

    [Fact]
    public async Task Extract_PdfExtractorThrowsException_ReturnsEmptyMetadata()
    {
        var handler = new ExtractGameMetadataFromPdfQueryHandler(
            _blobStorageMock.Object, _pdfExtractorMock.Object,
            _llmServiceMock.Object,
            NullLogger<ExtractGameMetadataFromPdfQueryHandler>.Instance);

        _blobStorageMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[100]));

        _pdfExtractorMock
            .Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SmolDocling service crashed"));

        var result = await handler.Handle(
            new ExtractGameMetadataFromPdfQuery("file-crash_20260216.pdf", _testUserId),
            CancellationToken.None);

        result.Title.Should().BeNull("Extraction exception should return empty metadata");
        result.ConfidenceScore.Should().Be(0.0);
    }

    [Fact]
    public async Task Extract_LowQualityExtraction_HasLowConfidence()
    {
        var handler = new ExtractGameMetadataFromPdfQueryHandler(
            _blobStorageMock.Object, _pdfExtractorMock.Object,
            _llmServiceMock.Object,
            NullLogger<ExtractGameMetadataFromPdfQueryHandler>.Instance);

        _blobStorageMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[100]));

        _pdfExtractorMock
            .Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.CreateSuccess(
                "Barely readable text", pageCount: 1, characterCount: 50,
                ocrTriggered: true, quality: ExtractionQuality.VeryLow));

        _llmServiceMock
            .Setup(l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameMetadataDto
            {
                Title = "Unknown Game"
            });

        var result = await handler.Handle(
            new ExtractGameMetadataFromPdfQuery("file-lowq_20260216.pdf", _testUserId),
            CancellationToken.None);

        result.ConfidenceScore.Should().BeLessThan(0.5,
            "VeryLow quality extraction with sparse fields should have low confidence");
    }

    [Fact]
    public async Task Extract_LlmThrowsException_ReturnsEmptyMetadata()
    {
        var handler = new ExtractGameMetadataFromPdfQueryHandler(
            _blobStorageMock.Object, _pdfExtractorMock.Object,
            _llmServiceMock.Object,
            NullLogger<ExtractGameMetadataFromPdfQueryHandler>.Instance);

        _blobStorageMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[100]));

        _pdfExtractorMock
            .Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.CreateSuccess(
                "Game rules text", 5, 3000, false, ExtractionQuality.High));

        _llmServiceMock
            .Setup(l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM API unreachable"));

        var result = await handler.Handle(
            new ExtractGameMetadataFromPdfQuery("file-llmerr_20260216.pdf", _testUserId),
            CancellationToken.None);

        result.Title.Should().BeNull("LLM exception should return empty metadata");
        result.ConfidenceScore.Should().Be(0.0);
    }

    #endregion

    #region BGG Enrichment Failure Scenarios

    [Fact]
    public async Task Enrich_BggHttpRequestException_FallsBackToPdfData()
    {
        var enrichHandler = new EnrichGameMetadataFromBggCommandHandler(
            _bggApiClientMock.Object,
            NullLogger<EnrichGameMetadataFromBggCommandHandler>.Instance);

        var pdfMetadata = new GameMetadataDto
        {
            Title = "My Board Game",
            Year = 2020,
            MinPlayers = 2,
            MaxPlayers = 6
        };

        _bggApiClientMock
            .Setup(b => b.GetGameDetailsAsync(12345, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var result = await enrichHandler.Handle(
            new EnrichGameMetadataFromBggCommand(pdfMetadata, 12345, _testUserId),
            CancellationToken.None);

        result.BggEnrichmentSucceeded.Should().BeFalse();
        result.EnrichmentWarning.Should().Contain("failed");
        result.Title.Should().Be("My Board Game", "Should preserve PDF title");
        result.Year.Should().Be(2020, "Should preserve PDF year");
        result.MinPlayers.Should().Be(2, "Should preserve PDF data");
        result.MaxPlayers.Should().Be(6, "Should preserve PDF data");
    }

    [Fact]
    public async Task Enrich_BggUnexpectedException_FallsBackToPdfData()
    {
        var enrichHandler = new EnrichGameMetadataFromBggCommandHandler(
            _bggApiClientMock.Object,
            NullLogger<EnrichGameMetadataFromBggCommandHandler>.Instance);

        var pdfMetadata = new GameMetadataDto
        {
            Title = "Another Game",
            MinPlayers = 3,
            MaxPlayers = 5
        };

        _bggApiClientMock
            .Setup(b => b.GetGameDetailsAsync(99999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected parse error"));

        var result = await enrichHandler.Handle(
            new EnrichGameMetadataFromBggCommand(pdfMetadata, 99999, _testUserId),
            CancellationToken.None);

        result.BggEnrichmentSucceeded.Should().BeFalse();
        result.EnrichmentWarning.Should().Contain("unexpected");
        result.Title.Should().Be("Another Game");
    }

    [Fact]
    public async Task Enrich_MultipleConflicts_AllDetected()
    {
        var enrichHandler = new EnrichGameMetadataFromBggCommandHandler(
            _bggApiClientMock.Object,
            NullLogger<EnrichGameMetadataFromBggCommandHandler>.Instance);

        var pdfMetadata = new GameMetadataDto
        {
            Title = "PDF Title",
            Year = 2019,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTime = 60,
            MinAge = 8
        };

        _bggApiClientMock
            .Setup(b => b.GetGameDetailsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BggGameDetails
            {
                BggId = 42,
                Title = "BGG Title",
                YearPublished = 2020,
                MinPlayers = 3,
                MaxPlayers = 5,
                PlayingTimeMinutes = 90
            });

        var result = await enrichHandler.Handle(
            new EnrichGameMetadataFromBggCommand(pdfMetadata, 42, _testUserId),
            CancellationToken.None);

        result.BggEnrichmentSucceeded.Should().BeTrue();
        result.Title.Should().Be("BGG Title", "BGG title should be preferred");

        // Year, MinPlayers, MaxPlayers, PlayingTime all differ
        result.Conflicts.Should().HaveCountGreaterThanOrEqualTo(4,
            "Should detect conflicts for Year, MinPlayers, MaxPlayers, PlayingTime");
        result.Conflicts.Should().Contain(c => c.FieldName == "Year");
        result.Conflicts.Should().Contain(c => c.FieldName == "MinPlayers");
        result.Conflicts.Should().Contain(c => c.FieldName == "MaxPlayers");
        result.Conflicts.Should().Contain(c => c.FieldName == "PlayingTime");
    }

    [Fact]
    public async Task Enrich_EmptyPdfMetadata_UsesBggDataOnly()
    {
        var enrichHandler = new EnrichGameMetadataFromBggCommandHandler(
            _bggApiClientMock.Object,
            NullLogger<EnrichGameMetadataFromBggCommandHandler>.Instance);

        var emptyPdfMetadata = new GameMetadataDto();

        _bggApiClientMock
            .Setup(b => b.GetGameDetailsAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BggGameDetails
            {
                BggId = 100,
                Title = "BGG Only Game",
                YearPublished = 2023,
                MinPlayers = 2,
                MaxPlayers = 4,
                PlayingTimeMinutes = 45,
                ComplexityRating = 3.0m,
                AverageRating = 8.1m
            });

        var result = await enrichHandler.Handle(
            new EnrichGameMetadataFromBggCommand(emptyPdfMetadata, 100, _testUserId),
            CancellationToken.None);

        result.BggEnrichmentSucceeded.Should().BeTrue();
        result.Title.Should().Be("BGG Only Game");
        result.Year.Should().Be(2023);
        result.MinPlayers.Should().Be(2);
        result.MaxPlayers.Should().Be(4);
        result.PlayingTime.Should().Be(45);
        result.Conflicts.Should().BeEmpty("No conflicts when only BGG data available");
    }

    [Fact]
    public async Task Enrich_MatchingData_NoConflicts()
    {
        var enrichHandler = new EnrichGameMetadataFromBggCommandHandler(
            _bggApiClientMock.Object,
            NullLogger<EnrichGameMetadataFromBggCommandHandler>.Instance);

        var pdfMetadata = new GameMetadataDto
        {
            Title = "Chess",
            Year = 1475,
            MinPlayers = 2,
            MaxPlayers = 2,
            PlayingTime = 60
        };

        _bggApiClientMock
            .Setup(b => b.GetGameDetailsAsync(171, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BggGameDetails
            {
                BggId = 171,
                Title = "Chess",
                YearPublished = 1475,
                MinPlayers = 2,
                MaxPlayers = 2,
                PlayingTimeMinutes = 60
            });

        var result = await enrichHandler.Handle(
            new EnrichGameMetadataFromBggCommand(pdfMetadata, 171, _testUserId),
            CancellationToken.None);

        result.BggEnrichmentSucceeded.Should().BeTrue();
        result.Conflicts.Should().BeEmpty("Matching data should produce no conflicts");
    }

    #endregion

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