using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure.Security;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for UploadPdfForGameExtractionCommand.
/// Uploads PDF to temporary storage for wizard metadata extraction.
/// Simplified version without quota checks or background processing.
/// Issue #4154: Upload PDF Command for Game Metadata Extraction Wizard
/// </summary>
internal sealed class UploadPdfForGameExtractionCommandHandler
    : ICommandHandler<UploadPdfForGameExtractionCommand, TempPdfUploadResult>
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<UploadPdfForGameExtractionCommandHandler> _logger;
    private readonly long _maxFileSizeBytes;
    private static readonly HashSet<string> AllowedContentTypes = ["application/pdf"];

    public UploadPdfForGameExtractionCommandHandler(
        IBlobStorageService blobStorageService,
        ILogger<UploadPdfForGameExtractionCommandHandler> logger,
        IOptions<PdfProcessingOptions> pdfOptions)
    {
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(pdfOptions);

        // Use LargePdfThresholdBytes (50 MB) for wizard uploads for faster processing feedback
        // Wizard uploads are temporary and don't need the full 100 MB limit of library uploads
        _maxFileSizeBytes = pdfOptions.Value.LargePdfThresholdBytes;
    }

    public async Task<TempPdfUploadResult> Handle(
        UploadPdfForGameExtractionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var file = command.File;

        // Validate file input
        var validationResult = await ValidateFileInputAsync(file, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return new TempPdfUploadResult(
                Success: false,
                FileId: null,
                FilePath: null,
                FileSizeBytes: file?.Length ?? 0,
                ErrorMessage: validationResult.ErrorMessage);
        }

        var sanitizedFileName = validationResult.SanitizedFileName!;
        var fileId = Guid.NewGuid();

        // Store file in temporary location
        // GameId "wizard-temp" is used for wizard temporary uploads
        try
        {
            using var fileStream = file!.OpenReadStream();
            var storageResult = await _blobStorageService.StoreAsync(
                fileStream,
                sanitizedFileName,
                gameId: "wizard-temp",
                ct: cancellationToken).ConfigureAwait(false);

            if (!storageResult.Success)
            {
                _logger.LogError("Failed to store temporary PDF for wizard extraction. FileId: {FileId}, Error: {Error}",
                    fileId, storageResult.ErrorMessage);

                return new TempPdfUploadResult(
                    Success: false,
                    FileId: null,
                    FilePath: null,
                    FileSizeBytes: file.Length,
                    ErrorMessage: storageResult.ErrorMessage ?? "Failed to store file");
            }

            _logger.LogInformation("Successfully stored temporary PDF for wizard extraction. FileId: {FileId}, Path: {Path}, Size: {SizeBytes}",
                fileId, storageResult.FilePath, storageResult.FileSizeBytes);

            return new TempPdfUploadResult(
                Success: true,
                FileId: fileId,
                FilePath: storageResult.FilePath,
                FileSizeBytes: storageResult.FileSizeBytes,
                ErrorMessage: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected error storing temporary PDF for wizard extraction. FileId: {FileId}", fileId);

            return new TempPdfUploadResult(
                Success: false,
                FileId: null,
                FilePath: null,
                FileSizeBytes: file!.Length,
                ErrorMessage: "An unexpected error occurred while storing the file. Please try again.");
        }
    }

    /// <summary>
    /// Validates file input: size, type, structure, filename.
    /// Returns validation result with sanitized filename.
    /// </summary>
    private async Task<(bool IsValid, string? ErrorMessage, string? SanitizedFileName)> ValidateFileInputAsync(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return (false, "No file provided. Please select a PDF file to upload.", null);
        }

        if (file.Length > _maxFileSizeBytes)
        {
            var sizeMB = file.Length / 1024.0 / 1024.0;
            var maxMB = _maxFileSizeBytes / 1024 / 1024;
            return (false, $"File is too large ({sizeMB:F1}MB). Maximum size is {maxMB}MB.", null);
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return (false, $"Invalid file type ({file.ContentType}). Only PDF files are allowed.", null);
        }

        // Validate PDF file structure
        using (var validationStream = file.OpenReadStream())
        {
            var (isValid, validationError) = await ValidatePdfStructureAsync(validationStream, cancellationToken)
                .ConfigureAwait(false);
            if (!isValid)
            {
                return (false, validationError!, null);
            }
        }

        // Sanitize filename for security (SEC-738)
        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return (false, "Invalid file name. The file must have a valid name.", null);
        }

        try
        {
            fileName = PathSecurity.SanitizeFilename(fileName);
        }
        catch (ArgumentException ex)
        {
            return (false, $"Invalid file name: {ex.Message}", null);
        }

        return (true, null, fileName);
    }

    /// <summary>
    /// Validates PDF file structure by checking for required PDF headers and trailers.
    /// Prevents upload of corrupted or malformed files.
    /// </summary>
    private static async Task<(bool IsValid, string? ErrorMessage)> ValidatePdfStructureAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        const int headerCheckBytes = 1024;
        const int trailerCheckBytes = 1024;

        try
        {
            // Check minimum file size
            if (stream.Length < 50)
            {
                return (false, "Invalid PDF file: File is too small to be a valid PDF (minimum 50 bytes required).");
            }

            // Read beginning for PDF header
            stream.Seek(0, SeekOrigin.Begin);
            var headerBuffer = new byte[Math.Min(headerCheckBytes, (int)stream.Length)];
            var headerBytesRead = await stream.ReadAsync(headerBuffer.AsMemory(0, headerBuffer.Length), cancellationToken)
                .ConfigureAwait(false);

            // Check for PDF header signature (%PDF-1.x)
            var headerText = System.Text.Encoding.ASCII.GetString(headerBuffer, 0, Math.Min(10, headerBytesRead));
            if (!headerText.StartsWith("%PDF-", StringComparison.Ordinal))
            {
                return (false, "Invalid PDF file: Missing PDF header signature. The file may be corrupted or not a valid PDF.");
            }

            // Read end for PDF trailer (%%EOF)
            if (stream.Length > trailerCheckBytes)
            {
                stream.Seek(-trailerCheckBytes, SeekOrigin.End);
            }
            else
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            var trailerBuffer = new byte[Math.Min(trailerCheckBytes, (int)stream.Length)];
            var trailerBytesRead = await stream.ReadAsync(trailerBuffer.AsMemory(0, trailerBuffer.Length), cancellationToken)
                .ConfigureAwait(false);

            var trailerText = System.Text.Encoding.ASCII.GetString(trailerBuffer, 0, trailerBytesRead);
            if (!trailerText.Contains("%%EOF", StringComparison.Ordinal))
            {
                return (false, "Invalid PDF file: Missing EOF marker. The file may be truncated or corrupted.");
            }

            return (true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, $"Error validating PDF structure: {ex.Message}");
        }
    }
}
