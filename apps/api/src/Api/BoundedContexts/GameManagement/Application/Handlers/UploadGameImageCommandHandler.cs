using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles game image upload command.
/// Issue #2255: Implements file upload with validation and storage.
/// </summary>
internal class UploadGameImageCommandHandler : ICommandHandler<UploadGameImageCommand, UploadGameImageResult>
{
    private readonly IBlobStorageService _storageService;
    private readonly ILogger<UploadGameImageCommandHandler> _logger;

    // File size limits (bytes)
    private const long MaxIconSizeBytes = 2 * 1024 * 1024; // 2MB for icons
    private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5MB for images

    // Allowed MIME types
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/webp",
        "image/svg+xml"
    };

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
            var mimeType = GetMimeTypeFromFileName(command.FileName);
            if (mimeType == null || !AllowedMimeTypes.Contains(mimeType))
            {
                return new UploadGameImageResult(
                    Success: false,
                    FileId: null,
                    FileUrl: null,
                    FileSizeBytes: command.FileStream.Length,
                    ErrorMessage: "Invalid file type. Allowed: PNG, JPEG, WebP, SVG"
                );
            }

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

    private static string? GetMimeTypeFromFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => null
        };
    }
}
