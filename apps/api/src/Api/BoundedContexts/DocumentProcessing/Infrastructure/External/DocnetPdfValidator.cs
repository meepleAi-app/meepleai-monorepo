using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Helpers;
using Docnet.Core;
using Docnet.Core.Models;
using Microsoft.Extensions.Logging;
using DomainPdfVersion = Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects.PdfVersion;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// PDF validator implementation using Docnet.Core library.
/// Combines technical validation (magic bytes, Docnet parsing) with domain business rules.
///
/// Technical Responsibilities:
/// - Magic bytes validation (PDF signature check)
/// - Docnet.Core parsing and metadata extraction
/// - Temporary file management for Docnet operations
/// - Thread-safe Docnet access (via semaphore)
///
/// Business Rules (delegated to PdfValidationDomainService):
/// - File size constraints
/// - Page count limits
/// - PDF version requirements
/// - MIME type validation
/// </summary>
internal class DocnetPdfValidator : IPdfValidator
{
    private readonly ILogger<DocnetPdfValidator> _logger;
    private readonly PdfValidationDomainService _domainService;

    // PDF magic bytes signature: %PDF-
    private static readonly byte[] PdfMagicBytes = { 0x25, 0x50, 0x44, 0x46, 0x2D };

    // Semaphore to prevent concurrent access to Docnet.Core (not thread-safe)
    private static readonly SemaphoreSlim DocnetSemaphore = new(1, 1);

    public DocnetPdfValidator(
        ILogger<DocnetPdfValidator> logger,
        PdfValidationDomainService domainService)
    {
        _logger = logger;
        _domainService = domainService;
    }

    public async Task<PdfValidationResult> ValidateAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        // Precondition validation
        var preconditionResult = ValidatePreconditions(pdfStream, fileName);
        if (preconditionResult != null)
        {
            return preconditionResult;
        }

        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        PdfMetadata? metadata = null;

        try
        {
            // Step 1: Technical validation - Magic bytes
            await ValidateTechnicalFormatAsync(pdfStream, errors, cancellationToken).ConfigureAwait(false);

            // Step 2 & 3: Business validation - File size and MIME type
            ValidateBusinessRules(pdfStream, fileName, errors);

            // Step 4 & 5: Docnet validation + metadata business rules
            metadata = await ValidateWithDocnetAndExtractMetadataAsync(pdfStream, errors, cancellationToken).ConfigureAwait(false);

            // Return result
            if (errors.Count > 0)
            {
                _logger.LogWarning("PDF validation failed for {FileName}: {@Errors}", fileName, errors);
                return PdfValidationResult.CreateFailure(errors);
            }

            _logger.LogInformation("PDF validation successful for {FileName}: {PageCount} pages, version {Version}",
                fileName, metadata?.PageCount ?? 0, metadata?.PdfVersion ?? "unknown");

            return PdfValidationResult.CreateSuccess(metadata!);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during PDF validation for {FileName}", fileName);
            throw new PdfValidationException($"Invalid PDF validation operation: {ex.Message}", ex);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument during PDF validation for {FileName}", fileName);
            throw new PdfValidationException($"Invalid validation argument: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error during PDF validation for {FileName}", fileName);
            throw new PdfValidationException($"Failed to read PDF file for validation: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied during PDF validation for {FileName}", fileName);
            throw new PdfValidationException("Failed to access PDF file: Permission denied.", ex);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // SERVICE BOUNDARY PATTERN: PDF validation adapter boundary - must handle all errors gracefully
            // Rationale: This is an adapter that validates untrusted PDF files from users. Validation
            // involves magic byte checks, Docnet.Core parsing, and file operations that can throw various runtime
            // exceptions. We wrap all exceptions in PdfValidationException for consistent error handling upstream.
            // Context: Docnet.Core can throw unexpected exceptions from native PDF validation libraries
            _logger.LogError(ex, "Unexpected error during PDF validation for {FileName}", fileName);
            throw new PdfValidationException($"PDF validation failed: {ex.Message}", ex);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    /// <summary>
    /// Validates preconditions (null checks) for PDF validation.
    /// Returns failure result if preconditions fail, null otherwise.
    /// </summary>
    private static PdfValidationResult? ValidatePreconditions(Stream? pdfStream, string? fileName)
    {
        if (pdfStream == null)
        {
            return PdfValidationResult.CreateFailure(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["stream"] = "PDF stream cannot be null"
            });
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return PdfValidationResult.CreateFailure(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["fileName"] = "File name cannot be empty"
            });
        }

