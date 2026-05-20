using Api.Helpers;
using Api.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Api.Services.Pdf;

/// <summary>
/// Generic file storage service for managing blob storage operations
/// Reusable for any file type beyond PDFs
/// </summary>
internal class BlobStorageService : IBlobStorageService
{
    private readonly string _storageBasePath;
    private readonly StorageLayoutOptions _layoutOptions;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(
        IConfiguration config,
        IOptions<StorageLayoutOptions> layoutOptions,
        ILogger<BlobStorageService> logger)
    {
        ArgumentNullException.ThrowIfNull(layoutOptions);
        _layoutOptions = layoutOptions.Value;
        _logger = logger;
        _storageBasePath = config["PDF_STORAGE_PATH"] ?? Path.Combine(Directory.GetCurrentDirectory(), "pdf_uploads");

        // Ensure storage directory exists
        if (!Directory.Exists(_storageBasePath))
        {
            Directory.CreateDirectory(_storageBasePath);
            _logger.LogInformation("Created blob storage directory at {Path}", _storageBasePath);
        }
    }

    /// <summary>
    /// Resolves the on-disk subdirectory under which a blob lives, based on
    /// <see cref="StorageLayoutOptions.WriteMode"/> (for writes) or each
    /// <see cref="StorageReadMode"/> candidate (for reads).
    /// Legacy layout uses <c>{_storageBasePath}/{resourceKey}/</c>; the new
    /// categorized layout uses <c>{_storageBasePath}/{category.ToS3Folder()}/{resourceKey}/</c>.
    /// </summary>
    private string ResolveResourceDir(BlobCategory category, string resourceKey, bool forNewLayout)
    {
        if (forNewLayout)
        {
            var categoryFolder = category.ToS3Folder();
            var categoryDir = PathSecurity.ValidatePathIsInDirectory(_storageBasePath, categoryFolder);
            return PathSecurity.ValidatePathIsInDirectory(categoryDir, resourceKey);
        }

        return PathSecurity.ValidatePathIsInDirectory(_storageBasePath, resourceKey);
    }

    private IEnumerable<string> EnumerateReadDirs(BlobCategory category, string resourceKey)
    {
        switch (_layoutOptions.ReadMode)
        {
            case StorageReadMode.New:
                yield return ResolveResourceDir(category, resourceKey, forNewLayout: true);
                break;
            case StorageReadMode.Legacy:
                yield return ResolveResourceDir(category, resourceKey, forNewLayout: false);
                break;
            case StorageReadMode.Dual:
                yield return ResolveResourceDir(category, resourceKey, forNewLayout: true);
                yield return ResolveResourceDir(category, resourceKey, forNewLayout: false);
                break;
            default:
                yield return ResolveResourceDir(category, resourceKey, forNewLayout: false);
                break;
        }
    }

