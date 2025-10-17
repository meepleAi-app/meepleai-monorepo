using Docnet.Core;
using Docnet.Core.Models;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
/// Service for validating PDF files before upload and processing
/// Validates file type, size, page count, and PDF version compatibility
/// </summary>
public interface IPdfValidationService
{
    /// <summary>
    /// Performs comprehensive validation of a PDF stream
    /// </summary>
    /// <param name="pdfStream">Stream containing the PDF data</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Validation result with errors and metadata</returns>
    Task<PdfValidationResult> ValidateAsync(Stream pdfStream, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Validates file size against configured maximum
    /// </summary>
    /// <param name="fileSizeBytes">File size in bytes</param>
    /// <returns>Validation result</returns>
    PdfValidationResult ValidateFileSize(long fileSizeBytes);

    /// <summary>
    /// Validates MIME type against allowed content types
    /// </summary>
    /// <param name="contentType">MIME content type</param>
    /// <returns>Validation result</returns>
    PdfValidationResult ValidateMimeType(string contentType);
}

public class PdfValidationService : IPdfValidationService
{
    private readonly ILogger<PdfValidationService> _logger;
    private readonly PdfProcessingConfiguration _config;

    // PDF magic bytes signature: %PDF-
    private static readonly byte[] PdfMagicBytes = { 0x25, 0x50, 0x44, 0x46, 0x2D }; // %PDF-

    // Semaphore to prevent concurrent access to Docnet.Core (not thread-safe)
    private static readonly SemaphoreSlim DocnetSemaphore = new(1, 1);

    public PdfValidationService(
        ILogger<PdfValidationService> logger,
        IOptions<PdfProcessingConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public async Task<PdfValidationResult> ValidateAsync(Stream pdfStream, string fileName, CancellationToken ct = default)
    {
        if (pdfStream == null)
        {
            return PdfValidationResult.CreateFailure(new Dictionary<string, string>
            {
                ["stream"] = "PDF stream cannot be null"
            });
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return PdfValidationResult.CreateFailure(new Dictionary<string, string>
            {
                ["fileName"] = "File name cannot be empty"
            });
        }

        var errors = new Dictionary<string, string>();
        PdfMetadata? metadata = null;

        try
        {
            // Validate magic bytes
            if (!await ValidatePdfMagicBytesAsync(pdfStream, ct))
            {
                errors["fileFormat"] = "Invalid PDF file format. File does not start with PDF signature.";
            }

            // Reset stream position for further processing
            pdfStream.Position = 0;

            // Validate PDF content and extract metadata using Docnet.Core
            await DocnetSemaphore.WaitAsync(ct);
            try
            {
                // Save stream to temporary file for Docnet.Core processing
                var tempFile = Path.GetTempFileName();
                try
                {
                    using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await pdfStream.CopyToAsync(fileStream, ct);
                    }

                    // Reset stream position for potential further use
                    pdfStream.Position = 0;

                    // Validate with Docnet.Core
                    var validationResult = await Task.Run(() => ValidatePdfWithDocnet(tempFile), ct);

                    if (validationResult.Errors.Count > 0)
                    {
                        foreach (var error in validationResult.Errors)
                        {
                            errors[error.Key] = error.Value;
                        }
                    }

                    if (validationResult.Metadata != null)
                    {
                        metadata = validationResult.Metadata;

                        // Validate page count
                        if (metadata.PageCount < _config.MinPageCount)
                        {
                            errors["pageCount"] = $"PDF must have at least {_config.MinPageCount} page(s). Found {metadata.PageCount} page(s).";
                        }
                        else if (metadata.PageCount > _config.MaxPageCount)
                        {
                            errors["pageCount"] = $"PDF has {metadata.PageCount} pages, maximum allowed is {_config.MaxPageCount} pages.";
                        }

                        // Validate PDF version
                        if (!string.IsNullOrEmpty(metadata.PdfVersion))
                        {
                            var minVersion = ParsePdfVersion(_config.MinPdfVersion);
                            var fileVersion = ParsePdfVersion(metadata.PdfVersion);

                            if (fileVersion < minVersion)
                            {
                                errors["pdfVersion"] = $"PDF version {metadata.PdfVersion} is not supported. Minimum version required is {_config.MinPdfVersion}.";
                            }
                        }
                    }
                }
                finally
                {
                    // Clean up temporary file
                    try
                    {
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temporary validation file {TempFile}", tempFile);
                    }
                }
            }
            finally
            {
                DocnetSemaphore.Release();
            }

            if (errors.Count > 0)
            {
                _logger.LogWarning("PDF validation failed for {FileName}: {@Errors}", fileName, errors);
                return PdfValidationResult.CreateFailure(errors);
            }

            _logger.LogInformation("PDF validation successful for {FileName}: {PageCount} pages, version {Version}",
                fileName, metadata?.PageCount ?? 0, metadata?.PdfVersion ?? "unknown");

            return PdfValidationResult.CreateSuccess(metadata);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during PDF validation for {FileName}", fileName);
            errors["validation"] = $"PDF validation failed: {ex.Message}";
            return PdfValidationResult.CreateFailure(errors);
        }
    }

