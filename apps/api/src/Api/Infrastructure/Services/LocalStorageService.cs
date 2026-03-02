using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Services;

/// <summary>
/// Local filesystem implementation of storage service.
/// Issue #2732: Storage service for document operations.
/// NOTE: This is a basic implementation for testing. Production should use cloud storage (S3, Azure Blob, etc.).
/// </summary>
internal sealed class LocalStorageService : IStorageService
{
    private readonly ILogger<LocalStorageService> _logger;
    private readonly string _baseStoragePath;
    private readonly string _baseUrl;

    public LocalStorageService(
        ILogger<LocalStorageService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _baseStoragePath = configuration["Storage:BasePath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "storage");
#pragma warning disable S1075 // URIs should not be hardcoded - Default fallback for local development
        _baseUrl = configuration["Storage:BaseUrl"]
            ?? "http://localhost:8080/storage";
#pragma warning restore S1075

        // Ensure base directory exists
        Directory.CreateDirectory(_baseStoragePath);
    }

    public async Task<string> CopyFile(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be empty", nameof(sourcePath));

        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path cannot be empty", nameof(destinationPath));

        // Convert relative paths to absolute
        var sourceFullPath = Path.IsPathRooted(sourcePath)
            ? sourcePath
            : Path.Combine(_baseStoragePath, sourcePath);

        var destFullPath = Path.IsPathRooted(destinationPath)
            ? destinationPath
            : Path.Combine(_baseStoragePath, destinationPath);

        if (!File.Exists(sourceFullPath))
            throw new FileNotFoundException($"Source file not found: {sourceFullPath}", sourceFullPath);

        // Ensure destination directory exists
        var destDirectory = Path.GetDirectoryName(destFullPath);
        if (!string.IsNullOrEmpty(destDirectory))
        {
            Directory.CreateDirectory(destDirectory);
        }

        // Copy file (async)
#pragma warning disable MA0004 // ConfigureAwait not supported with await using
        await using var sourceStream = new FileStream(sourceFullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        await using var destStream = new FileStream(destFullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
#pragma warning restore MA0004
        await sourceStream.CopyToAsync(destStream, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Copied file from {Source} to {Destination}",
            sourcePath, destinationPath);

        // Return relative path for storage
        return Path.GetRelativePath(_baseStoragePath, destFullPath);
    }

    public Task<string> GetPreviewUrl(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("Storage path cannot be empty", nameof(storagePath));

        // Generate time-limited URL (1 hour expiration)
        var expiration = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var signature = GenerateSignature(storagePath, expiration);

        var url = $"{_baseUrl}/{storagePath}?expires={expiration}&signature={signature}";

        _logger.LogDebug("Generated preview URL for {Path}", storagePath);

        return Task.FromResult(url);
    }

    public Task<string> GetDownloadUrl(
        string storagePath,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("Storage path cannot be empty", nameof(storagePath));

        // Generate time-limited download URL
        var expirationUnix = DateTimeOffset.UtcNow.Add(expiration).ToUnixTimeSeconds();
        var signature = GenerateSignature(storagePath, expirationUnix);

        var url = $"{_baseUrl}/{storagePath}?download=true&expires={expirationUnix}&signature={signature}";

        _logger.LogDebug(
            "Generated download URL for {Path} (expires in {Expiration})",
            storagePath, expiration);

        return Task.FromResult(url);
    }

    public Task DeleteFile(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("Storage path cannot be empty", nameof(storagePath));

        var fullPath = Path.IsPathRooted(storagePath)
            ? storagePath
            : Path.Combine(_baseStoragePath, storagePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Deleted file {Path}", storagePath);
        }
        else
        {
            _logger.LogWarning("File not found for deletion: {Path}", storagePath);
        }

        return Task.CompletedTask;
    }

    public Task<long> GetFileSize(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("Storage path cannot be empty", nameof(storagePath));

        var fullPath = Path.IsPathRooted(storagePath)
            ? storagePath
            : Path.Combine(_baseStoragePath, storagePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found: {fullPath}", fullPath);

        var fileInfo = new FileInfo(fullPath);
        return Task.FromResult(fileInfo.Length);
    }

    /// <summary>
    /// Generates an HMAC-SHA256 signature for URL validation.
    /// SEC-04: Uses cryptographic keyed hash instead of non-cryptographic GetHashCode.
    /// </summary>
    private static string GenerateSignature(string path, long expiration)
    {
        // SEC-04: Use HMAC-SHA256 with a fixed key derived from the storage path base.
        // In production with S3, pre-signed URLs are handled by the cloud provider.
        // This key should ideally come from configuration; using a deterministic
        // derivation from a fixed secret for local dev storage.
        var keyBytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("MeepleAI-LocalStorage-SigningKey"));
        var input = System.Text.Encoding.UTF8.GetBytes($"{path}:{expiration}");

        var hash = System.Security.Cryptography.HMACSHA256.HashData(keyBytes, input);
        return Convert.ToBase64String(hash)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
