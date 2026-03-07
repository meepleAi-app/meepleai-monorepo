using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;

namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Application service for session photo attachment operations.
/// Issue #5361 - Upload, thumbnail generation, S3 integration.
/// </summary>
public interface ISessionAttachmentService
{
    /// <summary>
    /// Uploads a session photo, generates a thumbnail, and stores both in blob storage.
    /// </summary>
    Task<SessionAttachmentUploadResult> UploadAsync(
        Guid sessionId,
        Guid playerId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSize,
        AttachmentType attachmentType,
        string? caption,
        int? snapshotIndex,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a pre-signed download URL for a stored attachment (1h expiry).
    /// Returns the blob URL as-is for local storage.
    /// </summary>
    Task<string> GetDownloadUrlAsync(string blobUrl, CancellationToken ct = default);

    /// <summary>
    /// Deletes the original and thumbnail blobs from storage.
    /// </summary>
    Task DeleteBlobsAsync(string blobUrl, string? thumbnailUrl, CancellationToken ct = default);
}

/// <summary>
/// Result of a session attachment upload operation.
/// </summary>
public sealed record SessionAttachmentUploadResult(
    bool Success,
    string? BlobUrl,
    string? ThumbnailUrl,
    long FileSizeBytes,
    string? ErrorMessage = null);