    public PdfValidationResult ValidateFileSize(long fileSizeBytes)
    {
        if (fileSizeBytes <= 0)
        {
            return PdfValidationResult.CreateFailure(new Dictionary<string, string>
            {
                ["fileSize"] = "File size must be greater than 0 bytes"
            });
        }

        if (fileSizeBytes > _config.MaxFileSizeBytes)
        {
            var sizeMB = fileSizeBytes / 1024.0 / 1024.0;
            var maxMB = _config.MaxFileSizeBytes / 1024.0 / 1024.0;
            return PdfValidationResult.CreateFailure(new Dictionary<string, string>
            {
                ["fileSize"] = $"File size ({sizeMB:F1} MB) exceeds maximum of {maxMB:F0} MB"
            });
        }

        return PdfValidationResult.CreateSuccess(new PdfMetadata
        {
            FileSizeBytes = fileSizeBytes,
            PageCount = 0,
            PdfVersion = string.Empty
        });
    }

    public PdfValidationResult ValidateMimeType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return PdfValidationResult.CreateFailure(new Dictionary<string, string>
            {
                ["fileType"] = "Content type cannot be empty"
            });
        }

        if (!_config.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return PdfValidationResult.CreateFailure(new Dictionary<string, string>
            {
                ["fileType"] = $"File type '{contentType}' is not allowed. Only PDF files (application/pdf) are supported."
            });
        }

        return PdfValidationResult.CreateSuccess(null);
    }

    /// <summary>
    /// Validates PDF magic bytes at the start of the stream
    /// </summary>
    private static async Task<bool> ValidatePdfMagicBytesAsync(Stream stream, CancellationToken ct)
    {
        if (stream.Length < PdfMagicBytes.Length)
        {
            return false;
        }

        stream.Position = 0;
        var buffer = new byte[PdfMagicBytes.Length];
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);

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
    /// Validates PDF using Docnet.Core and extracts metadata
    /// </summary>
    private (Dictionary<string, string> Errors, PdfMetadata? Metadata) ValidatePdfWithDocnet(string filePath)
    {
        var errors = new Dictionary<string, string>();
        PdfMetadata? metadata = null;

        try
        {
            using var library = DocLib.Instance;
            using var docReader = library.GetDocReader(filePath, new PageDimensions(1080, 1920));

            var pageCount = docReader.GetPageCount();
            var pdfVersion = ExtractPdfVersion(filePath);

            metadata = new PdfMetadata
            {
                PageCount = pageCount,
                PdfVersion = pdfVersion,
                FileSizeBytes = new FileInfo(filePath).Length
            };
        }
        catch (Exception ex)
        {
            errors["pdfStructure"] = $"Unable to read PDF structure: {ex.Message}";
        }

        return (errors, metadata);
    }

    /// <summary>
    /// Extracts PDF version from file header
    /// </summary>
    private static string ExtractPdfVersion(string filePath)
    {
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(fileStream);

            var firstLine = reader.ReadLine();
            if (firstLine != null && firstLine.StartsWith("%PDF-"))
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
    /// Parses PDF version string to comparable float value
    /// </summary>
    private static float ParsePdfVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return 0f;
        }

        // Remove any leading/trailing whitespace and parse
        version = version.Trim();

        if (float.TryParse(version, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return 0f;
    }
}

/// <summary>
/// Result of PDF validation operation
/// </summary>
public class PdfValidationResult
{
    public bool IsValid { get; set; }
    public Dictionary<string, string> Errors { get; set; } = new();
    public PdfMetadata? Metadata { get; set; }

    public static PdfValidationResult CreateSuccess(PdfMetadata? metadata) =>
        new()
        {
            IsValid = true,
            Metadata = metadata
        };

    public static PdfValidationResult CreateFailure(Dictionary<string, string> errors) =>
        new()
        {
            IsValid = false,
            Errors = errors
        };
}

/// <summary>
/// Metadata extracted from PDF during validation
/// </summary>
public class PdfMetadata
{
    public int PageCount { get; set; }
    public string PdfVersion { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
}

/// <summary>
/// Configuration for PDF processing and validation
/// </summary>
public class PdfProcessingConfiguration
{
    public long MaxFileSizeBytes { get; set; } = 104857600; // 100 MB
    public int MaxPageCount { get; set; } = 500;
    public int MinPageCount { get; set; } = 1;
    public string MinPdfVersion { get; set; } = "1.4";
    public List<string> AllowedContentTypes { get; set; } = new() { "application/pdf" };
}
