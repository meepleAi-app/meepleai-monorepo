using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// Unit tests for PDF quality validation domain service
/// ISSUE-1818: Migrated to FluentAssertions
/// </summary>
/// <remarks>
/// Issue #951: BGAI-012 - Tests quality threshold enforcement, reporting, recommendations
/// </remarks>
[Trait("Category", TestCategories.Unit)]
public class PdfQualityValidationDomainServiceTests
{
    private readonly PdfQualityValidationDomainService _service;
    private readonly IConfiguration _configuration;

    public PdfQualityValidationDomainServiceTests()
    {
        var inMemoryConfig = new Dictionary<string, string>
        {
            ["PdfProcessing:Quality:MinimumThreshold"] = "0.80",
            ["PdfProcessing:Quality:MinCharsPerPage"] = "500"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig!)
            .Build();

        _service = new PdfQualityValidationDomainService(
            Mock.Of<ILogger<PdfQualityValidationDomainService>>(),
            _configuration);
    }

    [Fact]
    public void HighQuality_PassesThreshold()
    {
        // Arrange - High quality extraction (0.85 ≥ 0.80)
        var result = TextExtractionResult.CreateSuccess(
            extractedText: new string('x', 10000),
            pageCount: 10,
            characterCount: 10000,
            ocrTriggered: false,
            quality: ExtractionQuality.High);

        // Act
        var validation = _service.ValidateExtractionQuality(result, "Unstructured", "req-001");

        // Assert
        validation.Passed.Should().BeTrue();
        validation.QualityScore.Should().Be(0.85);
        validation.Threshold.Should().Be(0.80);
        validation.Report.QualityLevel.Should().Be("Good");
        validation.Report.Recommendation.Should().Be("Quality is sufficient for indexing.");
    }

    [Fact]
    public void MediumQuality_FailsThreshold()
    {
        // Arrange - Medium quality (0.70 < 0.80)
        var result = TextExtractionResult.CreateSuccess(
            extractedText: new string('x', 7000),
            pageCount: 10,
            characterCount: 7000,
            ocrTriggered: false,
            quality: ExtractionQuality.Medium);

        // Act
        var validation = _service.ValidateExtractionQuality(result, "SmolDocling", "req-002");

        // Assert
        validation.Passed.Should().BeFalse();
        validation.QualityScore.Should().Be(0.70);
        validation.Report.QualityLevel.Should().Be("Acceptable");
        validation.Report.Recommendation.Should().Contain("below threshold");
    }

    [Fact]
    public void LowQuality_FailsWithPoorRating()
    {
        // Arrange - Low quality (0.50 < 0.80)
        var result = TextExtractionResult.CreateSuccess(
            extractedText: new string('x', 3000),
            pageCount: 10,
            characterCount: 3000,
            ocrTriggered: false,
            quality: ExtractionQuality.Low);

        // Act
        var validation = _service.ValidateExtractionQuality(result, "Docnet", "req-003");

        // Assert
        validation.Passed.Should().BeFalse();
        validation.QualityScore.Should().Be(0.50);
        validation.Report.QualityLevel.Should().Be("Poor");
        validation.Report.Recommendation.Should().Contain("below threshold");
    }

    [Fact]
    public void VeryLowQuality_FailsWithCriticalRating()
    {
        // Arrange - Very low quality (0.25 < 0.80)
        var result = TextExtractionResult.CreateSuccess(
            extractedText: new string('x', 500),
            pageCount: 10,
            characterCount: 500,
            ocrTriggered: false,
            quality: ExtractionQuality.VeryLow);

        // Act
        var validation = _service.ValidateExtractionQuality(result, "Fallback", "req-004");

        // Assert
        validation.Passed.Should().BeFalse();
        validation.QualityScore.Should().Be(0.25);
        validation.Report.QualityLevel.Should().Be("Critical");
        validation.Report.Recommendation.Should().Contain("very low quality");
    }

