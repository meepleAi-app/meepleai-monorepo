using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;

/// <summary>
/// BGAI-086: Validates PDF processing configuration on application startup
/// Implements IValidateOptions to fail fast on misconfiguration
/// </summary>
public class PdfProcessingConfigurationValidator : IValidateOptions<PdfProcessingOptions>
{
    private const long MinFileSizeBytes = 1024; // 1 KB
    private const long MaxFileSizeBytes = 524288000; // 500 MB
    private const int MinCharsPerPageLowerBound = 100;

    public ValidateOptionsResult Validate(string? name, PdfProcessingOptions options)
    {
        // Validate Quality thresholds
        if (options.Quality.MinimumThreshold < 0.0 || options.Quality.MinimumThreshold > 1.0)
        {
            return ValidateOptionsResult.Fail(
                $"Quality.MinimumThreshold must be between 0.0 and 1.0, got {options.Quality.MinimumThreshold}");
        }

        if (options.Quality.WarningThreshold < 0.0 || options.Quality.WarningThreshold > 1.0)
        {
            return ValidateOptionsResult.Fail(
                $"Quality.WarningThreshold must be between 0.0 and 1.0, got {options.Quality.WarningThreshold}");
        }

        // BGAI-038: Validate threshold relationship (MinimumThreshold should be >= WarningThreshold)
        if (options.Quality.MinimumThreshold < options.Quality.WarningThreshold)
        {
            return ValidateOptionsResult.Fail(
                $"Quality.MinimumThreshold ({options.Quality.MinimumThreshold}) must be >= Quality.WarningThreshold ({options.Quality.WarningThreshold})");
        }

        if (options.Quality.MinCharsPerPage < MinCharsPerPageLowerBound)
        {
            return ValidateOptionsResult.Fail(
                $"Quality.MinCharsPerPage must be ≥{MinCharsPerPageLowerBound}, got {options.Quality.MinCharsPerPage}");
        }

        // Validate file size limits
        if (options.MaxFileSizeBytes < MinFileSizeBytes || options.MaxFileSizeBytes > MaxFileSizeBytes)
        {
            return ValidateOptionsResult.Fail(
                $"MaxFileSizeBytes must be between {MinFileSizeBytes} (1KB) and {MaxFileSizeBytes} (500MB), got {options.MaxFileSizeBytes}");
        }

        // BGAI-087: Validate large PDF threshold
        if (options.LargePdfThresholdBytes < MinFileSizeBytes || options.LargePdfThresholdBytes > options.MaxFileSizeBytes)
        {
            return ValidateOptionsResult.Fail(
                $"LargePdfThresholdBytes must be between {MinFileSizeBytes} (1KB) and MaxFileSizeBytes ({options.MaxFileSizeBytes}), got {options.LargePdfThresholdBytes}");
        }

        // Validate Unstructured API URL
        if (string.IsNullOrWhiteSpace(options.Extractor.Unstructured.ApiUrl))
        {
            return ValidateOptionsResult.Fail(
                "Extractor.Unstructured.ApiUrl is required");
        }

        if (!Uri.TryCreate(options.Extractor.Unstructured.ApiUrl, UriKind.Absolute, out var unstructuredUri) ||
            (!string.Equals(unstructuredUri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && !string.Equals(unstructuredUri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
        {
            return ValidateOptionsResult.Fail(
                $"Extractor.Unstructured.ApiUrl must be a valid absolute URL, got '{options.Extractor.Unstructured.ApiUrl}'");
        }

        // Validate Unstructured timeout and retries
        if (options.Extractor.Unstructured.TimeoutSeconds < 1 || options.Extractor.Unstructured.TimeoutSeconds > 300)
        {
            return ValidateOptionsResult.Fail(
                $"Extractor.Unstructured.TimeoutSeconds must be between 1 and 300, got {options.Extractor.Unstructured.TimeoutSeconds}");
        }

        if (options.Extractor.Unstructured.MaxRetries < 0 || options.Extractor.Unstructured.MaxRetries > 10)
        {
            return ValidateOptionsResult.Fail(
                $"Extractor.Unstructured.MaxRetries must be between 0 and 10, got {options.Extractor.Unstructured.MaxRetries}");
        }

        // Validate SmolDocling API URL
        if (string.IsNullOrWhiteSpace(options.Extractor.SmolDocling.ApiUrl))
        {
            return ValidateOptionsResult.Fail(
                "Extractor.SmolDocling.ApiUrl is required");
        }

        if (!Uri.TryCreate(options.Extractor.SmolDocling.ApiUrl, UriKind.Absolute, out var smolDoclingUri) ||
            (!string.Equals(smolDoclingUri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && !string.Equals(smolDoclingUri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
        {
            return ValidateOptionsResult.Fail(
                $"Extractor.SmolDocling.ApiUrl must be a valid absolute URL, got '{options.Extractor.SmolDocling.ApiUrl}'");
        }

        // Validate SmolDocling timeout and retries
        if (options.Extractor.SmolDocling.TimeoutSeconds < 1 || options.Extractor.SmolDocling.TimeoutSeconds > 300)
        {
            return ValidateOptionsResult.Fail(
                $"Extractor.SmolDocling.TimeoutSeconds must be between 1 and 300, got {options.Extractor.SmolDocling.TimeoutSeconds}");
        }

        if (options.Extractor.SmolDocling.MaxRetries < 0 || options.Extractor.SmolDocling.MaxRetries > 10)
        {
            return ValidateOptionsResult.Fail(
                $"Extractor.SmolDocling.MaxRetries must be between 0 and 10, got {options.Extractor.SmolDocling.MaxRetries}");
        }

        return ValidateOptionsResult.Success;
    }
}
