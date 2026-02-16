

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services.Pdf;

/// <summary>
/// Generic file storage service for managing blob storage operations
/// Reusable for any file type beyond PDFs
/// </summary>
internal interface IBlobStorageService
{
    /// <summary>
    /// Stores a file from a stream
    /// </summary>
    /// <param name="stream">File stream to store</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="gameId">Game ID for organization</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Storage result with file ID and path</returns>
    Task<BlobStorageResult> StoreAsync(Stream stream, string fileName, string gameId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a file stream by file ID
    /// </summary>
    /// <param name="fileId">File ID to retrieve</param>
    /// <param name="gameId">Game ID for organization</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>File stream or null if not found</returns>
    /// <remarks>
    /// IMPORTANT: Caller MUST dispose the returned stream to prevent resource leaks.
    /// For S3 storage, this also disposes the underlying GetObjectResponse connection.
    /// </remarks>
    Task<Stream?> RetrieveAsync(string fileId, string gameId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file by file ID
    /// </summary>
    /// <param name="fileId">File ID to delete</param>
    /// <param name="gameId">Game ID for organization</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteAsync(string fileId, string gameId, CancellationToken ct = default);

    /// <summary>
    /// Gets the storage path for a file
    /// </summary>
    /// <param name="fileId">File ID</param>
    /// <param name="gameId">Game ID for organization</param>
    /// <param name="fileName">Original file name</param>
    /// <returns>Full storage path</returns>
    string GetStoragePath(string fileId, string gameId, string fileName);

    /// <summary>
    /// Checks if a file exists asynchronously
    /// </summary>
    /// <param name="fileId">File ID to check</param>
    /// <param name="gameId">Game ID for organization</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if file exists</returns>
    Task<bool> ExistsAsync(string fileId, string gameId, CancellationToken cancellationToken = default);
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
