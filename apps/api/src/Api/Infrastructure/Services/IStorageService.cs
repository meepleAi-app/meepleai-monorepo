namespace Api.Infrastructure.Services;

/// <summary>
/// Service interface for file storage operations.
/// Issue #2732: Storage integration for document copying and management.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Copies a file from source path to destination path.
    /// </summary>
    /// <param name="sourcePath">Source file path.</param>
    /// <param name="destinationPath">Destination file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final storage path of the copied file.</returns>
    Task<string> CopyFile(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a time-limited preview URL for a file.
    /// </summary>
    /// <param name="storagePath">Storage path of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Preview URL (time-limited for security).</returns>
    Task<string> GetPreviewUrl(
        string storagePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a time-limited download URL for a file.
    /// </summary>
    /// <param name="storagePath">Storage path of the file.</param>
    /// <param name="expiration">URL expiration duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Download URL (time-limited for security).</returns>
    Task<string> GetDownloadUrl(
        string storagePath,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    /// <param name="storagePath">Storage path of the file to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteFile(
        string storagePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the size of a file in bytes.
    /// </summary>
    /// <param name="storagePath">Storage path of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File size in bytes.</returns>
    Task<long> GetFileSize(
        string storagePath,
        CancellationToken cancellationToken = default);
}
