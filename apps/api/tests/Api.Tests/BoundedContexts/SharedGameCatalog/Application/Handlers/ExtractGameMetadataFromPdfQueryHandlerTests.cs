using System.Text.Json;
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

/// <summary>
/// Unit tests for ExtractGameMetadataFromPdfQueryHandler.
/// Tests metadata extraction from PDF using SmolDocling OCR + AI parsing.
/// Issue #4155: Extract Game Metadata Query
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ExtractGameMetadataFromPdfQueryHandlerTests
{
    private readonly Mock<IBlobStorageService> _blobStorageServiceMock;
    private readonly Mock<IPdfTextExtractor> _pdfTextExtractorMock;
    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly Mock<ILogger<ExtractGameMetadataFromPdfQueryHandler>> _loggerMock;
    private readonly ExtractGameMetadataFromPdfQueryHandler _handler;

    public ExtractGameMetadataFromPdfQueryHandlerTests()
    {
        _blobStorageServiceMock = new Mock<IBlobStorageService>();
        _pdfTextExtractorMock = new Mock<IPdfTextExtractor>();
        _llmServiceMock = new Mock<ILlmService>();
        _loggerMock = new Mock<ILogger<ExtractGameMetadataFromPdfQueryHandler>>();

        _handler = new ExtractGameMetadataFromPdfQueryHandler(
            _blobStorageServiceMock.Object,
            _pdfTextExtractorMock.Object,
            _llmServiceMock.Object,
            _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var handler = new ExtractGameMetadataFromPdfQueryHandler(
            _blobStorageServiceMock.Object,
            _pdfTextExtractorMock.Object,
            _llmServiceMock.Object,
            _loggerMock.Object);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullBlobStorageService_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new ExtractGameMetadataFromPdfQueryHandler(
            null!,
            _pdfTextExtractorMock.Object,
            _llmServiceMock.Object,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("blobStorageService");
    }

    [Fact]
    public void Constructor_WithNullPdfTextExtractor_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new ExtractGameMetadataFromPdfQueryHandler(
            _blobStorageServiceMock.Object,
            null!,
            _llmServiceMock.Object,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("pdfTextExtractor");
    }

    [Fact]
    public void Constructor_WithNullLlmService_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new ExtractGameMetadataFromPdfQueryHandler(
            _blobStorageServiceMock.Object,
            _pdfTextExtractorMock.Object,
            null!,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("llmService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new ExtractGameMetadataFromPdfQueryHandler(
            _blobStorageServiceMock.Object,
            _pdfTextExtractorMock.Object,
            _llmServiceMock.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Handle - Success Scenarios

    [Fact]
    public async Task Handle_WithValidPdfAndFullMetadata_ReturnsSuccessWithHighConfidence()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "abc123_1234567890.pdf",
            UserId: userId);

        SetupSuccessfulExtraction(
            extractedText: CreateRulebookText(complete: true),
            quality: ExtractionQuality.High,
            metadata: CreateFullMetadata());

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
        result.Description.Should().NotBeNullOrEmpty();
        result.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.8);
    }

    [Fact]
    public async Task Handle_WithMediumQualityAndPartialMetadata_ReturnsMediumConfidence()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "game_timestamp.pdf",
            UserId: Guid.NewGuid());

        SetupSuccessfulExtraction(
            extractedText: CreateRulebookText(complete: false),
            quality: ExtractionQuality.Medium,
            metadata: CreatePartialMetadata());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().NotBeNullOrEmpty();
        result.ConfidenceScore.Should().BeInRange(0.4, 0.7);
    }

    [Fact]
    public async Task Handle_WithLowQualityExtraction_ReturnsLowConfidence()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "scan_poor.pdf",
            UserId: Guid.NewGuid());

        SetupSuccessfulExtraction(
            extractedText: "Game Title... players... age...",
            quality: ExtractionQuality.Low,
            metadata: new GameMetadataDto { Title = "Game Title", ConfidenceScore = 0.0 });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ConfidenceScore.Should().BeLessThan(0.5);
    }

    #endregion

    #region Handle - PDF Retrieval Failures

    [Fact]
    public async Task Handle_WhenPdfNotFoundInStorage_ReturnsEmptyMetadata()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "missing_file.pdf",
            UserId: Guid.NewGuid());

        _blobStorageServiceMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(GameMetadataDto.CreateEmpty());
        _pdfTextExtractorMock.Verify(
            p => p.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenStorageThrowsException_ReturnsEmptyMetadata()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "file_error.pdf",
            UserId: Guid.NewGuid());

        _blobStorageServiceMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("S3 connection timeout"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(GameMetadataDto.CreateEmpty());
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to retrieve PDF")),
                It.IsAny<IOException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidFilePathFormat_ReturnsEmptyMetadata()
    {
        // Arrange - FilePath without valid fileId (empty after extraction)
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "",
            UserId: Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - Empty path should return empty metadata
        result.Should().BeEquivalentTo(GameMetadataDto.CreateEmpty());
    }

    #endregion

    #region Handle - Text Extraction Failures

    [Fact]
    public async Task Handle_WhenTextExtractionFails_ReturnsEmptyMetadata()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "corrupted_123.pdf",
            UserId: Guid.NewGuid());

        SetupPdfRetrieval();

        _pdfTextExtractorMock
            .Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.CreateFailure("PDF is corrupted"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(GameMetadataDto.CreateEmpty());
        _llmServiceMock.Verify(
            l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenExtractedTextIsEmpty_ReturnsEmptyMetadata()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "empty_456.pdf",
            UserId: Guid.NewGuid());

        SetupPdfRetrieval();

        _pdfTextExtractorMock
            .Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.CreateSuccess(
                extractedText: string.Empty,
                pageCount: 1,
                characterCount: 0,
                ocrTriggered: false,
                quality: ExtractionQuality.VeryLow));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(GameMetadataDto.CreateEmpty());
    }

    [Fact]
    public async Task Handle_WhenExtractionThrowsException_ReturnsEmptyMetadata()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "exception_789.pdf",
            UserId: Guid.NewGuid());

        SetupPdfRetrieval();

        _pdfTextExtractorMock
            .Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SmolDocling service unavailable"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(GameMetadataDto.CreateEmpty());
    }

    #endregion

    #region Handle - AI Parsing Failures

    [Fact]
    public async Task Handle_WhenAiReturnsNull_ReturnsEmptyMetadata()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "ai_null_111.pdf",
            UserId: Guid.NewGuid());

        SetupPdfRetrieval();
        SetupTextExtraction(CreateRulebookText(true), ExtractionQuality.High);

        _llmServiceMock
            .Setup(l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameMetadataDto?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(GameMetadataDto.CreateEmpty());
    }

    [Fact]
    public async Task Handle_WhenAiThrowsJsonException_ReturnsEmptyMetadata()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "json_error_222.pdf",
            UserId: Guid.NewGuid());

        SetupPdfRetrieval();
        SetupTextExtraction(CreateRulebookText(true), ExtractionQuality.High);

        _llmServiceMock
            .Setup(l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new JsonException("Invalid JSON format"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(GameMetadataDto.CreateEmpty());
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to deserialize AI response")),
                It.IsAny<JsonException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAiThrowsGeneralException_ReturnsEmptyMetadata()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "ai_crash_333.pdf",
            UserId: Guid.NewGuid());

        SetupPdfRetrieval();
        SetupTextExtraction(CreateRulebookText(true), ExtractionQuality.High);

        _llmServiceMock
            .Setup(l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM service timeout"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(GameMetadataDto.CreateEmpty());
    }

    #endregion

    #region Handle - Confidence Scoring

    [Theory]
    [InlineData(ExtractionQuality.High, 7, 0.75, 1.0)] // High quality + all fields
    [InlineData(ExtractionQuality.Medium, 7, 0.625, 0.875)] // Medium quality + all fields
    [InlineData(ExtractionQuality.Low, 4, 0.375, 0.625)] // Low quality + 4 fields
    [InlineData(ExtractionQuality.VeryLow, 1, 0.125, 0.25)] // VeryLow + 1 field
    public async Task Handle_CalculatesCorrectConfidenceScore(
        ExtractionQuality quality,
        int populatedFields,
        double minExpectedConfidence,
        double maxExpectedConfidence)
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "confidence_test.pdf",
            UserId: Guid.NewGuid());

        var metadata = CreateMetadataWithFields(populatedFields);

        SetupSuccessfulExtraction(
            extractedText: CreateRulebookText(true),
            quality: quality,
            metadata: metadata);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ConfidenceScore.Should().BeInRange(minExpectedConfidence, maxExpectedConfidence);
    }

    [Fact]
    public async Task Handle_WithAllFieldsNull_ReturnsZeroConfidenceFromFieldCompleteness()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "all_null.pdf",
            UserId: Guid.NewGuid());

        var metadata = new GameMetadataDto
        {
            Title = null,
            Year = null,
            MinPlayers = null,
            MaxPlayers = null,
            PlayingTime = null,
            MinAge = null,
            Description = null,
            ConfidenceScore = 0.0
        };

        SetupSuccessfulExtraction(
            extractedText: "Some text",
            quality: ExtractionQuality.High,
            metadata: metadata);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - Zero fields populated = zero confidence (even with High quality OCR)
        result.ConfidenceScore.Should().Be(0.0);
    }

    #endregion

    #region Handle - Text Truncation

    [Fact]
    public async Task Handle_WithLongText_TruncatesTo8000Characters()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "long_manual.pdf",
            UserId: Guid.NewGuid());

        var longText = new string('x', 10000);
        string? capturedUserPrompt = null;

        SetupPdfRetrieval();
        SetupTextExtraction(longText, ExtractionQuality.High);

        _llmServiceMock
            .Setup(l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((sys, user, ct) =>
            {
                capturedUserPrompt = user;
            })
            .ReturnsAsync(CreateFullMetadata());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        capturedUserPrompt.Should().NotBeNull();
        capturedUserPrompt.Should().Contain("...[truncated]");
        capturedUserPrompt!.Length.Should().BeLessThan(longText.Length);
    }

    [Fact]
    public async Task Handle_WithShortText_DoesNotTruncate()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "short_rules.pdf",
            UserId: Guid.NewGuid());

        var shortText = "Short rulebook content with game details.";
        string? capturedUserPrompt = null;

        SetupPdfRetrieval();
        SetupTextExtraction(shortText, ExtractionQuality.High);

        _llmServiceMock
            .Setup(l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((sys, user, ct) =>
            {
                capturedUserPrompt = user;
            })
            .ReturnsAsync(CreateFullMetadata());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        capturedUserPrompt.Should().NotBeNull();
        capturedUserPrompt.Should().NotContain("[truncated]");
        capturedUserPrompt.Should().Contain(shortText);
    }

    #endregion

    #region Handle - Cancellation

    [Fact]
    public async Task Handle_WhenCancellationTokenPassed_PassesToDownstreamServices()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "fileId_timestamp.pdf",
            UserId: Guid.NewGuid());

        using var cts = new CancellationTokenSource();
        var tokenPassed = false;

        _blobStorageServiceMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, _, ct) =>
            {
                tokenPassed = ct == cts.Token;
            })
            .ReturnsAsync(() => CreateMockPdfStream());

        SetupTextExtraction("test text", ExtractionQuality.High);
        SetupAiParsing(CreateFullMetadata());

        // Act
        var result = await _handler.Handle(query, cts.Token);

        // Assert
        tokenPassed.Should().BeTrue("cancellation token should be passed to blob storage service");
        result.Should().NotBeNull();
    }

    private static MemoryStream CreateMockPdfStream()
    {
        var pdfContent = "%PDF-1.4\ntest content\n%%EOF";
        var pdfBytes = System.Text.Encoding.ASCII.GetBytes(pdfContent);
        return new MemoryStream(pdfBytes);
    }

    #endregion

    #region Handle - Edge Cases

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = async () => await _handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_GracefulDegradation_ReturnsEmptyOnUnexpectedException()
    {
        // Arrange
        var query = new ExtractGameMetadataFromPdfQuery(
            FilePath: "crash_test.pdf",
            UserId: Guid.NewGuid());

        _blobStorageServiceMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated crash"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(GameMetadataDto.CreateEmpty());
        result.ConfidenceScore.Should().Be(0.0);
    }

    #endregion

    #region Helper Methods

    private void SetupSuccessfulExtraction(
        string extractedText,
        ExtractionQuality quality,
        GameMetadataDto metadata)
    {
        SetupPdfRetrieval();
        SetupTextExtraction(extractedText, quality);
        SetupAiParsing(metadata);
    }

    private void SetupPdfRetrieval()
    {
        _blobStorageServiceMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var pdfContent = "%PDF-1.4\ntest content\n%%EOF";
                var pdfBytes = System.Text.Encoding.ASCII.GetBytes(pdfContent);
                return new MemoryStream(pdfBytes);
            });
    }

    private void SetupTextExtraction(string text, ExtractionQuality quality)
    {
        _pdfTextExtractorMock
            .Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.CreateSuccess(
                extractedText: text,
                pageCount: 10,
                characterCount: text.Length,
                ocrTriggered: quality == ExtractionQuality.Low,
                quality: quality));
    }

    private void SetupAiParsing(GameMetadataDto metadata)
    {
        _llmServiceMock
            .Setup(l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);
    }

    private static GameMetadataDto CreateFullMetadata()
    {
        return new GameMetadataDto
        {
            Title = "Wingspan",
            Year = 2019,
            MinPlayers = 1,
            MaxPlayers = 5,
            PlayingTime = 70,
            MinAge = 10,
            Description = "A competitive, medium-weight, card-driven, engine-building board game from Stonemaier Games.",
            ConfidenceScore = 0.0
        };
    }

    private static GameMetadataDto CreatePartialMetadata()
    {
        return new GameMetadataDto
        {
            Title = "Mystery Game",
            Year = 2020,
            MinPlayers = 2,
            MaxPlayers = null,
            PlayingTime = null,
            MinAge = null,
            Description = null,
            ConfidenceScore = 0.0
        };
    }

    private static GameMetadataDto CreateMetadataWithFields(int fieldCount)
    {
        var metadata = new GameMetadataDto
        {
            Title = fieldCount >= 1 ? "Test Game" : null,
            Year = fieldCount >= 2 ? 2024 : null,
            MinPlayers = fieldCount >= 3 ? 2 : null,
            MaxPlayers = fieldCount >= 4 ? 4 : null,
            PlayingTime = fieldCount >= 5 ? 60 : null,
            MinAge = fieldCount >= 6 ? 12 : null,
            Description = fieldCount >= 7 ? "A test game description." : null,
            ConfidenceScore = 0.0
        };

        return metadata;
    }

    private static string CreateRulebookText(bool complete)
    {
        if (complete)
        {
            return """
                WINGSPAN
                A competitive, medium-weight, card-driven, engine-building board game

                Players: 1-5
                Playing Time: 40-70 minutes
                Age: 10+
                Year: 2019

                In Wingspan, you are bird enthusiasts researchers, bird watchers, ornithologists,
                and collectors seeking to discover and attract the best birds to your network of
                wildlife preserves. Each bird extends a chain of powerful combinations in one of
                your habitats.
                """;
        }
        else
        {
            return """
                GAME TITLE
                Players: 2-4
                Age: 8+

                Basic game description...
                """;
        }
    }

    #endregion
}