    public async Task<BlobStorageResult> StoreAsync(Stream stream, string fileName, BlobCategory category, string resourceKey, CancellationToken ct = default)
    {
        try
        {
            // SECURITY: Validate resourceKey to prevent path traversal
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

            var fileId = Guid.NewGuid().ToString("N");
            // PR 2: WriteMode.New routes to the categorized subdirectory; Legacy
            // (and Dual which writes the legacy copy first) routes to the
            // resourceKey-only subdirectory under _storageBasePath.
            var forNewLayout = _layoutOptions.WriteMode == StorageWriteMode.New;
            var resourceDir = ResolveResourceDir(category, resourceKey, forNewLayout);
            Directory.CreateDirectory(resourceDir);

            var sanitizedFileName = SanitizeFileName(fileName);
            var filePath = Path.Combine(resourceDir, $"{fileId}_{sanitizedFileName}");

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
            _logger.LogError(ex, "I/O error storing file in {ResourceKey}", resourceKey);
            return new BlobStorageResult(false, null, null, 0, $"I/O error: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied storing file in {ResourceKey}", resourceKey);
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
            _logger.LogError(ex, "Unexpected error storing file in {ResourceKey}", resourceKey);
            return new BlobStorageResult(false, null, null, 0, ex.Message);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public Task<Stream?> RetrieveAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken ct = default)
    {
        FileStream? fileStream = null;
        try
        {
            // SECURITY: Validate parameters to prevent path traversal (SEC-738, CWE-22, CWE-73)
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

            // PR 2: layout-aware retrieval — probe each candidate directory in
            // ReadMode order. First non-empty match wins.
            foreach (var resourceDir in EnumerateReadDirs(category, resourceKey))
            {
                if (!Directory.Exists(resourceDir))
                {
                    continue;
                }

                var files = Directory.GetFiles(resourceDir, $"{fileId}_*");
                if (files.Length == 0)
                {
                    continue;
                }

                fileStream = new FileStream(files[0], FileMode.Open, FileAccess.Read, FileShare.Read);
                return Task.FromResult<Stream?>(fileStream);
            }

            _logger.LogWarning("File not found for {FileId} in {ResourceKey}", fileId, resourceKey);
            return Task.FromResult<Stream?>(null);
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

            _logger.LogError(ex, "Error retrieving file {FileId} for {ResourceKey}", fileId, resourceKey);
            return Task.FromResult<Stream?>(null);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public async Task<bool> DeleteAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken ct = default)
    {
        try
        {
            // SECURITY: Validate parameters to prevent path traversal (SEC-738, CWE-22, CWE-73)
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

            // PR 2: layout-aware deletion — sweep every candidate directory in
            // ReadMode order so a mid-migration delete cleans up both copies.
            var deletedAny = false;
            foreach (var resourceDir in EnumerateReadDirs(category, resourceKey))
            {
                if (!Directory.Exists(resourceDir))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(resourceDir, $"{fileId}_*"))
                {
                    File.Delete(file);
                    _logger.LogInformation("Deleted file at {FilePath}", file);
                    deletedAny = true;
                }
            }

            if (!deletedAny)
            {
                _logger.LogWarning("File not found for deletion: {FileId} in {ResourceKey}", fileId, resourceKey);
                return false;
            }

            return await Task.FromResult(true).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "I/O error deleting file {FileId} for {ResourceKey}", fileId, resourceKey);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied deleting file {FileId} for {ResourceKey}", fileId, resourceKey);
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
            _logger.LogWarning(ex, "Unexpected error deleting file {FileId} for {ResourceKey}", fileId, resourceKey);
            return false;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public string GetStoragePath(string fileId, BlobCategory category, string resourceKey, string fileName)
    {
        // SECURITY: Validate resourceKey to prevent path traversal
        PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

        // PR 2: GetStoragePath returns the path under the active WriteMode layout
        // (the path callers would observe when storing a new blob).
        var forNewLayout = _layoutOptions.WriteMode == StorageWriteMode.New;
        var resourceDir = ResolveResourceDir(category, resourceKey, forNewLayout);
        var sanitizedFileName = SanitizeFileName(fileName);
        return Path.Combine(resourceDir, $"{fileId}_{sanitizedFileName}");
    }

    public Task<bool> ExistsAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken cancellationToken = default)
    {
        try
        {
            // SECURITY: Validate parameters to prevent path traversal (SEC-738, CWE-22, CWE-73)
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

            // PR 2: layout-aware existence check — return true if ANY candidate
            // directory contains a file matching the fileId prefix.
            foreach (var resourceDir in EnumerateReadDirs(category, resourceKey))
            {
                if (!Directory.Exists(resourceDir))
                {
                    continue;
                }

                if (Directory.GetFiles(resourceDir, $"{fileId}_*").Length > 0)
                {
                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(false);
        }
        catch (ArgumentException)
        {
            // Invalid resourceKey - path traversal attempt
            return Task.FromResult(false);
        }
        catch (System.Security.SecurityException)
        {
            // Path traversal attempt detected
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Local storage does not support pre-signed URLs.
    /// Consumers should fall back to the API download endpoint when this returns null.
    /// </summary>
    public Task<string?> GetPresignedDownloadUrlAsync(string fileId, BlobCategory category, string resourceKey, int? expirySeconds = null)
    {
        _ = (fileId, category, resourceKey, expirySeconds); // Local storage: no presigned URL
        return Task.FromResult<string?>(null);
    }

    private static string SanitizeFileName(string fileName)
    {
        return StringHelper.SanitizeFilename(fileName, maxLength: 200, fallbackName: "file");
    }
}
