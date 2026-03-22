using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ExtractGameMetadataFromPdfQueryIntegrationTests
{
    private readonly Mock<IBlobStorageService> _blobStorageServiceMock;
    private readonly Mock<IPdfTextExtractor> _pdfTextExtractorMock;
    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly ILogger<ExtractGameMetadataFromPdfQueryHandler> _logger;
    private readonly ExtractGameMetadataFromPdfQueryHandler _handler;

    public ExtractGameMetadataFromPdfQueryIntegrationTests()
    {
        _blobStorageServiceMock = new Mock<IBlobStorageService>();
        _pdfTextExtractorMock = new Mock<IPdfTextExtractor>();
        _llmServiceMock = new Mock<ILlmService>();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<ExtractGameMetadataFromPdfQueryHandler>();

        _handler = new ExtractGameMetadataFromPdfQueryHandler(
            _blobStorageServiceMock.Object,
            _pdfTextExtractorMock.Object,
            _llmServiceMock.Object,
            _logger);
    }

    #region Full Flow Tests

    [Fact]
    public async Task FullFlow_ValidPdfToMetadata_ReturnsCompleteMetadataWithConfidence()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "wingspan_rulebook_20240101.pdf",
            UserId: userId);

        SetupPdfRetrieval(CreateMockPdfStream());
        var rulebookText = CreateWingspanRulebookText();
        SetupTextExtraction(rulebookText, ExtractionQuality.High);
        var expectedMetadata = CreateWingspanMetadata();
        SetupAiParsing(expectedMetadata);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Wingspan");
        result.Year.Should().Be(2019);
        result.MinPlayers.Should().Be(1);
        result.MaxPlayers.Should().Be(5);
        result.PlayingTime.Should().Be(70);
        result.MinAge.Should().Be(10);
        result.Description.Should().Contain("bird enthusiasts");
        result.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.8);

        _blobStorageServiceMock.Verify(
            b => b.RetrieveAsync(It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()),
            Times.Once);
        _pdfTextExtractorMock.Verify(
            p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()),
            Times.Once);
        _llmServiceMock.Verify(
            l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FullFlow_LowQualityScanWithOcr_ReturnsLowerConfidence()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "scanned_manual_456.pdf",
            UserId: Guid.NewGuid());

        SetupPdfRetrieval(CreateMockPdfStream());
        var poorQualityText = "GAME NAME\nPlayers 2 4\nAge 8+\nBasic game...";
        SetupTextExtraction(poorQualityText, ExtractionQuality.Low);

        var partialMetadata = new GameMetadataDto
        {
            Title = "GAME NAME",
            MinPlayers = 2,
            MaxPlayers = 4,
            MinAge = 8,
            Year = null,
            PlayingTime = null,
            Description = null,
            ConfidenceScore = 0.0
        };
        SetupAiParsing(partialMetadata);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("GAME NAME");
        result.ConfidenceScore.Should().BeLessThan(0.6);
        result.Year.Should().BeNull();
        result.PlayingTime.Should().BeNull();
    }

    [Fact]
    public async Task FullFlow_MissingFieldsInPdf_ReturnsPartialMetadata()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "incomplete_rulebook.pdf",
            UserId: Guid.NewGuid());

        SetupPdfRetrieval(CreateMockPdfStream());

        var incompleteText = "Mystery Quest\nA strategic adventure game\n\nPlayers: 2-6\nDifficulty: Advanced";
        SetupTextExtraction(incompleteText, ExtractionQuality.Medium);

        var metadataWithGaps = new GameMetadataDto
        {
            Title = "Mystery Quest",
            MinPlayers = 2,
            MaxPlayers = 6,
            Description = "A strategic adventure game",
            Year = null,
            PlayingTime = null,
            MinAge = null,
            ConfidenceScore = 0.0
        };
        SetupAiParsing(metadataWithGaps);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Mystery Quest");
        result.Year.Should().BeNull();
        result.PlayingTime.Should().BeNull();
        result.MinAge.Should().BeNull();
        result.ConfidenceScore.Should().BeInRange(0.4, 0.7);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ErrorHandling_StorageFailure_ReturnsEmptyMetadata()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "storage_error.pdf",
            UserId: Guid.NewGuid());

        _blobStorageServiceMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("S3 service unavailable"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(GameMetadataDto.CreateEmpty());
        _pdfTextExtractorMock.Verify(
            p => p.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ErrorHandling_SmolDoclingFailure_ReturnsEmptyMetadata()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "extraction_error.pdf",
            UserId: Guid.NewGuid());

        SetupPdfRetrieval(CreateMockPdfStream());

        _pdfTextExtractorMock
            .Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("SmolDocling service timeout"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(GameMetadataDto.CreateEmpty());
        _llmServiceMock.Verify(
            l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ErrorHandling_LlmServiceFailure_ReturnsEmptyMetadata()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "llm_error.pdf",
            UserId: Guid.NewGuid());

        SetupPdfRetrieval(CreateMockPdfStream());
        SetupTextExtraction("Valid rulebook text", ExtractionQuality.High);

        _llmServiceMock
            .Setup(l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("OpenRouter API timeout"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(GameMetadataDto.CreateEmpty());
    }

    #endregion

    #region Timeout and Cancellation Tests

    [Fact]
    public async Task Timeout_LongRunningOperation_CompletesWithinReasonableTime()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "large_rulebook.pdf",
            UserId: Guid.NewGuid());

        SetupPdfRetrieval(CreateMockPdfStream());
        var largeText = new string('x', 7000);
        SetupTextExtraction(largeText, ExtractionQuality.Medium);
        SetupAiParsing(CreateWingspanMetadata());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        var result = await _handler.Handle(query, cts.Token);

        // Assert
        result.Should().NotBeNull();
        cts.Token.IsCancellationRequested.Should().BeFalse("operation completed before timeout");
    }

    [Fact]
    public async Task Cancellation_CancellationTokenPassedThrough_VerifiedByMock()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "fileId_timestamp.pdf",
            UserId: Guid.NewGuid());

        using var cts = new CancellationTokenSource();
        var tokenReceived = false;

        _blobStorageServiceMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, _, ct) =>
            {
                tokenReceived = ct == cts.Token;
            })
            .ReturnsAsync(CreateMockPdfStream());

        SetupTextExtraction("Test text", ExtractionQuality.High);
        SetupAiParsing(CreateWingspanMetadata());

        // Act
        var result = await _handler.Handle(query, cts.Token);

        // Assert
        tokenReceived.Should().BeTrue("cancellation token should be passed through to services");
        result.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private void SetupPdfRetrieval(Stream pdfStream)
    {
        _blobStorageServiceMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => pdfStream);
    }

    private void SetupTextExtraction(string text, ExtractionQuality quality)
    {
        _pdfTextExtractorMock
            .Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.CreateSuccess(
                extractedText: text,
                pageCount: 10,
                characterCount: text.Length,
                ocrTriggered: quality <= ExtractionQuality.Low,
                quality: quality));
    }

    private void SetupAiParsing(GameMetadataDto metadata)
    {
        _llmServiceMock
            .Setup(l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);
    }

    private static Stream CreateMockPdfStream()
    {
        var pdfContent = "%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\nxref\n0 1\ntrailer<</Size 1>>\nstartxref\n%%EOF";
        var pdfBytes = System.Text.Encoding.ASCII.GetBytes(pdfContent);
        return new MemoryStream(pdfBytes);
    }

    private static string CreateWingspanRulebookText()
    {
        return "WINGSPAN\nA competitive, medium-weight, card-driven, engine-building board game\n\n" +
               "Players: 1-5\nPlaying Time: 40-70 minutes\nAge: 10+\nYear: 2019\n\n" +
               "In Wingspan, you are bird enthusiasts researchers, bird watchers, ornithologists, " +
               "and collectors seeking to discover and attract the best birds to your network of " +
               "wildlife preserves. Each bird extends a chain of powerful combinations in one of your habitats.";
    }

    private static GameMetadataDto CreateWingspanMetadata()
    {
        return new GameMetadataDto
        {
            Title = "Wingspan",
            Year = 2019,
            MinPlayers = 1,
            MaxPlayers = 5,
            PlayingTime = 70,
            MinAge = 10,
            Description = "In Wingspan, you are bird enthusiasts researchers, bird watchers, ornithologists, and collectors seeking to discover and attract the best birds to your network of wildlife preserves.",
            ConfidenceScore = 0.0
        };
    }

    #endregion
}
