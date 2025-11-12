using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// Unit tests for PDF quality validation domain service
/// </summary>
/// <remarks>
/// Issue #951: BGAI-012 - Tests quality threshold enforcement, reporting, recommendations
/// </remarks>
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
    public void Test01_HighQuality_PassesThreshold()
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
        Assert.True(validation.Passed);
        Assert.Equal(0.85, validation.QualityScore);
        Assert.Equal(0.80, validation.Threshold);
        Assert.Equal("Good", validation.Report.QualityLevel);
        Assert.Contains("meets threshold", validation.Report.Recommendation);
    }

    [Fact]
    public void Test02_MediumQuality_FailsThreshold()
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
        Assert.False(validation.Passed);
        Assert.Equal(0.70, validation.QualityScore);
        Assert.Equal("Acceptable", validation.Report.QualityLevel);
        Assert.Contains("below optimal threshold", validation.Report.Recommendation);
    }

    [Fact]
    public void Test03_LowQuality_FailsWithPoorRating()
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
        Assert.False(validation.Passed);
        Assert.Equal(0.50, validation.QualityScore);
        Assert.Equal("Poor", validation.Report.QualityLevel);
        Assert.Contains("poor", validation.Report.Recommendation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Test04_VeryLowQuality_FailsWithCriticalRating()
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
        Assert.False(validation.Passed);
        Assert.Equal(0.25, validation.QualityScore);
        Assert.Equal("Critical", validation.Report.QualityLevel);
        Assert.Contains("critical", validation.Report.Recommendation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Test05_FailedExtraction_ReturnsFailedValidation()
    {
        // Arrange - Extraction failed
        var result = TextExtractionResult.CreateFailure("Service unavailable");

        // Act
        var validation = _service.ValidateExtractionQuality(result, "Unstructured", "req-005");

        // Assert
        Assert.False(validation.Passed);
        Assert.Equal(0.0, validation.QualityScore);
        Assert.Equal("Failed", validation.Report.QualityLevel);
        Assert.Contains("failed", validation.Report.Recommendation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Test06_QualityMetrics_CalculatedCorrectly()
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
        Assert.Equal(10, validation.Report.Metrics.PageCount);
        Assert.Equal(10000, validation.Report.Metrics.CharacterCount);
        Assert.Equal(1000, validation.Report.Metrics.CharsPerPage);
        Assert.True(validation.Report.Metrics.TextCoverageScore > 0.5);
        Assert.True(validation.Report.Metrics.TotalScore > 0.80);
    }

    [Fact]
    public void Test07_MeetsMinimumQuality_EnumVersion()
    {
        // Test helper method with enum
        Assert.True(_service.MeetsMinimumQuality(ExtractionQuality.High)); // 0.85 ≥ 0.80
        Assert.False(_service.MeetsMinimumQuality(ExtractionQuality.Medium)); // 0.70 < 0.80
        Assert.False(_service.MeetsMinimumQuality(ExtractionQuality.Low)); // 0.50 < 0.80
        Assert.False(_service.MeetsMinimumQuality(ExtractionQuality.VeryLow)); // 0.25 < 0.80
    }

    [Fact]
    public void Test08_MeetsMinimumQuality_NumericVersion()
    {
        // Test helper method with numeric score
        Assert.True(_service.MeetsMinimumQuality(0.85)); // ≥ 0.80
        Assert.True(_service.MeetsMinimumQuality(0.80)); // Exactly at threshold
        Assert.False(_service.MeetsMinimumQuality(0.79)); // < 0.80
        Assert.False(_service.MeetsMinimumQuality(0.50)); // < 0.80
        Assert.False(_service.MeetsMinimumQuality(0.00)); // < 0.80
    }

    [Fact]
    public void Test09_QualityReport_ContainsAllFields()
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
        Assert.NotNull(validation.Report);
        Assert.Equal("req-009", validation.Report.RequestId);
        Assert.Equal("TestExtractor", validation.Report.SourceExtractor);
        Assert.NotNull(validation.Report.QualityLevel);
        Assert.NotNull(validation.Report.Metrics);
        Assert.NotNull(validation.Report.Recommendation);
        Assert.True(validation.Report.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void Test10_TextCoverage_LowCharsPerPage()
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
        Assert.False(validation.Passed);
        Assert.True(validation.Report.Metrics.CharsPerPage < 500);
        Assert.True(validation.Report.Metrics.TextCoverageScore < 0.5);
    }
}
