using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Application.Validators;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.Services.Pdf;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// Session photo attachment service with S3 storage and thumbnail generation.
/// Issue #5361 - Upload, thumbnail, pre-signed URL, blob deletion.
/// </summary>
internal sealed class SessionAttachmentService : ISessionAttachmentService
{
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<SessionAttachmentService> _logger;

    private const int ThumbnailMaxDimension = 300;
    private const int ThumbnailJpegQuality = 80;
    private const int DownloadUrlExpirySeconds = 3600; // 1 hour

    public SessionAttachmentService(
        IBlobStorageService blobStorage,
        ILogger<SessionAttachmentService> logger)
    {
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SessionAttachmentUploadResult> UploadAsync(
        Guid sessionId,
        Guid playerId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSize,
        AttachmentType attachmentType,
        string? caption,
        int? snapshotIndex,
        CancellationToken ct = default)
    {
        // Validate content type
        if (!IsAllowedContentType(contentType))
        {
            return new SessionAttachmentUploadResult(false, null, null, 0,
                "Invalid content type. Only image/jpeg and image/png are allowed.");
        }

        // Validate magic bytes
        if (fileStream.CanSeek)
        {
            var isValid = await ImageFileValidator.ValidateMagicBytesAsync(fileStream, contentType)
                .ConfigureAwait(false);
            if (!isValid)
            {
                return new SessionAttachmentUploadResult(false, null, null, 0,
                    "File content does not match declared content type.");
            }
        }

        // Use sessionId as the storage folder key
        var storageFolder = $"session-photos-{sessionId:N}";
        var extension = GetExtensionFromContentType(contentType);
        var photoFileName = $"{playerId:N}_{Guid.NewGuid():N}{extension}";

        // Upload original
        var originalResult = await _blobStorage.StoreAsync(
            fileStream, photoFileName, storageFolder, ct).ConfigureAwait(false);

        if (!originalResult.Success)
        {
            _logger.LogError(
                "Failed to upload session attachment for session {SessionId}, player {PlayerId}: {Error}",
                sessionId, playerId, originalResult.ErrorMessage);
            return new SessionAttachmentUploadResult(false, null, null, 0, originalResult.ErrorMessage);
        }

        // Generate and upload thumbnail
        string? thumbnailBlobUrl = null;
        try
        {
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            using var thumbnailStream = await GenerateThumbnailAsync(fileStream, ct).ConfigureAwait(false);
            if (thumbnailStream != null)
            {
                var thumbFileName = $"{playerId:N}_{Guid.NewGuid():N}_thumb.jpg";
                var thumbResult = await _blobStorage.StoreAsync(
                    thumbnailStream, thumbFileName, storageFolder, ct).ConfigureAwait(false);

                if (thumbResult.Success)
                {
                    thumbnailBlobUrl = thumbResult.FilePath;
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to upload thumbnail for session {SessionId}: {Error}",
                        sessionId, thumbResult.ErrorMessage);
                }
            }
        }
#pragma warning disable CA1031 // Thumbnail failure is non-critical
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Thumbnail generation failed for session {SessionId}, player {PlayerId}. Continuing without thumbnail.",
                sessionId, playerId);
        }
