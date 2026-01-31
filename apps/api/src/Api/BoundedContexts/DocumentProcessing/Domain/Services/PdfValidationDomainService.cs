using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// Domain service for PDF validation business rules.
/// Encapsulates business logic separate from technical validation (magic bytes, Docnet parsing).
///
/// Business Rules:
/// - File size constraints (max MB limit)
/// - Page count constraints (min/max pages)
/// - PDF version requirements (minimum supported version)
/// - MIME type whitelist (allowed content types)
/// </summary>
internal class PdfValidationDomainService
{
    private readonly IConfiguration _config;

    public PdfValidationDomainService(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Business rule: File size must not exceed configured maximum.
    /// Default: 50 MB (configurable via Pdf:MaxFileSizeMb)
    /// </summary>
    public ValidationResult ValidateFileSize(FileSize fileSize)
    {
        var maxMb = _config.GetValue<int>("Pdf:MaxFileSizeMb", 50);
        var maxBytes = maxMb * 1024L * 1024L;

        if (!fileSize.IsWithinLimit(maxBytes))
        {
            return ValidationResult.Failure(
                "fileSize",
                $"File size ({fileSize.Megabytes:F1} MB) exceeds maximum of {maxMb:F0} MB");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Business rule: Page count must be within configured limits.
    /// Default: Min 1, Max 500 (configurable via Pdf:MinPageCount, Pdf:MaxPageCount)
    /// </summary>
    public ValidationResult ValidatePageCount(PageCount pageCount)
    {
        var minPages = _config.GetValue<int>("Pdf:MinPageCount", 1);
        var maxPages = _config.GetValue<int>("Pdf:MaxPageCount", 500);

        if (pageCount.Value < minPages)
        {
            return ValidationResult.Failure(
                "pageCount",
                $"PDF must have at least {minPages} page(s). Found {pageCount.Value} page(s).");
        }

        if (!pageCount.IsWithinLimit(maxPages))
        {
            return ValidationResult.Failure(
                "pageCount",
                $"PDF has {pageCount.Value} pages, maximum allowed is {maxPages} pages.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Business rule: PDF version must meet minimum requirements.
    /// Default: Minimum PDF 1.0 (configurable via Pdf:MinVersion)
    /// </summary>
    public ValidationResult ValidatePdfVersion(PdfVersion version)
    {
        var minVersionString = _config.GetValue<string>("Pdf:MinVersion", "1.0");
        var minVersion = PdfVersion.Parse(minVersionString);

        if (!version.IsAtLeast(minVersion))
        {
            return ValidationResult.Failure(
                "pdfVersion",
                $"PDF version {version} is not supported. Minimum version required is {minVersion}.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Business rule: MIME type must be in whitelist of allowed content types.
    /// Default: application/pdf, application/x-pdf
    /// </summary>
    public ValidationResult ValidateMimeType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return ValidationResult.Failure(
                "fileType",
                "Content type cannot be empty");
        }

        var allowedTypes = _config.GetSection("Pdf:AllowedContentTypes")
            .Get<string[]>() ?? new[] { "application/pdf", "application/x-pdf" };

        if (!allowedTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Failure(
                "fileType",
                $"File type '{contentType}' is not allowed. Only PDF files (application/pdf) are supported.");
        }

        return ValidationResult.Success();
    }
}

/// <summary>
/// Result of a validation operation.
/// Contains success/failure status and error details if validation failed.
/// </summary>
internal record ValidationResult(bool IsSuccess, string? FieldName = null, string? Error = null)
{
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new(true);

    /// <summary>
    /// Creates a failed validation result with field name and error message.
    /// </summary>
    public static ValidationResult Failure(string fieldName, string error) => new(false, fieldName, error);
}
