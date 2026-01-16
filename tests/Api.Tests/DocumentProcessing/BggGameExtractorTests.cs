using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.DocumentProcessing;

public sealed class BggGameExtractorTests : IAsyncLifetime
{
    private readonly Mock<IPdfTextExtractor> _pdfTextExtractorMock;
    private readonly Mock<ILogger<BggGameExtractor>> _loggerMock;
    private readonly BggGameExtractor _extractor;
    private readonly string _testPdfPath;

    public BggGameExtractorTests()
    {
        _pdfTextExtractorMock = new Mock<IPdfTextExtractor>();
        _loggerMock = new Mock<ILogger<BggGameExtractor>>();
        _extractor = new BggGameExtractor(_pdfTextExtractorMock.Object, _loggerMock.Object);

        // Create temporary test PDF file
        _testPdfPath = Path.Combine(Path.GetTempPath(), $"test_bgg_{Guid.NewGuid()}.pdf");
    }

    public Task InitializeAsync()
    {
        // Create empty PDF file for testing
        File.WriteAllText(_testPdfPath, "dummy PDF content");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // Cleanup test file
        if (File.Exists(_testPdfPath))
        {
            File.Delete(_testPdfPath);
        }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ExtractGamesAsync_WithValidCsvFormat_ShouldReturnGamesList()
    {
        // Arrange
        var mockPdfText = """
            Nome Gioco,ID BGG
            Brass: Birmingham,224517
            Gloomhaven,174430
            Wingspan,266192
            """;

        _pdfTextExtractorMock
            .Setup(x => x.ExtractTextAsync(
                It.IsAny<Stream>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TextExtractionResult(
                Success: true,
                ExtractedText: mockPdfText,
                PageCount: 1,
                CharacterCount: mockPdfText.Length,
                OcrTriggered: false,
                Quality: ExtractionQuality.VeryHigh,
                ErrorMessage: null));

        // Act
        var result = await _extractor.ExtractGamesAsync(_testPdfPath, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().BeEquivalentTo(new BggGameDto("Brass: Birmingham", 224517));
        result[1].Should().BeEquivalentTo(new BggGameDto("Gloomhaven", 174430));
        result[2].Should().BeEquivalentTo(new BggGameDto("Wingspan", 266192));
    }

    [Fact]
    public async Task ExtractGamesAsync_WithMixedContent_ShouldFilterValidLines()
    {
        // Arrange
        var mockPdfText = """
            Ecco le due liste richieste:
            Nome Gioco,ID BGG
            Codenames,178900
            Some random text that doesn't match CSV format
            7 Wonders,68448
            Invalid line without comma
            Splendor,148228
            Footer text
            """;

        _pdfTextExtractorMock
            .Setup(x => x.ExtractTextAsync(
                It.IsAny<Stream>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TextExtractionResult(
                Success: true,
                ExtractedText: mockPdfText,
                PageCount: 1,
                CharacterCount: mockPdfText.Length,
                OcrTriggered: false,
                Quality: ExtractionQuality.High,
                ErrorMessage: null));

        // Act
        var result = await _extractor.ExtractGamesAsync(_testPdfPath);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(g => g.GameName == "Codenames" && g.BggId == 178900);
        result.Should().Contain(g => g.GameName == "7 Wonders" && g.BggId == 68448);
        result.Should().Contain(g => g.GameName == "Splendor" && g.BggId == 148228);
    }

    [Fact]
    public async Task ExtractGamesAsync_WithInvalidBggIds_ShouldSkipInvalidEntries()
    {
        // Arrange
        var mockPdfText = """
            Valid Game,123456
            Invalid ID,abc123
            Negative ID,-999
            Zero ID,0
            Valid Game 2,789012
            """;

        _pdfTextExtractorMock
            .Setup(x => x.ExtractTextAsync(
                It.IsAny<Stream>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TextExtractionResult(
                Success: true,
                ExtractedText: mockPdfText,
                PageCount: 1,
                CharacterCount: mockPdfText.Length,
                OcrTriggered: false,
                Quality: ExtractionQuality.VeryHigh,
                ErrorMessage: null));

        // Act
        var result = await _extractor.ExtractGamesAsync(_testPdfPath);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(g => g.GameName == "Valid Game" && g.BggId == 123456);
        result.Should().Contain(g => g.GameName == "Valid Game 2" && g.BggId == 789012);
    }

    [Fact]
    public async Task ExtractGamesAsync_WithShortGameNames_ShouldSkipNamesLessThan2Chars()
    {
        // Arrange
        var mockPdfText = """
            A,123456
            Go,188
            XYZ,789012
            """;

        _pdfTextExtractorMock
            .Setup(x => x.ExtractTextAsync(
                It.IsAny<Stream>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TextExtractionResult(
                Success: true,
                ExtractedText: mockPdfText,
                PageCount: 1,
                CharacterCount: mockPdfText.Length,
                OcrTriggered: false,
                Quality: ExtractionQuality.VeryHigh,
                ErrorMessage: null));

        // Act
        var result = await _extractor.ExtractGamesAsync(_testPdfPath);

        // Assert
        result.Should().HaveCount(2);
        result.Should().NotContain(g => g.GameName == "A");
        result.Should().Contain(g => g.GameName == "Go" && g.BggId == 188);
        result.Should().Contain(g => g.GameName == "XYZ" && g.BggId == 789012);
    }

    [Fact]
    public async Task ExtractGamesAsync_WithSpecialCharactersInGameName_ShouldHandleCorrectly()
    {
        // Arrange
        var mockPdfText = """
            The Lord of the Rings: The Card Game,421006
            7 Wonders Duel,173346
            Clank!: A Deck-Building Adventure,201808
            """;

        _pdfTextExtractorMock
            .Setup(x => x.ExtractTextAsync(
                It.IsAny<Stream>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TextExtractionResult(
                Success: true,
                ExtractedText: mockPdfText,
                PageCount: 1,
                CharacterCount: mockPdfText.Length,
                OcrTriggered: false,
                Quality: ExtractionQuality.VeryHigh,
                ErrorMessage: null));

        // Act
        var result = await _extractor.ExtractGamesAsync(_testPdfPath);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(g => g.GameName == "The Lord of the Rings: The Card Game" && g.BggId == 421006);
        result.Should().Contain(g => g.GameName == "7 Wonders Duel" && g.BggId == 173346);
        result.Should().Contain(g => g.GameName == "Clank!: A Deck-Building Adventure" && g.BggId == 201808);
    }

    [Fact]
    public async Task ExtractGamesAsync_WhenPdfExtractionFails_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _pdfTextExtractorMock
            .Setup(x => x.ExtractTextAsync(
                It.IsAny<Stream>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TextExtractionResult(
                Success: false,
                ExtractedText: string.Empty,
                PageCount: 0,
                CharacterCount: 0,
                OcrTriggered: false,
                Quality: ExtractionQuality.VeryLow,
                ErrorMessage: "PDF extraction failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _extractor.ExtractGamesAsync(_testPdfPath));
    }

    [Fact]
    public async Task ExtractGamesAsync_WhenPdfFileNotFound_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.pdf");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _extractor.ExtractGamesAsync(nonExistentPath));
    }

    [Fact]
    public async Task ExtractGamesAsync_WithNullOrEmptyPath_ShouldThrowArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _extractor.ExtractGamesAsync(null!));

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _extractor.ExtractGamesAsync(string.Empty));
    }

    [Fact]
    public async Task ExtractGamesAsync_WithEmptyPdfText_ShouldReturnEmptyList()
    {
        // Arrange
        _pdfTextExtractorMock
            .Setup(x => x.ExtractTextAsync(
                It.IsAny<Stream>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TextExtractionResult(
                Success: true,
                ExtractedText: string.Empty,
                PageCount: 0,
                CharacterCount: 0,
                OcrTriggered: false,
                Quality: ExtractionQuality.High,
                ErrorMessage: null));

        // Act
        var result = await _extractor.ExtractGamesAsync(_testPdfPath);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractGamesAsync_WithWhitespaceOnlyPdfText_ShouldReturnEmptyList()
    {
        // Arrange
        _pdfTextExtractorMock
            .Setup(x => x.ExtractTextAsync(
                It.IsAny<Stream>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TextExtractionResult(
                Success: true,
                ExtractedText: "   \n\n\t   ",
                PageCount: 1,
                CharacterCount: 7,
                OcrTriggered: false,
                Quality: ExtractionQuality.Medium,
                ErrorMessage: null));

        // Act
        var result = await _extractor.ExtractGamesAsync(_testPdfPath);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractGamesAsync_ShouldTrimWhitespaceFromGameNames()
    {
        // Arrange
        var mockPdfText = """
            Brass: Birmingham  ,224517
              Gloomhaven,174430
            Wingspan  ,266192
            """;

        _pdfTextExtractorMock
            .Setup(x => x.ExtractTextAsync(
                It.IsAny<Stream>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TextExtractionResult(
                Success: true,
                ExtractedText: mockPdfText,
                PageCount: 1,
                CharacterCount: mockPdfText.Length,
                OcrTriggered: false,
                Quality: ExtractionQuality.High,
                ErrorMessage: null));

        // Act
        var result = await _extractor.ExtractGamesAsync(_testPdfPath);

        // Assert
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(g => g.GameName.Should().NotStartWith(" ").And.NotEndWith(" "));
    }
}
