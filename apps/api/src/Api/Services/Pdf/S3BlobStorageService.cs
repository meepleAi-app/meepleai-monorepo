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

    public async Task<BlobStorageResult> StoreAsync(Stream stream, string fileName, string gameId, CancellationToken ct = default)
    {
        try
        {
            // SECURITY: Validate gameId to prevent path traversal
            PathSecurity.ValidateIdentifier(gameId, nameof(gameId));

            var fileId = Guid.NewGuid().ToString("N");
            var sanitizedFileName = SanitizeFileName(fileName);
            var s3Key = GetS3Key(fileId, gameId, sanitizedFileName);

            var request = new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = s3Key,
                InputStream = stream,
                ContentType = "application/pdf",
                AutoCloseStream = false // Caller owns the stream
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
            _logger.LogError(ex, "S3 error storing file for game {GameId}: {ErrorCode}", gameId, ex.ErrorCode);
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
            _logger.LogError(ex, "Unexpected error storing file for game {GameId}", gameId);
            return new BlobStorageResult(false, null, null, 0, ex.Message);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public async Task<Stream?> RetrieveAsync(string fileId, string gameId, CancellationToken ct = default)
    {
        try
        {
            // SECURITY: Validate parameters to prevent path traversal (SEC-738, CWE-22, CWE-73)
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(gameId, nameof(gameId));

            // S3 key pattern: pdf_uploads/{gameId}/{fileId}_{filename}
            // We need to list objects with prefix to find the exact key (since filename may vary)
            var prefix = $"pdf_uploads/{gameId}/{fileId}_";

            var listRequest = new ListObjectsV2Request
            {
                BucketName = _options.BucketName,
                Prefix = prefix,
                MaxKeys = 1
            };

            var listResponse = await _s3Client.ListObjectsV2Async(listRequest, ct).ConfigureAwait(false);

            if (listResponse.S3Objects.Count == 0)
            {
                _logger.LogWarning("File not found in S3 for {FileId} in game {GameId}", fileId, gameId);
                return null;
            }

            var s3Key = listResponse.S3Objects[0].Key;

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
            _logger.LogWarning(ex, "File not found in S3 for {FileId} in game {GameId}", fileId, gameId);
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
            _logger.LogError(ex, "Error retrieving file {FileId} for game {GameId}", fileId, gameId);
            return null;
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

            // Find the exact S3 key (since filename may vary)
            var prefix = $"pdf_uploads/{gameId}/{fileId}_";

            var listRequest = new ListObjectsV2Request
            {
                BucketName = _options.BucketName,
                Prefix = prefix,
                MaxKeys = 10 // Allow multiple files with same ID (edge case)
            };

            var listResponse = await _s3Client.ListObjectsV2Async(listRequest, ct).ConfigureAwait(false);

            if (listResponse.S3Objects.Count == 0)
            {
                _logger.LogWarning("File not found for deletion: {FileId} in game {GameId}", fileId, gameId);
                return false;
            }

            // Delete all matching files
            foreach (var s3Object in listResponse.S3Objects)
            {
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = _options.BucketName,
                    Key = s3Object.Key
                };

                await _s3Client.DeleteObjectAsync(deleteRequest, ct).ConfigureAwait(false);
                _logger.LogInformation("Deleted file from S3: {Key}", s3Object.Key);
            }

            return true;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "S3 error deleting file {FileId} for game {GameId}: {ErrorCode}", fileId, gameId, ex.ErrorCode);
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
            _logger.LogWarning(ex, "Unexpected error deleting file {FileId} for game {GameId}", fileId, gameId);
            return false;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    public string GetStoragePath(string fileId, string gameId, string fileName)
    {
        // SECURITY: Validate gameId to prevent path traversal
        PathSecurity.ValidateIdentifier(gameId, nameof(gameId));

        var sanitizedFileName = SanitizeFileName(fileName);
        return GetS3Key(fileId, gameId, sanitizedFileName);
    }

    public async Task<bool> ExistsAsync(string fileId, string gameId, CancellationToken cancellationToken = default)
    {
        try
        {
            // SECURITY: Validate parameters to prevent path traversal (SEC-738, CWE-22, CWE-73)
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(gameId, nameof(gameId));

            var prefix = $"pdf_uploads/{gameId}/{fileId}_";

            var listRequest = new ListObjectsV2Request
            {
                BucketName = _options.BucketName,
                Prefix = prefix,
                MaxKeys = 1
            };

            // Proper async with cancellation support
            var listResponse = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken)
                .ConfigureAwait(false);

            return listResponse.S3Objects.Count > 0;
        }
        catch (ArgumentException)
        {
            // Invalid gameId - path traversal attempt
            return false;
        }
        catch (System.Security.SecurityException)
        {
            // Path traversal attempt detected
            return false;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "S3 error checking file existence {FileId} in game {GameId}: {ErrorCode}", fileId, gameId, ex.ErrorCode);
            return false;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error checking file existence {FileId} in game {GameId}", fileId, gameId);
            return false;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    private static string GetS3Key(string fileId, string gameId, string sanitizedFileName)
    {
        return $"pdf_uploads/{gameId}/{fileId}_{sanitizedFileName}";
    }

    /// <summary>
    /// Generates a pre-signed URL for secure, temporary file downloads
    /// </summary>
    /// <param name="fileId">File ID to generate URL for</param>
    /// <param name="gameId">Game ID for organization</param>
    /// <param name="expirySeconds">URL expiration time (optional, defaults to configured value)</param>
    /// <returns>Pre-signed download URL or null if file not found</returns>
    public async Task<string?> GetPresignedDownloadUrlAsync(string fileId, string gameId, int? expirySeconds = null)
    {
        try
        {
            // SECURITY: Validate parameters to prevent path traversal
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(gameId, nameof(gameId));

            // Find the exact S3 key
            var prefix = $"pdf_uploads/{gameId}/{fileId}_";

            var listRequest = new ListObjectsV2Request
            {
                BucketName = _options.BucketName,
                Prefix = prefix,
                MaxKeys = 1
            };

            var listResponse = await _s3Client.ListObjectsV2Async(listRequest).ConfigureAwait(false);

            if (listResponse.S3Objects.Count == 0)
            {
                _logger.LogWarning("Cannot generate pre-signed URL: file not found {FileId} in game {GameId}", fileId, gameId);
                return null;
            }

            var s3Key = listResponse.S3Objects[0].Key;
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
            _logger.LogError(ex, "S3 error generating pre-signed URL for {FileId} in game {GameId}: {ErrorCode}", fileId, gameId, ex.ErrorCode);
            return null;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating pre-signed URL for {FileId} in game {GameId}", fileId, gameId);
            return null;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    private static string SanitizeFileName(string fileName)
    {
        return StringHelper.SanitizeFilename(fileName, maxLength: 200, fallbackName: "file");
    }
}