        return null;
    }

    /// <summary>
    /// Validates technical PDF format (magic bytes).
    /// </summary>
    private static async Task ValidateTechnicalFormatAsync(
        Stream pdfStream,
        Dictionary<string, string> errors,
        CancellationToken cancellationToken)
    {
        if (!await ValidateMagicBytesAsync(pdfStream, cancellationToken).ConfigureAwait(false))
        {
            errors["fileFormat"] = "Invalid PDF file format. File does not start with PDF signature.";
        }

        // Reset stream for further processing
        pdfStream.Position = 0;
    }

    /// <summary>
    /// Validates business rules (file size and MIME type).
    /// </summary>
    private void ValidateBusinessRules(
        Stream pdfStream,
        string fileName,
        Dictionary<string, string> errors)
    {
        // File size validation
        if (pdfStream.Length < 1)
        {
            errors["fileSize"] = "File size must be at least 1 byte";
        }
        else
        {
            var fileSize = new FileSize(pdfStream.Length);
            var fileSizeResult = _domainService.ValidateFileSize(fileSize);
            if (!fileSizeResult.IsSuccess)
            {
                errors[fileSizeResult.FieldName!] = fileSizeResult.Error!;
            }
        }

        // MIME type validation
        var contentType = GetContentType(fileName);
        var mimeResult = _domainService.ValidateMimeType(contentType);
        if (!mimeResult.IsSuccess)
        {
            errors[mimeResult.FieldName!] = mimeResult.Error!;
        }
    }

    /// <summary>
    /// Validates PDF with Docnet.Core and extracts metadata.
    /// Also validates metadata business rules (page count, PDF version).
    /// </summary>
    private async Task<PdfMetadata?> ValidateWithDocnetAndExtractMetadataAsync(
        Stream pdfStream,
        Dictionary<string, string> errors,
        CancellationToken cancellationToken)
    {
        PdfMetadata? metadata = null;

        await DocnetSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tempFile = SecureTempFileHelper.CreateSecureTempFilePath(".pdf");
            try
            {
                // Write stream to temp file
                using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await pdfStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                }

                pdfStream.Position = 0;

                // Validate with Docnet.Core
                var validationResult = await Task.Run(() => ValidatePdfWithDocnet(tempFile), cancellationToken).ConfigureAwait(false);

                // Collect Docnet errors
                if (validationResult.Errors.Count > 0)
                {
                    foreach (var error in validationResult.Errors)
                    {
                        errors[error.Key] = error.Value;
                    }
                }

                // Validate metadata business rules
                if (validationResult.Metadata != null)
                {
                    metadata = validationResult.Metadata;
                    ValidateMetadataBusinessRules(metadata, errors);
                }
            }
            finally
            {
                CleanupTempFile(tempFile);
            }
        }
        finally
        {
            DocnetSemaphore.Release();
        }

        return metadata;
    }

    /// <summary>
    /// Validates business rules on extracted PDF metadata (page count, PDF version).
    /// </summary>
    private void ValidateMetadataBusinessRules(PdfMetadata metadata, Dictionary<string, string> errors)
    {
        // Page count validation
        var pageCount = new PageCount(metadata.PageCount);
        var pageCountResult = _domainService.ValidatePageCount(pageCount);
        if (!pageCountResult.IsSuccess)
        {
            errors[pageCountResult.FieldName!] = pageCountResult.Error!;
        }

        // PDF version validation
        if (!string.IsNullOrEmpty(metadata.PdfVersion) &&
            DomainPdfVersion.TryParse(metadata.PdfVersion, out var version) &&
            version != null)
        {
            var versionResult = _domainService.ValidatePdfVersion(version);
            if (!versionResult.IsSuccess)
            {
                errors[versionResult.FieldName!] = versionResult.Error!;
            }
        }
    }

    public async Task<PdfMetadata?> ExtractMetadataAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        if (pdfStream == null)
            return null;

        await DocnetSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // S5445 fix: Use secure temp file creation instead of Path.GetTempFileName()
            var tempFile = SecureTempFileHelper.CreateSecureTempFilePath(".pdf");
            try
            {
                using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await pdfStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                }

                pdfStream.Position = 0;

                var result = await Task.Run(() => ValidatePdfWithDocnet(tempFile), cancellationToken).ConfigureAwait(false);
                return result.Metadata;
            }
            finally
            {
                CleanupTempFile(tempFile);
            }
        }
        finally
        {
            DocnetSemaphore.Release();
        }
    }

    /// <summary>
    /// Validates PDF magic bytes at the start of the stream.
    /// PDF files must start with "%PDF-" signature (0x25 0x50 0x44 0x46 0x2D).
    /// </summary>
    private static async Task<bool> ValidateMagicBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.Length < PdfMagicBytes.Length)
        {
            return false;
        }

        stream.Position = 0;
        var buffer = new byte[PdfMagicBytes.Length];
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);

        if (bytesRead < PdfMagicBytes.Length)
        {
            return false;
        }

        for (int i = 0; i < PdfMagicBytes.Length; i++)
        {
            if (buffer[i] != PdfMagicBytes[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates PDF using Docnet.Core and extracts metadata.
    /// Returns errors if PDF structure is invalid, otherwise returns metadata.
    /// </summary>
    private (Dictionary<string, string> Errors, PdfMetadata? Metadata) ValidatePdfWithDocnet(string filePath)
    {
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        PdfMetadata? metadata = null;

        try
        {
            using var library = DocLib.Instance;
            using var docReader = library.GetDocReader(filePath, new PageDimensions(1080, 1920));

            var pageCount = docReader.GetPageCount();
            var pdfVersion = ExtractPdfVersion(filePath);

            metadata = new PdfMetadata(
                PageCount: pageCount,
                PdfVersion: pdfVersion,
                FileSizeBytes: new FileInfo(filePath).Length,
                HasText: false,  // Could be enhanced with text detection
                HasImages: false // Could be enhanced with image detection
            );
        }
        catch (InvalidOperationException ex)
        {
            errors["pdfStructure"] = $"Invalid PDF structure: {ex.Message}";
        }
        catch (ArgumentException ex)
        {
            errors["pdfStructure"] = $"Invalid PDF argument: {ex.Message}";
        }
        catch (NotSupportedException ex)
        {
            errors["pdfStructure"] = $"Unsupported PDF format: {ex.Message}";
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // VALIDATION PATTERN: All PDF parsing errors must be captured and returned as validation errors
        // PDF structure validation must convert all exceptions to user-friendly validation messages
#pragma warning restore S125
        catch (Exception ex)
        {
            errors["pdfStructure"] = $"Unable to read PDF structure: {ex.Message}";
        }
#pragma warning restore CA1031

        return (errors, metadata);
    }

    /// <summary>
    /// Extracts PDF version from file header.
    /// Reads first line looking for "%PDF-X.Y" format.
    /// </summary>
    private static string ExtractPdfVersion(string filePath)
    {
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(fileStream);

            var firstLine = reader.ReadLine();
            if (firstLine != null && firstLine.StartsWith("%PDF-", StringComparison.Ordinal))
            {
                return firstLine.Substring(5).Trim();
            }
        }
        catch
        {
            // Ignore errors, return empty string
        }

        return string.Empty;
    }

    /// <summary>
    /// Gets content type from file name extension.
    /// Defaults to "application/pdf" for .pdf files.
    /// </summary>
    private static string GetContentType(string fileName)
    {
        if (fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return "application/pdf";
        }

        // Fallback for other extensions (should be validated upstream)
        return "application/octet-stream";
    }

    /// <summary>
    /// Cleans up temporary file with best-effort error handling.
    /// Temp file deletion failures are logged but not thrown (cleanup pattern).
    /// </summary>
    private void CleanupTempFile(string tempFile)
    {
        try
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "I/O error deleting temporary validation file {TempFile}", tempFile);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied deleting temporary validation file {TempFile}", tempFile);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // CLEANUP PATTERN: Temp file deletion failures must not affect validation result
        // Validation is complete; file cleanup is best-effort only.
        // Rationale: Validation is already complete and result determined. Temporary file
        // cleanup is a best-effort operation - failing to delete the temp file should not
        // change the validation result or fail the upload. OS will eventually clean up temp
        // directory, and we log for monitoring filesystem issues.
        // Context: File failures are typically permission/locking (antivirus, backup processes)
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error deleting temporary validation file {TempFile}", tempFile);
        }
#pragma warning restore CA1031
    }
}

/// <summary>
/// Exception thrown when PDF validation fails unexpectedly.
/// Wraps underlying exceptions for consistent error handling.
/// </summary>
public class PdfValidationException : Exception
{
    public PdfValidationException(string message) : base(message) { }
    public PdfValidationException(string message, Exception innerException) : base(message, innerException) { }
    public PdfValidationException()
    {
    }
}

