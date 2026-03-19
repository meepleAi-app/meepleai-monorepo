using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Validators;
using Api.Infrastructure.Security;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Handles game image upload command.
/// Issue #2255: Implements file upload with validation and storage.
/// Security: Magic byte validation, path traversal prevention, size limits.
/// </summary>
internal class UploadGameImageCommandHandler : ICommandHandler<UploadGameImageCommand, UploadGameImageResult>
{
    private readonly IBlobStorageService _storageService;
    private readonly ILogger<UploadGameImageCommandHandler> _logger;

    // File size limits (bytes)
    private const long MaxIconSizeBytes = 2 * 1024 * 1024; // 2MB for icons
    private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5MB for images

    public UploadGameImageCommandHandler(
        IBlobStorageService storageService,
        ILogger<UploadGameImageCommandHandler> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UploadGameImageResult> Handle(
        UploadGameImageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            // SECURITY FIX: Validate gameId to prevent path traversal (CWE-22, CWE-73)
            // Code review finding: Defense-in-depth requires validation at handler level
            PathSecurity.ValidateIdentifier(command.GameId, nameof(command.GameId));

            // Validate file stream
            if (command.FileStream == null || !command.FileStream.CanRead)
            {
                return new UploadGameImageResult(
                    Success: false,
                    FileId: null,
                    FileUrl: null,
                    FileSizeBytes: 0,
                    ErrorMessage: "Invalid file stream"
                );
            }

            if (!command.FileStream.CanSeek)
            {
                return new UploadGameImageResult(
                    Success: false,
                    FileId: null,
                    FileUrl: null,
                    FileSizeBytes: 0,
                    ErrorMessage: "Stream must be seekable for validation"
                );
            }

            // SECURITY FIX: Reset stream position to ensure accurate validation
            // Code review finding: Stream position may be advanced after Length check
            command.FileStream.Position = 0;

            // Validate file size
            var maxSize = command.ImageType == ImageType.Icon ? MaxIconSizeBytes : MaxImageSizeBytes;
            if (command.FileStream.Length > maxSize)
            {
                var maxSizeMB = maxSize / (1024.0 * 1024.0);
                return new UploadGameImageResult(
                    Success: false,
                    FileId: null,
                    FileUrl: null,
                    FileSizeBytes: command.FileStream.Length,
                    ErrorMessage: $"File size exceeds maximum allowed ({maxSizeMB:F1}MB)"
                );
            }

            // Validate MIME type from file extension
            var mimeType = ImageFileValidator.GetMimeTypeFromFileName(command.FileName);
            if (mimeType == null || !ImageFileValidator.AllowedMimeTypes.Contains(mimeType))
            {
                return new UploadGameImageResult(
                    Success: false,
                    FileId: null,
                    FileUrl: null,
                    FileSizeBytes: command.FileStream.Length,
                    ErrorMessage: "Invalid file type. Allowed: PNG, JPEG, WebP (SVG excluded for security)"
                );
            }

            // SECURITY FIX: Validate file content using magic bytes (not just extension)
            // Code review finding: Extension-only validation vulnerable to renamed malicious files
            var isValidImage = await ImageFileValidator.ValidateMagicBytesAsync(
                command.FileStream,
                mimeType
            ).ConfigureAwait(false);

            if (!isValidImage)
            {
                _logger.LogWarning(
                    "File {FileName} claimed to be {MimeType} but magic bytes validation failed",
                    command.FileName,
                    mimeType
                );

                return new UploadGameImageResult(
                    Success: false,
                    FileId: null,
                    FileUrl: null,
                    FileSizeBytes: command.FileStream.Length,
                    ErrorMessage: "File content does not match declared type. Possible malicious file."
                );
            }

            // Stream position is already reset to 0 by ValidateMagicBytesAsync

            // Store file using BlobStorageService
            var storageResult = await _storageService.StoreAsync(
                stream: command.FileStream,
                fileName: command.FileName,
                gameId: command.GameId,
                ct: cancellationToken
            ).ConfigureAwait(false);

            if (!storageResult.Success)
            {
                _logger.LogError(
                    "Failed to store {ImageType} for game {GameId}: {Error}",
                    command.ImageType,
                    command.GameId,
                    storageResult.ErrorMessage
                );

                return new UploadGameImageResult(
                    Success: false,
                    FileId: null,
                    FileUrl: null,
                    FileSizeBytes: command.FileStream.Length,
                    ErrorMessage: storageResult.ErrorMessage ?? "Storage operation failed"
                );
            }

            // Generate URL for the uploaded file
            // Note: In production, this should be a CDN URL or static file serving endpoint
            var fileUrl = $"/api/v1/games/{command.GameId}/files/{storageResult.FileId}";

            _logger.LogInformation(
                "Successfully uploaded {ImageType} for game {GameId}: {FileId} ({Size} bytes)",
                command.ImageType,
                command.GameId,
                storageResult.FileId,
                storageResult.FileSizeBytes
            );

            return new UploadGameImageResult(
                Success: true,
                FileId: storageResult.FileId,
                FileUrl: fileUrl,
                FileSizeBytes: storageResult.FileSizeBytes
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error uploading {ImageType} for game {GameId}",
                command.ImageType,
                command.GameId
            );

            return new UploadGameImageResult(
                Success: false,
                FileId: null,
                FileUrl: null,
                FileSizeBytes: 0,
                ErrorMessage: "An unexpected error occurred during file upload"
            );
        }
    }
}
