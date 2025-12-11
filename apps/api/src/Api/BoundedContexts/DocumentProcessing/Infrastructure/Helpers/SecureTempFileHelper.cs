namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Helpers;

/// <summary>
/// Helper for secure temporary file creation.
/// Addresses S5445: 'Path.GetTempFileName()' is insecure.
/// Uses Path.GetRandomFileName() with dedicated subdirectory for security.
/// </summary>
public static class SecureTempFileHelper
{
    private const string TempSubdirectory = "meepleai-pdf";

    /// <summary>
    /// Creates a secure temporary file path.
    /// Unlike Path.GetTempFileName(), this approach:
    /// - Uses cryptographically random file names (Path.GetRandomFileName)
    /// - Creates files in a dedicated subdirectory for isolation
    /// - Does not create the file (caller creates with proper permissions)
    /// </summary>
    /// <param name="extension">Optional file extension (e.g., ".pdf"). Defaults to ".tmp".</param>
    /// <returns>Full path to the secure temporary file location.</returns>
    public static string CreateSecureTempFilePath(string extension = ".tmp")
    {
        // Ensure extension starts with a dot
        if (!string.IsNullOrEmpty(extension) && !extension.StartsWith('.'))
        {
            extension = "." + extension;
        }

        // Create dedicated subdirectory for isolation
        var tempDir = Path.Combine(Path.GetTempPath(), TempSubdirectory);
        Directory.CreateDirectory(tempDir);

        // Use cryptographically random file name (S5445 fix)
        var randomFileName = Path.GetRandomFileName();
        var fileNameWithExtension = Path.ChangeExtension(randomFileName, extension);

        return Path.Combine(tempDir, fileNameWithExtension);
    }

    /// <summary>
    /// Creates a secure temporary file and returns an open FileStream.
    /// The file is created with exclusive access to prevent race conditions.
    /// </summary>
    /// <param name="extension">Optional file extension (e.g., ".pdf"). Defaults to ".tmp".</param>
    /// <returns>Open FileStream to the secure temporary file.</returns>
    public static FileStream CreateSecureTempFile(string extension = ".tmp")
    {
        var filePath = CreateSecureTempFilePath(extension);

        // Create file with exclusive access (prevents TOCTOU race conditions)
        return new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.DeleteOnClose
        );
    }

    /// <summary>
    /// Creates a secure temporary file that persists after the stream is closed.
    /// Caller is responsible for cleanup via CleanupTempFile.
    /// </summary>
    /// <param name="extension">Optional file extension (e.g., ".pdf"). Defaults to ".tmp".</param>
    /// <returns>Tuple of (FileStream, FilePath) for the secure temporary file.</returns>
    public static (FileStream Stream, string Path) CreatePersistentSecureTempFile(string extension = ".tmp")
    {
        var filePath = CreateSecureTempFilePath(extension);

        // Create file with exclusive access during write, allow read after
        var stream = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 4096
        );

        return (stream, filePath);
    }

    /// <summary>
    /// Cleans up the MeepleAI temp directory.
    /// Safe to call periodically for maintenance.
    /// </summary>
    public static void CleanupTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), TempSubdirectory);
        if (Directory.Exists(tempDir))
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (IOException)
            {
                // Directory may be in use, ignore
            }
            catch (UnauthorizedAccessException)
            {
                // Insufficient permissions, ignore
            }
        }
    }
}
