namespace Api.Services.Pdf;

/// <summary>
/// Generic file storage service for managing blob storage operations
/// Reusable for any file type beyond PDFs
/// </summary>
public class BlobStorageService : IBlobStorageService
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
            var fileId = Guid.NewGuid().ToString("N");
            var gameDir = Path.Combine(_storageBasePath, gameId);
            Directory.CreateDirectory(gameDir);

            var sanitizedFileName = SanitizeFileName(fileName);
            var filePath = Path.Combine(gameDir, $"{fileId}_{sanitizedFileName}");

            // Save file to disk
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await stream.CopyToAsync(fileStream, ct);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error storing file for game {GameId}", gameId);
            return new BlobStorageResult(false, null, null, 0, ex.Message);
        }
    }

    public async Task<Stream?> RetrieveAsync(string fileId, string gameId, CancellationToken ct = default)
    {
        try
        {
            var gameDir = Path.Combine(_storageBasePath, gameId);
            var files = Directory.GetFiles(gameDir, $"{fileId}_*");

            if (files.Length == 0)
            {
                _logger.LogWarning("File not found for {FileId} in game {GameId}", fileId, gameId);
                return null;
            }

            var filePath = files[0];
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await Task.FromResult<Stream>(fileStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file {FileId} for game {GameId}", fileId, gameId);
            return null;
        }
    }

    public async Task<bool> DeleteAsync(string fileId, string gameId, CancellationToken ct = default)
    {
        try
        {
            var gameDir = Path.Combine(_storageBasePath, gameId);
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

            return await Task.FromResult(true);
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error deleting file {FileId} for game {GameId}", fileId, gameId);
            return false;
        }
    }

    public string GetStoragePath(string fileId, string gameId, string fileName)
    {
        var gameDir = Path.Combine(_storageBasePath, gameId);
        var sanitizedFileName = SanitizeFileName(fileName);
        return Path.Combine(gameDir, $"{fileId}_{sanitizedFileName}");
    }

    public bool Exists(string fileId, string gameId)
    {
        var gameDir = Path.Combine(_storageBasePath, gameId);
        if (!Directory.Exists(gameDir))
        {
            return false;
        }

        var files = Directory.GetFiles(gameDir, $"{fileId}_*");
        return files.Length > 0;
    }

    private static string SanitizeFileName(string fileName)
    {
        // Get OS-specific invalid chars and add additional problematic chars
        var invalidChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { '<', '>', '?', '*', '|', '"', ':' })
            .Distinct()
            .ToArray();

        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Length > 200 ? sanitized.Substring(0, 200) : sanitized;
    }
}
