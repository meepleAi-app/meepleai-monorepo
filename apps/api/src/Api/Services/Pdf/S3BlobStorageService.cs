using Amazon.S3;
using Amazon.S3.Model;
using Api.Helpers;
using Api.Infrastructure.Security;

namespace Api.Services.Pdf;

/// <summary>
/// S3-compatible object storage service for managing blob storage operations
/// Supports multiple S3-compatible providers: Cloudflare R2, AWS S3, Backblaze B2, MinIO, DigitalOcean Spaces
/// </summary>
internal sealed class S3BlobStorageService : IBlobStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3StorageOptions _options;
    private readonly ILogger<S3BlobStorageService> _logger;

    public S3BlobStorageService(
        IAmazonS3 s3Client,
        S3StorageOptions options,
        ILogger<S3BlobStorageService> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Exposes the S3 client for health check connectivity verification.
    /// </summary>
    internal IAmazonS3 S3Client => _s3Client;

    /// <summary>
    /// Exposes the storage options for health check reporting.
    /// </summary>
    internal S3StorageOptions Options => _options;

    public async Task<BlobStorageResult> StoreAsync(Stream stream, string fileName, BlobCategory category, string resourceKey, CancellationToken ct = default)
    {
        try
        {
            // SECURITY: Validate resourceKey to prevent path traversal
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

            var fileId = Guid.NewGuid().ToString("N");
            var sanitizedFileName = SanitizeFileName(fileName);
            var s3Key = GetS3Key(fileId, category, resourceKey, sanitizedFileName);

            var request = new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = s3Key,
                InputStream = stream,
                ContentType = "application/pdf",
                AutoCloseStream = false, // Caller owns the stream
                DisablePayloadSigning = true // Required for S3-compatible providers (MinIO, R2) that don't support STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER
            };

            // Server-side encryption if enabled
            if (_options.EnableEncryption)
            {
                request.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256;
            }

            var response = await _s3Client.PutObjectAsync(request, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Stored file to S3: {Key} (size: {Size} bytes, ETag: {ETag})",
                s3Key, stream.Length, response.ETag);

            return new BlobStorageResult(true, fileId, s3Key, stream.Length);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error storing file in {Category}/{ResourceKey}: {ErrorCode}", category, resourceKey, ex.ErrorCode);
            return new BlobStorageResult(false, null, null, 0, $"S3 error: {ex.Message}");
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // SERVICE BOUNDARY PATTERN: S3 storage service boundary - must handle all errors gracefully
            // Rationale: This is a service entry point that interacts with external S3 storage. Network and
            // S3 operations can throw various runtime exceptions (timeouts, network errors, authentication failures).
            // We must catch all exceptions to return error results instead of crashing the service.
            // Context: S3 operations can fail in unpredictable ways across different network conditions
            _logger.LogError(ex, "Unexpected error storing file in {Category}/{ResourceKey}", category, resourceKey);
            return new BlobStorageResult(false, null, null, 0, ex.Message);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public async Task<Stream?> RetrieveAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken ct = default)
    {
        try
        {
            // SECURITY: Validate parameters to prevent path traversal (SEC-738, CWE-22, CWE-73)
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

            var s3Key = await TryResolveKeyAsync(fileId, category, resourceKey, ct).ConfigureAwait(false);
            if (s3Key is null)
            {
                _logger.LogWarning("File not found in S3 for {FileId} in {Category}/{ResourceKey}", fileId, category, resourceKey);
                return null;
            }

            var getRequest = new GetObjectRequest
            {
                BucketName = _options.BucketName,
                Key = s3Key
            };

            var response = await _s3Client.GetObjectAsync(getRequest, ct).ConfigureAwait(false);

            _logger.LogInformation("Retrieved file from S3: {Key}", s3Key);

            // Return the response stream (caller must dispose)
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning(ex, "File not found in S3 for {FileId} in {Category}/{ResourceKey}", fileId, category, resourceKey);
            return null;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // SERVICE BOUNDARY PATTERN: S3 storage service boundary - must handle all errors gracefully
            // Rationale: This is a service entry point that retrieves files from S3 storage. Network and
            // S3 operations can throw various runtime exceptions (timeouts, network errors, authentication failures).
            // We must catch all exceptions to return null instead of crashing the service.
            // Context: S3 operations can fail in unpredictable ways across different network conditions
            _logger.LogError(ex, "Error retrieving file {FileId} for {Category}/{ResourceKey}", fileId, category, resourceKey);
            return null;
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

            // List up to 10 matching objects to handle the defensive edge case of a
            // duplicate fileId (should not happen but we cleanup whatever is there).
            var prefix = $"{category.ToS3Folder()}/{resourceKey}/{fileId}_";
            var listResponse = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _options.BucketName,
                Prefix = prefix,
                MaxKeys = 10,
            }, ct).ConfigureAwait(false);

            if (listResponse.S3Objects.Count == 0)
            {
                _logger.LogWarning("File not found for deletion: {FileId} in {Category}/{ResourceKey}", fileId, category, resourceKey);
                return false;
            }

            foreach (var s3Object in listResponse.S3Objects)
            {
                await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _options.BucketName,
                    Key = s3Object.Key,
                }, ct).ConfigureAwait(false);
                _logger.LogInformation("Deleted file from S3: {Key}", s3Object.Key);
            }

            return true;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "S3 error deleting file {FileId} for {Category}/{ResourceKey}: {ErrorCode}", fileId, category, resourceKey, ex.ErrorCode);
            return false;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // SERVICE BOUNDARY PATTERN: S3 storage service boundary - must handle all errors gracefully
            // Rationale: This is a service entry point that deletes files from S3 storage. Network and
            // S3 operations can throw various runtime exceptions (timeouts, network errors, authentication failures).
            // We must catch all exceptions to return false instead of crashing the service.
            // Context: S3 operations can fail in unpredictable ways across different network conditions
            _logger.LogWarning(ex, "Unexpected error deleting file {FileId} for {Category}/{ResourceKey}", fileId, category, resourceKey);
            return false;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public string GetStoragePath(string fileId, BlobCategory category, string resourceKey, string fileName)
    {
        // SECURITY: Validate resourceKey to prevent path traversal
        PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

        var sanitizedFileName = SanitizeFileName(fileName);
        return GetS3Key(fileId, category, resourceKey, sanitizedFileName);
    }

    public async Task<bool> ExistsAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken cancellationToken = default)
    {
        try
        {
            // SECURITY: Validate parameters to prevent path traversal (SEC-738, CWE-22, CWE-73)
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

            var resolved = await TryResolveKeyAsync(fileId, category, resourceKey, cancellationToken).ConfigureAwait(false);
            return resolved is not null;
        }
        catch (ArgumentException)
        {
            // Invalid resourceKey - path traversal attempt
            return false;
        }
        catch (System.Security.SecurityException)
        {
            // Path traversal attempt detected
            return false;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "S3 error checking file existence {FileId} in {Category}/{ResourceKey}: {ErrorCode}", fileId, category, resourceKey, ex.ErrorCode);
            return false;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error checking file existence {FileId} in {Category}/{ResourceKey}", fileId, category, resourceKey);
            return false;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    /// <summary>
    /// Constructs the canonical S3 key for a blob using the categorized "v2"
    /// layout: <c>{category.ToS3Folder()}/{resourceKey}/{fileId}_{filename}</c>.
    /// Issue #1399: legacy <c>pdf_uploads/</c> prefix removed after Phase 4
    /// migration drainer flushed all rows.
    /// </summary>
    private static string GetS3Key(string fileId, BlobCategory category, string resourceKey, string sanitizedFileName)
        => $"{category.ToS3Folder()}/{resourceKey}/{fileId}_{sanitizedFileName}";

    /// <summary>
    /// Resolves the full S3 key for the (fileId, category, resourceKey) tuple by
    /// listing the categorized prefix and returning the first match (we expect
    /// a single hit; ListObjectsV2 with MaxKeys=1 keeps the round-trip cheap).
    /// </summary>
    private async Task<string?> TryResolveKeyAsync(
        string fileId,
        BlobCategory category,
        string resourceKey,
        CancellationToken ct)
    {
        var prefix = $"{category.ToS3Folder()}/{resourceKey}/{fileId}_";
        var listResponse = await _s3Client
            .ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _options.BucketName,
                Prefix = prefix,
                MaxKeys = 1,
            }, ct)
            .ConfigureAwait(false);
        return listResponse.S3Objects.Count > 0 ? listResponse.S3Objects[0].Key : null;
    }

    /// <summary>
    /// Generates a pre-signed URL for secure, temporary file downloads.
    /// </summary>
    /// <param name="fileId">File ID to generate URL for.</param>
    /// <param name="category">Blob category for folder organization.</param>
    /// <param name="resourceKey">Opaque resource identifier used as folder key.</param>
    /// <param name="expirySeconds">URL expiration time (optional, defaults to configured value).</param>
    /// <returns>Pre-signed download URL or null if file not found.</returns>
    public async Task<string?> GetPresignedDownloadUrlAsync(string fileId, BlobCategory category, string resourceKey, int? expirySeconds = null)
    {
        try
        {
            // SECURITY: Validate parameters to prevent path traversal
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

            var s3Key = await TryResolveKeyAsync(fileId, category, resourceKey, CancellationToken.None).ConfigureAwait(false);
            if (s3Key is null)
            {
                _logger.LogWarning("Cannot generate pre-signed URL: file not found {FileId} in {Category}/{ResourceKey}", fileId, category, resourceKey);
                return null;
            }

            var expiry = expirySeconds ?? _options.PresignedUrlExpirySeconds;

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _options.BucketName,
                Key = s3Key,
                Expires = DateTime.UtcNow.AddSeconds(expiry),
                Verb = HttpVerb.GET
            };

            var url = await _s3Client.GetPreSignedURLAsync(request).ConfigureAwait(false);

            _logger.LogInformation(
                "Generated pre-signed URL for {Key} (expires in {Expiry}s)",
                s3Key, expiry);

            return url;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error generating pre-signed URL for {FileId} in {Category}/{ResourceKey}: {ErrorCode}", fileId, category, resourceKey, ex.ErrorCode);
            return null;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating pre-signed URL for {FileId} in {Category}/{ResourceKey}", fileId, category, resourceKey);
            return null;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    private static string SanitizeFileName(string fileName)
    {
        return StringHelper.SanitizeFilename(fileName, maxLength: 200, fallbackName: "file");
    }
}
