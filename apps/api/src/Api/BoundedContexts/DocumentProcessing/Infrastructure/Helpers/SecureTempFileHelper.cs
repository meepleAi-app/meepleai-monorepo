using System.Security.Cryptography;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Helpers;

/// <summary>
/// Helper class for secure temporary file operations.
/// Addresses S5445: Uses Path.GetRandomFileName() instead of Path.GetTempFileName()
/// to prevent TOCTOU (Time-of-check to time-of-use) race condition vulnerabilities.
/// </summary>
internal static class SecureTempFileHelper
{
    private const string TempSubdirectory = "meepleai-pdf";

    /// <summary>
    /// Creates a secure temporary file path using cryptographically random filename.
    /// Does NOT create the file - caller is responsible for file creation.
    /// </summary>
    /// <param name="extension">File extension (default: .tmp). Will be prefixed with '.' if missing.</param>
    /// <returns>Full path to a non-existent temporary file with random name.</returns>
    public static string CreateSecureTempFilePath(string extension = ".tmp")
    {
        // Normalize extension
        if (!string.IsNullOrEmpty(extension) && !extension.StartsWith('.'))
        {
            extension = "." + extension;
        }

        // Use dedicated subdirectory for easier cleanup
        var tempDir = Path.Combine(Path.GetTempPath(), TempSubdirectory);
        Directory.CreateDirectory(tempDir);

        // Path.GetRandomFileName() generates a cryptographically strong random 11-char name
        // Format: xxxxxxxx.xxx (8 chars + . + 3 chars)
        var randomFileName = Path.GetRandomFileName();
        var fileNameWithExtension = Path.ChangeExtension(randomFileName, extension);

        return Path.Combine(tempDir, fileNameWithExtension);
    }

    /// <summary>
    /// Creates a secure temporary file and returns both path and an open stream.
    /// Uses FileMode.CreateNew to ensure atomic creation (fails if file exists).
    /// </summary>
    /// <param name="extension">File extension (default: .tmp).</param>
    /// <returns>Tuple of file path and open FileStream. Caller must dispose the stream.</returns>
    public static (string FilePath, FileStream Stream) CreateSecureTempFile(string extension = ".tmp")
    {
        var filePath = CreateSecureTempFilePath(extension);

        // FileMode.CreateNew ensures atomic creation - fails if file already exists
        // This eliminates TOCTOU race condition
        var stream = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        return (filePath, stream);
    }

    /// <summary>
    /// Safely cleans up a temporary file with proper error handling.
    /// Failures are silently ignored (cleanup is best-effort).
    /// </summary>
    /// <param name="filePath">Path to the temporary file to delete.</param>
    public static void CleanupTempFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Best-effort cleanup - ignore errors
            // OS will eventually clean temp directory
        }
    }
}
