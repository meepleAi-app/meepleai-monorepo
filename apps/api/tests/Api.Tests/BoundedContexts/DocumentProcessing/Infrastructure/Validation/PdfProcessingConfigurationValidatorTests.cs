using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Validation;

/// <summary>
/// BGAI-086: Unit tests for PDF processing configuration validation
/// Tests all validation rules for startup configuration
/// </summary>
public class PdfProcessingConfigurationValidatorTests
{
    private readonly PdfProcessingConfigurationValidator _validator = new();

    #region Valid Configuration Tests

    [Fact]
    public void Validate_ValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var options = CreateValidOptions();

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_MinimumValidThresholds_ReturnsSuccess()
    {
        // Arrange
        var options = CreateValidOptions();
        options.Quality.MinimumThreshold = 0.0;
        options.Quality.WarningThreshold = 0.0;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_MaximumValidThresholds_ReturnsSuccess()
    {
        // Arrange
        var options = CreateValidOptions();
        options.Quality.MinimumThreshold = 1.0;
        options.Quality.WarningThreshold = 1.0;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    #endregion

    #region Quality Threshold Validation Tests

    [Theory]
    [InlineData(-0.1)]
    [InlineData(-1.0)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void Validate_InvalidMinimumThreshold_ReturnsFail(double threshold)
    {
        // Arrange
        var options = CreateValidOptions();
        options.Quality.MinimumThreshold = threshold;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Quality.MinimumThreshold must be between 0.0 and 1.0", result.FailureMessage);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(-1.0)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void Validate_InvalidWarningThreshold_ReturnsFail(double threshold)
    {
        // Arrange
        var options = CreateValidOptions();
        options.Quality.WarningThreshold = threshold;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Quality.WarningThreshold must be between 0.0 and 1.0", result.FailureMessage);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(50)]
    [InlineData(0)]
    [InlineData(-100)]
    public void Validate_InvalidMinCharsPerPage_ReturnsFail(int chars)
    {
        // Arrange
        var options = CreateValidOptions();
        options.Quality.MinCharsPerPage = chars;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Quality.MinCharsPerPage must be ≥100", result.FailureMessage);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public void Validate_ValidMinCharsPerPage_ReturnsSuccess(int chars)
    {
        // Arrange
        var options = CreateValidOptions();
        options.Quality.MinCharsPerPage = chars;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_MinimumThresholdLessThanWarningThreshold_ReturnsFail()
    {
        // Arrange - BGAI-038: Test threshold relationship validation
        var options = CreateValidOptions();
        options.Quality.MinimumThreshold = 0.60; // Less than WarningThreshold
        options.Quality.WarningThreshold = 0.70;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Quality.MinimumThreshold", result.FailureMessage);
        Assert.Contains("must be >= Quality.WarningThreshold", result.FailureMessage);
    }

    [Theory]
    [InlineData(0.70, 0.70)] // Equal thresholds (valid)
    [InlineData(0.80, 0.70)] // MinimumThreshold > WarningThreshold (valid)
    [InlineData(0.90, 0.80)] // MinimumThreshold > WarningThreshold (valid)
    public void Validate_MinimumThresholdGreaterOrEqualToWarningThreshold_ReturnsSuccess(
        double minimumThreshold, double warningThreshold)
    {
        // Arrange - BGAI-038: Test valid threshold relationships
        var options = CreateValidOptions();
        options.Quality.MinimumThreshold = minimumThreshold;
        options.Quality.WarningThreshold = warningThreshold;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    #endregion

    #region File Size Validation Tests

    [Theory]
    [InlineData(1024)] // 1 KB - minimum
    [InlineData(1048576)] // 1 MB
    [InlineData(104857600)] // 100 MB - default
    [InlineData(524288000)] // 500 MB - maximum
    public void Validate_ValidFileSizes_ReturnsSuccess(long fileSize)
    {
        // Arrange
        var options = CreateValidOptions();
        options.MaxFileSizeBytes = fileSize;
        // LargePdfThresholdBytes must be between 1KB and MaxFileSizeBytes
        // Use half of max, but ensure it's at least 1KB
        options.LargePdfThresholdBytes = Math.Max(1024, fileSize / 2);

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(1023)] // Below 1 KB
    [InlineData(0)]
    [InlineData(-1024)]
    [InlineData(524288001)] // Above 500 MB
    [InlineData(1073741824)] // 1 GB
    public void Validate_InvalidFileSizes_ReturnsFail(long fileSize)
    {
        // Arrange
        var options = CreateValidOptions();
        options.MaxFileSizeBytes = fileSize;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("MaxFileSizeBytes must be between", result.FailureMessage);
    }

    #endregion

    #region Unstructured API URL Validation Tests

    [Theory]
    [InlineData("http://localhost:8001")]
    [InlineData("https://unstructured-service:8001")]
    [InlineData("http://192.168.1.100:8001/api")]
    public void Validate_ValidUnstructuredApiUrl_ReturnsSuccess(string apiUrl)
    {
        // Arrange
        var options = CreateValidOptions();
        options.Extractor.Unstructured.ApiUrl = apiUrl;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_EmptyUnstructuredApiUrl_ReturnsFail(string? apiUrl)
    {
        // Arrange
        var options = CreateValidOptions();
        options.Extractor.Unstructured.ApiUrl = apiUrl!;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Extractor.Unstructured.ApiUrl is required", result.FailureMessage);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://invalid")]
    [InlineData("localhost:8001")] // Missing scheme
    public void Validate_InvalidUnstructuredApiUrl_ReturnsFail(string apiUrl)
    {
        // Arrange
        var options = CreateValidOptions();
        options.Extractor.Unstructured.ApiUrl = apiUrl;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Extractor.Unstructured.ApiUrl must be a valid absolute URL", result.FailureMessage);
    }

    #endregion

    #region Unstructured Timeout/Retry Validation Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(301)]
    [InlineData(1000)]
    public void Validate_InvalidUnstructuredTimeout_ReturnsFail(int timeout)
    {
        // Arrange
        var options = CreateValidOptions();
        options.Extractor.Unstructured.TimeoutSeconds = timeout;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Extractor.Unstructured.TimeoutSeconds must be between 1 and 300", result.FailureMessage);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(100)]
    public void Validate_InvalidUnstructuredMaxRetries_ReturnsFail(int retries)
    {
        // Arrange
        var options = CreateValidOptions();
        options.Extractor.Unstructured.MaxRetries = retries;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Extractor.Unstructured.MaxRetries must be between 0 and 10", result.FailureMessage);
    }

    #endregion

    #region SmolDocling API URL Validation Tests

    [Theory]
    [InlineData("http://localhost:8002")]
    [InlineData("https://smoldocling-service:8002")]
    [InlineData("http://192.168.1.100:8002/api")]
    public void Validate_ValidSmolDoclingApiUrl_ReturnsSuccess(string apiUrl)
    {
        // Arrange
        var options = CreateValidOptions();
        options.Extractor.SmolDocling.ApiUrl = apiUrl;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_EmptySmolDoclingApiUrl_ReturnsFail(string? apiUrl)
    {
        // Arrange
        var options = CreateValidOptions();
        options.Extractor.SmolDocling.ApiUrl = apiUrl!;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Extractor.SmolDocling.ApiUrl is required", result.FailureMessage);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://invalid")]
    [InlineData("localhost:8002")] // Missing scheme
    public void Validate_InvalidSmolDoclingApiUrl_ReturnsFail(string apiUrl)
    {
        // Arrange
        var options = CreateValidOptions();
        options.Extractor.SmolDocling.ApiUrl = apiUrl;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Extractor.SmolDocling.ApiUrl must be a valid absolute URL", result.FailureMessage);
    }

    #endregion

    #region SmolDocling Timeout/Retry Validation Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(301)]
    [InlineData(1000)]
    public void Validate_InvalidSmolDoclingTimeout_ReturnsFail(int timeout)
    {
        // Arrange
        var options = CreateValidOptions();
        options.Extractor.SmolDocling.TimeoutSeconds = timeout;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Extractor.SmolDocling.TimeoutSeconds must be between 1 and 300", result.FailureMessage);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(100)]
    public void Validate_InvalidSmolDoclingMaxRetries_ReturnsFail(int retries)
    {
        // Arrange
        var options = CreateValidOptions();
        options.Extractor.SmolDocling.MaxRetries = retries;

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Extractor.SmolDocling.MaxRetries must be between 0 and 10", result.FailureMessage);
    }

    #endregion

    #region Helper Methods

    private static PdfProcessingOptions CreateValidOptions()
    {
        return new PdfProcessingOptions
        {
            Quality = new QualityOptions
            {
                MinimumThreshold = 0.80,
                WarningThreshold = 0.70,
                MinCharsPerPage = 500
            },
            MaxFileSizeBytes = 104857600, // 100 MB
            LargePdfThresholdBytes = 52428800, // 50 MB (must be ≤ MaxFileSizeBytes)
            UseTempFileForLargePdfs = true,
            Extractor = new ExtractorOptions
            {
                Provider = "Orchestrator",
                Unstructured = new UnstructuredOptions
                {
                    ApiUrl = "http://unstructured-service:8001",
                    TimeoutSeconds = 35,
                    MaxRetries = 3,
                    Strategy = "fast",
                    Language = "ita"
                },
                SmolDocling = new SmolDoclingOptions
                {
                    ApiUrl = "http://smoldocling-service:8002",
                    TimeoutSeconds = 30,
                    MaxRetries = 3
                }
            }
        };
    }

    #endregion
}

