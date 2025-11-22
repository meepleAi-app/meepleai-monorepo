namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// Adapter interface for PDF validation.
/// Abstracts technical validation (magic bytes, Docnet.Core parsing) from domain logic.
/// Implements PDF-09 validation specification.
/// </summary>
public interface IPdfValidator
{
    /// <summary>
    /// Validates PDF stream against all business and technical constraints.
    ///
    /// Validation steps:
    /// 1. Technical: Magic bytes check (PDF signature)
    /// 2. Business: File size validation (configurable max MB)
    /// 3. Business: MIME type validation (whitelist)
    /// 4. Technical: PDF parsing with Docnet.Core (extract metadata)
    /// 5. Business: Page count validation (min/max limits)
    /// 6. Business: PDF version validation (minimum supported version)
    /// </summary>
    /// <param name="pdfStream">PDF file stream to validate</param>
    /// <param name="fileName">Original file name (for logging and MIME detection)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Validation result with errors and metadata</returns>
    Task<PdfValidationResult> ValidateAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts PDF metadata without full validation.
    /// Used for metadata-only operations (e.g., quick info retrieval).
    /// </summary>
    /// <param name="pdfStream">PDF file stream</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>PDF metadata or null if extraction fails</returns>
    Task<PdfMetadata?> ExtractMetadataAsync(
        Stream pdfStream,
        CancellationToken ct = default);
}

/// <summary>
/// Result of PDF validation operation.
/// Contains validation status, error details, and extracted metadata.
/// </summary>
public record PdfValidationResult(
    bool IsValid,
    Dictionary<string, string> Errors,
    PdfMetadata? Metadata = null)
{
    /// <summary>
    /// Creates a successful validation result with metadata.
    /// </summary>
    public static PdfValidationResult CreateSuccess(PdfMetadata metadata) =>
        new(true, new Dictionary<string, string>(), metadata);

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    public static PdfValidationResult CreateFailure(Dictionary<string, string> errors) =>
        new(false, errors, null);
}

/// <summary>
/// PDF document metadata extracted during validation.
/// Contains structural information about the PDF.
/// </summary>
public record PdfMetadata(
    int PageCount,
    string PdfVersion,
    long FileSizeBytes,
    bool HasText = false,
    bool HasImages = false);