    [Fact]
    public void FailedExtraction_ReturnsFailedValidation()
    {
        // Arrange - Extraction failed
        var result = TextExtractionResult.CreateFailure("Service unavailable");

        // Act
        var validation = _service.ValidateExtractionQuality(result, "Unstructured", "req-005");

        // Assert
        validation.Passed.Should().BeFalse();
        validation.QualityScore.Should().Be(0.0);
        validation.Report.QualityLevel.Should().Be("Failed");
        validation.Report.Recommendation.Should().ContainEquivalentOf("failed");
    }

    [Fact]
    public void QualityMetrics_CalculatedCorrectly()
    {
        // Arrange - 1000 chars/page (good coverage)
        var result = TextExtractionResult.CreateSuccess(
            extractedText: new string('x', 10000),
            pageCount: 10,
            characterCount: 10000,
            ocrTriggered: false,
            quality: ExtractionQuality.High);

        // Act
        var validation = _service.ValidateExtractionQuality(result, "Unstructured", "req-006");

        // Assert - Metrics
        validation.Report.Metrics.PageCount.Should().Be(10);
        validation.Report.Metrics.CharacterCount.Should().Be(10000);
        validation.Report.Metrics.CharsPerPage.Should().Be(1000);
        validation.Report.Metrics.TextCoverageScore.Should().BeGreaterThan(0.5);
        validation.Report.Metrics.TotalScore.Should().BeGreaterThan(0.80);
    }

    [Fact]
    public void MeetsMinimumQuality_EnumVersion()
    {
        // Test helper method with enum
        _service.MeetsMinimumQuality(ExtractionQuality.High).Should().BeTrue(); // 0.85 ≥ 0.80
        _service.MeetsMinimumQuality(ExtractionQuality.Medium).Should().BeFalse(); // 0.70 < 0.80
        _service.MeetsMinimumQuality(ExtractionQuality.Low).Should().BeFalse(); // 0.50 < 0.80
        _service.MeetsMinimumQuality(ExtractionQuality.VeryLow).Should().BeFalse(); // 0.25 < 0.80
    }

    [Fact]
    public void MeetsMinimumQuality_NumericVersion()
    {
        // Test helper method with numeric score
        _service.MeetsMinimumQuality(0.85).Should().BeTrue(); // ≥ 0.80
        _service.MeetsMinimumQuality(0.80).Should().BeTrue(); // Exactly at threshold
        _service.MeetsMinimumQuality(0.79).Should().BeFalse(); // < 0.80
        _service.MeetsMinimumQuality(0.50).Should().BeFalse(); // < 0.80
        _service.MeetsMinimumQuality(0.00).Should().BeFalse(); // < 0.80
    }

    [Fact]
    public void QualityReport_ContainsAllFields()
    {
        // Arrange
        var result = TextExtractionResult.CreateSuccess(
            extractedText: "Sample text",
            pageCount: 5,
            characterCount: 2500,
            ocrTriggered: false,
            quality: ExtractionQuality.High);

        // Act
        var validation = _service.ValidateExtractionQuality(result, "TestExtractor", "req-009");

        // Assert - Report structure
        validation.Report.Should().NotBeNull();
        validation.Report.RequestId.Should().Be("req-009");
        validation.Report.SourceExtractor.Should().Be("TestExtractor");
        validation.Report.QualityLevel.Should().NotBeNull();
        validation.Report.Metrics.Should().NotBeNull();
        validation.Report.Recommendation.Should().NotBeNull();
        validation.Report.Timestamp.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void TextCoverage_LowCharsPerPage()
    {
        // Arrange - Low chars/page (300 chars/page < 500 min)
        var result = TextExtractionResult.CreateSuccess(
            extractedText: new string('x', 3000),
            pageCount: 10,
            characterCount: 3000, // 300 chars/page
            ocrTriggered: false,
            quality: ExtractionQuality.Low);

        // Act
        var validation = _service.ValidateExtractionQuality(result, "LowQuality", "req-010");

        // Assert
        validation.Passed.Should().BeFalse();
        validation.Report.Metrics.CharsPerPage.Should().BeLessThan(500);
        validation.Report.Metrics.TextCoverageScore.Should().BeLessThan(0.5);
    }
}
