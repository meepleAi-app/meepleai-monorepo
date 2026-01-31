using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Microsoft.Extensions.Configuration;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// Tests for PdfTextProcessingDomainService
/// ISSUE-1818: Migrated to FluentAssertions
/// </summary>
[Trait("Category", TestCategories.Unit)]
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
    [Fact]
    public void ShouldTriggerOcr_BelowThreshold_ReturnsTrue()
    {
        // Arrange: 10 chars / 2 pages = 5 chars/page (< 100 threshold)
        var extractedText = "short text";
        var pageCount = 2;

        // Act
        var result = _sut.ShouldTriggerOcr(extractedText, pageCount);

        // Assert
        result.Should().BeTrue("Should trigger OCR when chars/page < threshold");
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
        result.Should().BeFalse("Should not trigger OCR when chars/page >= threshold");
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
        result.Should().BeFalse("Should not trigger OCR for zero pages (invalid input)");
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
        result.Should().BeTrue("Should trigger OCR for empty text (likely scanned PDF)");
    }
    [Fact]
    public void NormalizeText_EmptyString_ReturnsEmpty()
    {
        // Act
        var result = PdfTextProcessingDomainService.NormalizeText("");

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void NormalizeText_NormalizesLineEndings()
    {
        // Arrange: Mixed line endings
        var rawText = "Line1\r\nLine2\rLine3\nLine4";

        // Act
        var result = PdfTextProcessingDomainService.NormalizeText(rawText);

        // Assert
        result.Should().NotContain("\r");
        result.Should().Contain("Line1\nLine2\nLine3\nLine4");
    }

    [Fact]
    public void NormalizeText_RemovesExcessiveWhitespace()
    {
        // Arrange: Multiple spaces and tabs
        var rawText = "Word1    Word2\t\tWord3";

        // Act
        var result = PdfTextProcessingDomainService.NormalizeText(rawText);

        // Assert
        result.Should().Be("Word1 Word2 Word3");
    }

    [Fact]
    public void NormalizeText_FixesBrokenParagraphs()
    {
        // Arrange: Line ends mid-sentence
        var rawText = "This is a sentenc\ne that was split";

        // Act
        var result = PdfTextProcessingDomainService.NormalizeText(rawText);

        // Assert
        result.Should().Contain("sentence that was split");
    }

    [Fact]
    public void NormalizeText_NormalizesMultipleNewlines()
    {
        // Arrange: Multiple consecutive newlines
        var rawText = "Paragraph1\n\n\n\nParagraph2";

        // Act
        var result = PdfTextProcessingDomainService.NormalizeText(rawText);

        // Assert
        result.Should().Contain("Paragraph1\n\nParagraph2");
        result.Should().NotContain("\n\n\n");
    }

    [Fact]
    public void NormalizeText_RemovesZeroWidthCharacters()
    {
        // Arrange: Zero-width space (U+200B)
        var rawText = "Word1\u200BWord2";
        rawText.Length.Should().Be(11); // Verify input has zero-width character

        // Act
        var result = PdfTextProcessingDomainService.NormalizeText(rawText);

        // Assert
        result.Length.Should().Be(10); // Zero-width character removed
        result.Should().Be("Word1Word2");
    }

    [Fact]
    public void NormalizeText_PerformsUnicodeNormalization()
    {
        // Arrange: Decomposed unicode (é as e + combining accent)
        var rawText = "cafe\u0301"; // café with combining accent

        // Act
        var result = PdfTextProcessingDomainService.NormalizeText(rawText);

        // Assert: Should normalize to composed form (NFC)
        result.Should().Be("café");
    }
    [Fact]
    public void AssessQuality_VeryLowQuality_ReturnsVeryLow()
    {
        // Arrange: 20 chars / 2 pages = 10 chars/page (< 50)
        var text = "Very short PDF text.";
        var pageCount = 2;

        // Act
        var result = PdfTextProcessingDomainService.AssessQuality(text, pageCount);

        // Assert
        result.Should().Be(ExtractionQuality.VeryLow);
    }

    [Fact]
    public void AssessQuality_LowQuality_ReturnsLow()
    {
        // Arrange: 200 chars / 2 pages = 100 chars/page (50-200)
        var text = new string('a', 200);
        var pageCount = 2;

        // Act
        var result = PdfTextProcessingDomainService.AssessQuality(text, pageCount);

        // Assert
        result.Should().Be(ExtractionQuality.Low);
    }

    [Fact]
    public void AssessQuality_MediumQuality_ReturnsMedium()
    {
        // Arrange: 1000 chars / 2 pages = 500 chars/page (200-1000)
        var text = new string('a', 1000);
        var pageCount = 2;

        // Act
        var result = PdfTextProcessingDomainService.AssessQuality(text, pageCount);

        // Assert
        result.Should().Be(ExtractionQuality.Medium);
    }

    [Fact]
    public void AssessQuality_HighQuality_ReturnsHigh()
    {
        // Arrange: 5000 chars / 2 pages = 2500 chars/page (> 1000)
        var text = new string('a', 5000);
        var pageCount = 2;

        // Act
        var result = PdfTextProcessingDomainService.AssessQuality(text, pageCount);

        // Assert
        result.Should().Be(ExtractionQuality.High);
    }

    [Fact]
    public void AssessQuality_ZeroPages_ReturnsVeryLow()
    {
        // Arrange
        var text = "some text";
        var pageCount = 0;

        // Act
        var result = PdfTextProcessingDomainService.AssessQuality(text, pageCount);

        // Assert
        result.Should().Be(ExtractionQuality.VeryLow);
    }

    [Fact]
    public void AssessQuality_EmptyText_ReturnsVeryLow()
    {
        // Arrange
        var text = "";
        var pageCount = 5;

        // Act
        var result = PdfTextProcessingDomainService.AssessQuality(text, pageCount);

        // Assert
        result.Should().Be(ExtractionQuality.VeryLow);
    }
}
