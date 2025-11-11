using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Services;

public class PdfTextProcessingDomainServiceTests
{
    private readonly PdfTextProcessingDomainService _sut;
    private readonly IConfiguration _configuration;

    public PdfTextProcessingDomainServiceTests()
    {
        // Setup configuration with default threshold
        var configData = new Dictionary<string, string?>
        {
            { "PdfExtraction:Ocr:ThresholdCharsPerPage", "100" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _sut = new PdfTextProcessingDomainService(_configuration);
    }

    #region ShouldTriggerOcr Tests

    [Fact]
    public void ShouldTriggerOcr_BelowThreshold_ReturnsTrue()
    {
        // Arrange: 10 chars / 2 pages = 5 chars/page (< 100 threshold)
        var extractedText = "short text";
        var pageCount = 2;

        // Act
        var result = _sut.ShouldTriggerOcr(extractedText, pageCount);

        // Assert
        Assert.True(result, "Should trigger OCR when chars/page < threshold");
    }

    [Fact]
    public void ShouldTriggerOcr_AboveThreshold_ReturnsFalse()
    {
        // Arrange: 1000 chars / 5 pages = 200 chars/page (> 100 threshold)
        var extractedText = new string('a', 1000);
        var pageCount = 5;

        // Act
        var result = _sut.ShouldTriggerOcr(extractedText, pageCount);

        // Assert
        Assert.False(result, "Should not trigger OCR when chars/page >= threshold");
    }

    [Fact]
    public void ShouldTriggerOcr_ZeroPages_ReturnsFalse()
    {
        // Arrange
        var extractedText = "some text";
        var pageCount = 0;

        // Act
        var result = _sut.ShouldTriggerOcr(extractedText, pageCount);

        // Assert
        Assert.False(result, "Should not trigger OCR for zero pages (invalid input)");
    }

    [Fact]
    public void ShouldTriggerOcr_EmptyText_ReturnsTrue()
    {
        // Arrange
        var extractedText = "";
        var pageCount = 5;

        // Act
        var result = _sut.ShouldTriggerOcr(extractedText, pageCount);

        // Assert
        Assert.True(result, "Should trigger OCR for empty text (likely scanned PDF)");
    }

    #endregion

    #region NormalizeText Tests

    [Fact]
    public void NormalizeText_EmptyString_ReturnsEmpty()
    {
        // Act
        var result = _sut.NormalizeText("");

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void NormalizeText_NormalizesLineEndings()
    {
        // Arrange: Mixed line endings
        var rawText = "Line1\r\nLine2\rLine3\nLine4";

        // Act
        var result = _sut.NormalizeText(rawText);

        // Assert
        Assert.DoesNotContain("\r", result);
        Assert.Contains("Line1\nLine2\nLine3\nLine4", result);
    }

    [Fact]
    public void NormalizeText_RemovesExcessiveWhitespace()
    {
        // Arrange: Multiple spaces and tabs
        var rawText = "Word1    Word2\t\tWord3";

        // Act
        var result = _sut.NormalizeText(rawText);

        // Assert
        Assert.Equal("Word1 Word2 Word3", result);
    }

    [Fact]
    public void NormalizeText_FixesBrokenParagraphs()
    {
        // Arrange: Line ends mid-sentence
        var rawText = "This is a sentenc\ne that was split";

        // Act
        var result = _sut.NormalizeText(rawText);

        // Assert
        Assert.Contains("sentence that was split", result);
    }

    [Fact]
    public void NormalizeText_NormalizesMultipleNewlines()
    {
        // Arrange: Multiple consecutive newlines
        var rawText = "Paragraph1\n\n\n\nParagraph2";

        // Act
        var result = _sut.NormalizeText(rawText);

        // Assert
        Assert.Contains("Paragraph1\n\nParagraph2", result);
        Assert.DoesNotContain("\n\n\n", result);
    }

    [Fact]
    public void NormalizeText_RemovesZeroWidthCharacters()
    {
        // Arrange: Zero-width space (U+200B)
        var rawText = "Word1\u200BWord2";

        // Act
        var result = _sut.NormalizeText(rawText);

        // Assert
        Assert.DoesNotContain("\u200B", result);
        Assert.Contains("Word1Word2", result);
    }

    [Fact]
    public void NormalizeText_PerformsUnicodeNormalization()
    {
        // Arrange: Decomposed unicode (é as e + combining accent)
        var rawText = "cafe\u0301"; // café with combining accent

        // Act
        var result = _sut.NormalizeText(rawText);

        // Assert: Should normalize to composed form (NFC)
        Assert.Equal("café", result);
    }

    #endregion

    #region AssessQuality Tests

    [Fact]
    public void AssessQuality_VeryLowQuality_ReturnsVeryLow()
    {
        // Arrange: 20 chars / 2 pages = 10 chars/page (< 50)
        var text = "Very short PDF text.";
        var pageCount = 2;

        // Act
        var result = _sut.AssessQuality(text, pageCount);

        // Assert
        Assert.Equal(ExtractionQuality.VeryLow, result);
    }

    [Fact]
    public void AssessQuality_LowQuality_ReturnsLow()
    {
        // Arrange: 200 chars / 2 pages = 100 chars/page (50-200)
        var text = new string('a', 200);
        var pageCount = 2;

        // Act
        var result = _sut.AssessQuality(text, pageCount);

        // Assert
        Assert.Equal(ExtractionQuality.Low, result);
    }

    [Fact]
    public void AssessQuality_MediumQuality_ReturnsMedium()
    {
        // Arrange: 1000 chars / 2 pages = 500 chars/page (200-1000)
        var text = new string('a', 1000);
        var pageCount = 2;

        // Act
        var result = _sut.AssessQuality(text, pageCount);

        // Assert
        Assert.Equal(ExtractionQuality.Medium, result);
    }

    [Fact]
    public void AssessQuality_HighQuality_ReturnsHigh()
    {
        // Arrange: 5000 chars / 2 pages = 2500 chars/page (> 1000)
        var text = new string('a', 5000);
        var pageCount = 2;

        // Act
        var result = _sut.AssessQuality(text, pageCount);

        // Assert
        Assert.Equal(ExtractionQuality.High, result);
    }

    [Fact]
    public void AssessQuality_ZeroPages_ReturnsVeryLow()
    {
        // Arrange
        var text = "some text";
        var pageCount = 0;

        // Act
        var result = _sut.AssessQuality(text, pageCount);

        // Assert
        Assert.Equal(ExtractionQuality.VeryLow, result);
    }

    [Fact]
    public void AssessQuality_EmptyText_ReturnsVeryLow()
    {
        // Arrange
        var text = "";
        var pageCount = 5;

        // Act
        var result = _sut.AssessQuality(text, pageCount);

        // Assert
        Assert.Equal(ExtractionQuality.VeryLow, result);
    }

    #endregion
}
