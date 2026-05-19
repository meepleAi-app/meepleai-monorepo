

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services.Pdf;

/// <summary>
/// Logical category for blob storage objects. Each category maps to a distinct
/// S3 prefix via <see cref="BlobCategoryExtensions.ToS3Folder"/> so PDF, session photos,
/// game images, vision snapshots and gamebook photos no longer share the legacy
/// PDF-specific prefix.
/// </summary>
/// <remarks>
/// Issue #1314 PR 1: this enum is added to the public surface of IBlobStorageService
/// but the S3 key layout is INTENTIONALLY unchanged in PR 1 (behavior-preserving
/// refactor). PR 2 of issue #1314 wires <see cref="BlobCategoryExtensions.ToS3Folder"/>
/// into <see cref="S3BlobStorageService"/> behind feature flag for the 5-phase rollout.
/// </remarks>
public enum BlobCategory
{
    /// <summary>PDF documents (rulebooks, manuals). Legacy prefix <c>pdf_uploads/</c>, target <c>pdfs/</c>.</summary>
    Pdf,

    /// <summary>Session photos uploaded during a game night. Legacy prefix <c>pdf_uploads/session-photos-{sessionId}/</c>, target <c>session-photos/{sessionId}/</c>.</summary>
    SessionPhoto,

    /// <summary>Game catalog images (cover, thumbnail, hero). Legacy prefix <c>pdf_uploads/{gameId}/</c>, target <c>game-images/{gameId}/</c>.</summary>
    GameImage,

    /// <summary>Vision snapshot captures (in-session board states). Target prefix <c>vision-snapshots/{snapshotId}/</c>.</summary>
    VisionSnapshot,

    /// <summary>Gamebook scanned photos. Target prefix <c>gamebook-photos/{gamebookId}/</c>.</summary>
    GamebookPhoto,

    /// <summary>
    /// JPEG photo batches uploaded for OCR + text extraction (Libro Game AI Assistant camera pipeline).
    /// Each batch is N per-page captures of a physical rulebook/gamebook. Distinct from
    /// <see cref="GamebookPhoto"/> (which is per-photo session storage) and <see cref="Pdf"/>
    /// (which is the binary PDF document itself). Target prefix <c>photo-batches/{gameId}/</c>.
    /// </summary>
    PhotoBatch,
}

internal static class BlobCategoryExtensions
{
    /// <summary>
    /// Returns the canonical S3 folder prefix for the given category. Used by PR 2
    /// of issue #1314 when feature flag <c>STORAGE_WRITE_MODE=new</c>. PR 1 does not
    /// consume this value — keys are still rendered via the legacy <c>pdf_uploads/</c>
    /// prefix to preserve behavior.
    /// </summary>
    public static string ToS3Folder(this BlobCategory category) => category switch
    {
        BlobCategory.Pdf => "pdfs",
        BlobCategory.SessionPhoto => "session-photos",
        BlobCategory.GameImage => "game-images",
        BlobCategory.VisionSnapshot => "vision-snapshots",
        BlobCategory.GamebookPhoto => "gamebook-photos",
        BlobCategory.PhotoBatch => "photo-batches",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };
}

/// <summary>
/// Generic file storage service for managing blob storage operations
/// Reusable for any file type beyond PDFs
/// </summary>
internal interface IBlobStorageService
{
    /// <summary>
    /// Stores a file from a stream.
    /// </summary>
    /// <param name="stream">File stream to store.</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="category">Blob category for folder organization (PR 2 wiring).</param>
    /// <param name="resourceKey">Opaque resource identifier (e.g. pdfId, sessionId, gameId) used as folder key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Storage result with file ID and path.</returns>
    Task<BlobStorageResult> StoreAsync(Stream stream, string fileName, BlobCategory category, string resourceKey, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a file stream by file ID.
    /// </summary>
    /// <param name="fileId">File ID to retrieve.</param>
    /// <param name="category">Blob category for folder organization (PR 2 wiring).</param>
    /// <param name="resourceKey">Opaque resource identifier used as folder key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>File stream or null if not found.</returns>
    /// <remarks>
    /// IMPORTANT: Caller MUST dispose the returned stream to prevent resource leaks.
    /// For S3 storage, this also disposes the underlying GetObjectResponse connection.
    /// </remarks>
    Task<Stream?> RetrieveAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file by file ID.
    /// </summary>
    /// <param name="fileId">File ID to delete.</param>
    /// <param name="category">Blob category for folder organization (PR 2 wiring).</param>
    /// <param name="resourceKey">Opaque resource identifier used as folder key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if deleted successfully.</returns>
    Task<bool> DeleteAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken ct = default);

    /// <summary>
    /// Gets the storage path for a file.
    /// </summary>
    /// <param name="fileId">File ID.</param>
    /// <param name="category">Blob category for folder organization (PR 2 wiring).</param>
    /// <param name="resourceKey">Opaque resource identifier used as folder key.</param>
    /// <param name="fileName">Original file name.</param>
    /// <returns>Full storage path.</returns>
    string GetStoragePath(string fileId, BlobCategory category, string resourceKey, string fileName);

    /// <summary>
    /// Checks if a file exists asynchronously.
    /// </summary>
    /// <param name="fileId">File ID to check.</param>
    /// <param name="category">Blob category for folder organization (PR 2 wiring).</param>
    /// <param name="resourceKey">Opaque resource identifier used as folder key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if file exists.</returns>
    Task<bool> ExistsAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a pre-signed URL for secure, temporary file downloads.
    /// Returns null for local storage (use API download endpoint instead).
    /// </summary>
    /// <param name="fileId">File ID to generate URL for.</param>
    /// <param name="category">Blob category for folder organization (PR 2 wiring).</param>
    /// <param name="resourceKey">Opaque resource identifier used as folder key.</param>
    /// <param name="expirySeconds">URL expiration time (optional, defaults to configured value).</param>
    /// <returns>Pre-signed download URL, or null if not supported or file not found.</returns>
    Task<string?> GetPresignedDownloadUrlAsync(string fileId, BlobCategory category, string resourceKey, int? expirySeconds = null);
}

/// <summary>
/// Result of blob storage operation
/// </summary>
internal record BlobStorageResult(
    bool Success,
    string? FileId,
    string? FilePath,
    long FileSizeBytes,
    string? ErrorMessage = null);
