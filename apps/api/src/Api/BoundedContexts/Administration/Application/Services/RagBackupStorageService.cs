using Amazon.S3;
using Amazon.S3.Model;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Dual-write backup storage: always writes to local filesystem and optionally to S3.
/// S3 failures are non-blocking — a warning is logged and the local copy is preserved.
/// </summary>
internal sealed class RagBackupStorageService : IRagBackupStorageService
{
    private readonly IAmazonS3? _s3Client;
    private readonly string _localBasePath;
    private readonly string? _s3BucketName;
    private readonly ILogger<RagBackupStorageService> _logger;

    public RagBackupStorageService(
        IConfiguration configuration,
        ILogger<RagBackupStorageService> logger,
        IAmazonS3? s3Client = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _s3Client = s3Client;

        _localBasePath = configuration["BACKUP_LOCAL_PATH"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "data", "rag-exports");

        _s3BucketName = configuration["S3_BACKUP_BUCKET_NAME"];
    }

    /// <inheritdoc />
    public async Task WriteFileAsync(string relativePath, byte[] content, CancellationToken ct = default)
    {
        using var ms = new MemoryStream(content);
        await WriteFileAsync(relativePath, ms, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteFileAsync(string relativePath, Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(content);

        // --- 1. Local write (always) ---
        var localPath = GetLocalPath(relativePath);
        var directory = Path.GetDirectoryName(localPath)!;

        Directory.CreateDirectory(directory);

        // Buffer stream to support both local and S3 write without double-consuming
        byte[] buffer;
        if (content is MemoryStream existingMs)
        {
            buffer = existingMs.ToArray();
        }
        else
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct).ConfigureAwait(false);
            buffer = ms.ToArray();
        }

        await File.WriteAllBytesAsync(localPath, buffer, ct).ConfigureAwait(false);
        _logger.LogDebug("RAG backup written locally: {Path}", localPath);

        // --- 2. S3 write (optional, non-blocking) ---
        if (_s3Client is not null && !string.IsNullOrEmpty(_s3BucketName))
        {
            await TryWriteToS3Async(relativePath, buffer, ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<byte[]?> ReadFileAsync(string relativePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        var localPath = GetLocalPath(relativePath);

        if (!File.Exists(localPath))
        {
            _logger.LogDebug("RAG backup file not found locally: {Path}", localPath);
            return null;
        }

        return await File.ReadAllBytesAsync(localPath, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<List<string>> ListSnapshotsAsync(CancellationToken ct = default)
    {
        // _localBasePath is already the rag-exports root; snapshots live directly under it
        var root = _localBasePath;

        var snapshots = new List<string>();

        if (!Directory.Exists(root))
            return Task.FromResult(snapshots);

        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (File.Exists(manifestPath))
            {
                snapshots.Add(Path.GetFileName(dir));
            }
        }

        return Task.FromResult(snapshots);
    }

    /// <inheritdoc />
    public Task DeleteSnapshotAsync(string snapshotId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotId);

        var snapshotPath = Path.Combine(_localBasePath, snapshotId);

        if (Directory.Exists(snapshotPath))
        {
            Directory.Delete(snapshotPath, recursive: true);
            _logger.LogInformation("RAG backup snapshot deleted: {SnapshotId}", snapshotId);
        }
        else
        {
            _logger.LogWarning("RAG backup snapshot not found for deletion: {SnapshotId}", snapshotId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<string?> GetDownloadUrlAsync(string relativePath, int expirySeconds = 3600, CancellationToken ct = default)
    {
        if (_s3Client is null || string.IsNullOrEmpty(_s3BucketName))
        {
            _logger.LogDebug("S3 not configured — cannot generate download URL for {Path}", relativePath);
            return null;
        }

        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _s3BucketName,
                Key = relativePath,
                Expires = DateTime.UtcNow.AddSeconds(expirySeconds),
                Verb = HttpVerb.GET
            };

            var url = await _s3Client.GetPreSignedURLAsync(request).ConfigureAwait(false);

            _logger.LogInformation(
                "Generated pre-signed URL for RAG backup {Key} (expires in {Expiry}s)", relativePath, expirySeconds);

            return url;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "S3 error generating pre-signed URL for {Key}: {ErrorCode}", relativePath, ex.ErrorCode);
            return null;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // SERVICE BOUNDARY PATTERN: S3 storage service boundary — must handle all errors gracefully
            // Rationale: Pre-signed URL generation can fail due to network issues, auth failures, or SDK errors.
            // Returning null is safe — callers fall back to local file access.
            _logger.LogWarning(ex, "Unexpected error generating pre-signed URL for {Key}", relativePath);
            return null;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private string GetLocalPath(string relativePath)
    {
        // relativePath is already prefixed with "rag-exports/{snapshotId}/..."
        // _localBasePath points to the rag-exports directory, so we strip the leading segment
        const string Prefix = "rag-exports/";
        var path = relativePath.StartsWith(Prefix, StringComparison.Ordinal)
            ? relativePath[Prefix.Length..]
            : relativePath;

        return Path.Combine(_localBasePath, path);
    }

    private async Task TryWriteToS3Async(string key, byte[] content, CancellationToken ct)
    {
        try
        {
            using var ms = new MemoryStream(content);

            var request = new PutObjectRequest
            {
                BucketName = _s3BucketName,
                Key = key,
                InputStream = ms,
                AutoCloseStream = false,
                DisablePayloadSigning = true, // Required for R2 and other S3-compatible providers
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
            };

            await _s3Client!.PutObjectAsync(request, ct).ConfigureAwait(false);

            _logger.LogDebug("RAG backup written to S3: {Key}", key);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex,
                "S3 write failed for RAG backup {Key} (local copy preserved): {ErrorCode}", key, ex.ErrorCode);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // SERVICE BOUNDARY PATTERN: S3 write failures must not block the backup operation.
            // The local copy has already been written successfully — this is a best-effort S3 sync.
            _logger.LogWarning(ex,
                "Unexpected S3 write error for RAG backup {Key} (local copy preserved)", key);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }
}
