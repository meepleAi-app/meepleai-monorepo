using Api.Helpers;
using Api.Infrastructure.Security;

namespace Api.Services.Pdf;

/// <summary>
/// Generic file storage service for managing blob storage operations
/// Reusable for any file type beyond PDFs
/// </summary>
internal class BlobStorageService : IBlobStorageService
{
    private readonly string _storageBasePath;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(
        IConfiguration config,
        ILogger<BlobStorageService> logger)
    {
        _logger = logger;
        _storageBasePath = config["PDF_STORAGE_PATH"] ?? Path.Combine(Directory.GetCurrentDirectory(), "pdf_uploads");

        // Ensure storage directory exists
        if (!Directory.Exists(_storageBasePath))
        {
            Directory.CreateDirectory(_storageBasePath);
            _logger.LogInformation("Created blob storage directory at {Path}", _storageBasePath);
        }
    }

    public async Task<BlobStorageResult> StoreAsync(Stream stream, string fileName, string gameId, CancellationToken ct = default)
    {
        try
        {
            // SECURITY: Validate gameId to prevent path traversal
            PathSecurity.ValidateIdentifier(gameId, nameof(gameId));

            var fileId = Guid.NewGuid().ToString("N");
            var gameDir = PathSecurity.ValidatePathIsInDirectory(_storageBasePath, gameId);
            Directory.CreateDirectory(gameDir);

            var sanitizedFileName = SanitizeFileName(fileName);
            var filePath = Path.Combine(gameDir, $"{fileId}_{sanitizedFileName}");

            // Save file to disk
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await stream.CopyToAsync(fileStream, ct).ConfigureAwait(false);
            }

            var fileSize = new FileInfo(filePath).Length;

            _logger.LogInformation("Saved file to {FilePath} (size: {Size} bytes)", filePath, fileSize);

            return new BlobStorageResult(true, fileId, filePath, fileSize);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error storing file for game {GameId}", gameId);
            return new BlobStorageResult(false, null, null, 0, $"I/O error: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied storing file for game {GameId}", gameId);
            return new BlobStorageResult(false, null, null, 0, "Access denied to storage location");
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // SERVICE BOUNDARY PATTERN: Blob storage service boundary - must handle all file system errors gracefully
            // Rationale: This is a service entry point that interacts with the file system. File operations can
            // throw various runtime exceptions (disk full, permissions, antivirus locks, path too long). We must
            // catch all exceptions to return error results instead of crashing the service.
            // Context: File system operations can fail in unpredictable ways across different OS environments
            _logger.LogError(ex, "Unexpected error storing file for game {GameId}", gameId);
            return new BlobStorageResult(false, null, null, 0, ex.Message);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public Task<Stream?> RetrieveAsync(string fileId, string gameId, CancellationToken ct = default)
    {
        FileStream? fileStream = null;
        try
        {
            // SECURITY: Validate parameters to prevent path traversal (SEC-738, CWE-22, CWE-73)
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(gameId, nameof(gameId));

            var gameDir = PathSecurity.ValidatePathIsInDirectory(_storageBasePath, gameId);
            var files = Directory.GetFiles(gameDir, $"{fileId}_*");

            if (files.Length == 0)
            {
                _logger.LogWarning("File not found for {FileId} in game {GameId}", fileId, gameId);
                return Task.FromResult<Stream?>(null);
            }

            var filePath = files[0];
            fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult<Stream?>(fileStream);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // SERVICE BOUNDARY PATTERN: Blob storage service boundary - must handle all file system errors gracefully
            // Rationale: This is a service entry point that retrieves files from the file system. File operations
            // can throw various runtime exceptions (file locked, permissions, disk errors, path issues). We must
            // catch all exceptions to return null instead of crashing the service.
            // Context: File system operations can fail in unpredictable ways across different OS environments

            // RESOURCE LEAK FIX: Dispose FileStream if created but exception occurred before returning
            fileStream?.Dispose();

            _logger.LogError(ex, "Error retrieving file {FileId} for game {GameId}", fileId, gameId);
            return Task.FromResult<Stream?>(null);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public async Task<bool> DeleteAsync(string fileId, string gameId, CancellationToken ct = default)
    {
        try
        {
            // SECURITY: Validate parameters to prevent path traversal (SEC-738, CWE-22, CWE-73)
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(gameId, nameof(gameId));

            var gameDir = PathSecurity.ValidatePathIsInDirectory(_storageBasePath, gameId);
            var files = Directory.GetFiles(gameDir, $"{fileId}_*");

            if (files.Length == 0)
            {
                _logger.LogWarning("File not found for deletion: {FileId} in game {GameId}", fileId, gameId);
                return false;
            }

            foreach (var file in files)
            {
                File.Delete(file);
                _logger.LogInformation("Deleted file at {FilePath}", file);
            }

            return await Task.FromResult(true).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "I/O error deleting file {FileId} for game {GameId}", fileId, gameId);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied deleting file {FileId} for game {GameId}", fileId, gameId);
            return false;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // SERVICE BOUNDARY PATTERN: Blob storage service boundary - must handle all file system errors gracefully
            // Rationale: This is a service entry point that deletes files from the file system. File operations
            // can throw various runtime exceptions (file locked, permissions, antivirus blocks, disk errors). We
            // must catch all exceptions to return false instead of crashing the service.
            // Context: File system operations can fail in unpredictable ways across different OS environments
            _logger.LogWarning(ex, "Unexpected error deleting file {FileId} for game {GameId}", fileId, gameId);
            return false;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public string GetStoragePath(string fileId, string gameId, string fileName)
    {
        // SECURITY: Validate gameId to prevent path traversal
        PathSecurity.ValidateIdentifier(gameId, nameof(gameId));

        var gameDir = PathSecurity.ValidatePathIsInDirectory(_storageBasePath, gameId);
        var sanitizedFileName = SanitizeFileName(fileName);
        return Path.Combine(gameDir, $"{fileId}_{sanitizedFileName}");
    }

    public Task<bool> ExistsAsync(string fileId, string gameId, CancellationToken cancellationToken = default)
    {
        try
        {
            // SECURITY: Validate parameters to prevent path traversal (SEC-738, CWE-22, CWE-73)
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(gameId, nameof(gameId));

            var gameDir = PathSecurity.ValidatePathIsInDirectory(_storageBasePath, gameId);
            if (!Directory.Exists(gameDir))
            {
                return Task.FromResult(false);
            }

            var files = Directory.GetFiles(gameDir, $"{fileId}_*");
            return Task.FromResult(files.Length > 0);
        }
        catch (ArgumentException)
        {
            // Invalid gameId - path traversal attempt
            return Task.FromResult(false);
        }
        catch (System.Security.SecurityException)
        {
            // Path traversal attempt detected
            return Task.FromResult(false);
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        return StringHelper.SanitizeFilename(fileName, maxLength: 200, fallbackName: "file");
    }
}