#pragma warning restore CA1031

        _logger.LogInformation(
            "Uploaded session attachment: session={SessionId}, player={PlayerId}, size={Size}, thumbnail={HasThumb}",
            sessionId, playerId, originalResult.FileSizeBytes, thumbnailBlobUrl != null);

        return new SessionAttachmentUploadResult(
            true,
            originalResult.FilePath,
            thumbnailBlobUrl,
            originalResult.FileSizeBytes);
    }

    public async Task<string> GetDownloadUrlAsync(string blobUrl, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobUrl);

        // Try to get a pre-signed URL from S3 storage
        // For local storage, GetPresignedDownloadUrlAsync returns null
        var (fileId, folder) = ParseBlobPath(blobUrl);
        if (fileId != null && folder != null)
        {
            var presignedUrl = await _blobStorage.GetPresignedDownloadUrlAsync(
                fileId, folder, DownloadUrlExpirySeconds).ConfigureAwait(false);
            if (presignedUrl != null)
            {
                return presignedUrl;
            }
        }

        // Fallback: return the blob URL as-is (for local storage / API download endpoint)
        return blobUrl;
    }

    public async Task DeleteBlobsAsync(string blobUrl, string? thumbnailUrl, CancellationToken ct = default)
    {
        // Delete original
        await DeleteSingleBlobAsync(blobUrl).ConfigureAwait(false);

        // Delete thumbnail if present
        if (!string.IsNullOrWhiteSpace(thumbnailUrl))
        {
            await DeleteSingleBlobAsync(thumbnailUrl).ConfigureAwait(false);
        }
    }

    private async Task DeleteSingleBlobAsync(string blobPath)
    {
        var (fileId, folder) = ParseBlobPath(blobPath);
        if (fileId != null && folder != null)
        {
            var deleted = await _blobStorage.DeleteAsync(fileId, folder).ConfigureAwait(false);
            if (!deleted)
            {
                _logger.LogWarning("Failed to delete blob at path: {Path}", blobPath);
            }
        }
        else
        {
            _logger.LogWarning("Could not parse blob path for deletion: {Path}", blobPath);
        }
    }

    internal static async Task<MemoryStream?> GenerateThumbnailAsync(Stream sourceStream, CancellationToken ct = default)
    {
        using var image = await Image.LoadAsync(sourceStream, ct).ConfigureAwait(false);

        // Calculate new dimensions maintaining aspect ratio
        var (newWidth, newHeight) = CalculateThumbnailDimensions(image.Width, image.Height);

        image.Mutate(ctx => ctx.Resize(newWidth, newHeight));

        var outputStream = new MemoryStream();
        var encoder = new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
        {
            Quality = ThumbnailJpegQuality
        };
        await image.SaveAsync(outputStream, encoder, ct).ConfigureAwait(false);
        outputStream.Position = 0;
        return outputStream;
    }

    internal static (int width, int height) CalculateThumbnailDimensions(int originalWidth, int originalHeight)
    {
        if (originalWidth <= ThumbnailMaxDimension && originalHeight <= ThumbnailMaxDimension)
        {
            return (originalWidth, originalHeight);
        }

        double ratio;
        if (originalWidth >= originalHeight)
        {
            ratio = (double)ThumbnailMaxDimension / originalWidth;
        }
        else
        {
            ratio = (double)ThumbnailMaxDimension / originalHeight;
        }

        return (Math.Max(1, (int)(originalWidth * ratio)), Math.Max(1, (int)(originalHeight * ratio)));
    }

    private static bool IsAllowedContentType(string contentType)
    {
        return string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetExtensionFromContentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            _ => ".bin"
        };
    }

    /// <summary>
    /// Parses a blob storage path into fileId and folder components.
    /// Local storage paths: basePath/folder/fileId_filename
    /// S3 paths: folder/fileId_filename
    /// </summary>
    private static (string? fileId, string? folder) ParseBlobPath(string blobPath)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
            return (null, null);

        // Extract the file name component
        var fileName = Path.GetFileName(blobPath);
        if (string.IsNullOrEmpty(fileName))
            return (null, null);

        // FileId is everything before the first underscore
        var underscoreIndex = fileName.IndexOf('_', StringComparison.Ordinal);
        if (underscoreIndex <= 0)
            return (null, null);

        var fileId = fileName[..underscoreIndex];

        // Folder is the parent directory name
        var directory = Path.GetDirectoryName(blobPath);
        if (string.IsNullOrEmpty(directory))
            return (null, null);

        var folder = Path.GetFileName(directory);
        if (string.IsNullOrEmpty(folder))
            return (null, null);

        return (fileId, folder);
    }
}
