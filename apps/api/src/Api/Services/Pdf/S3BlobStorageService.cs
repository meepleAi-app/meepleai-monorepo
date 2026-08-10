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
    private readonly IAmazonS3 _presignClient;
    private readonly S3StorageOptions _options;
    private readonly ILogger<S3BlobStorageService> _logger;

    // Issue #3498: `presignClient` (optional) is configured against S3StorageOptions.PublicEndpoint
    // and used ONLY to sign presigned URLs (SigV4 signs the host). Defaults to `s3Client` when the
    // store host is already browser-reachable (prod R2/AWS). HEAD/existence checks always use `s3Client`.
    public S3BlobStorageService(
        IAmazonS3 s3Client,
        S3StorageOptions options,
        ILogger<S3BlobStorageService> logger,
        IAmazonS3? presignClient = null)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _presignClient = presignClient ?? s3Client;
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
        // Issue #2271: pre-buffer non-seekable streams so PutObject can declare an
        // explicit ContentLength + skip the SDK's MD5 streaming pass. R2 (and other
        // S3-compatible providers) fail with "Could not determine content length"
        // when AmazonS3PostMarshallHandler.SetStreamChecksum() reads Length on a
        // forward-only stream (e.g. an HTTP request body forwarded raw). The buffer
        // is local to this call and disposed in `finally`.
        MemoryStream? buffer = null;
        try
        {
            // SECURITY: Validate resourceKey to prevent path traversal
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

            Stream uploadStream;
            long contentLength;

            if (stream.CanSeek)
            {
                uploadStream = stream;
                contentLength = stream.Length;
            }
            else
            {
                buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, ct).ConfigureAwait(false);
                buffer.Position = 0;
                uploadStream = buffer;
                contentLength = buffer.Length;
            }

            var fileId = Guid.NewGuid().ToString("N");
            var sanitizedFileName = SanitizeFileName(fileName);
            var s3Key = GetS3Key(fileId, category, resourceKey, sanitizedFileName);

            var request = new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = s3Key,
                InputStream = uploadStream,
                ContentType = "application/pdf",
                AutoCloseStream = false, // Caller owns the original stream; we own `buffer` and dispose it below.
                DisablePayloadSigning = true, // Required for S3-compatible providers (MinIO, R2) that don't support STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER
                DisableDefaultChecksumValidation = true, // Issue #2271: ContentLength is explicit; skip the checksum re-read of the stream.
            };
            request.Headers.ContentLength = contentLength; // Issue #2271: explicit header required by R2 strict validation.

            // Server-side encryption if enabled
            if (_options.EnableEncryption)
            {
                request.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256;
            }

            var response = await _s3Client.PutObjectAsync(request, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Stored file to S3: {Key} (size: {Size} bytes, ETag: {ETag})",
                s3Key, contentLength, response.ETag);

            return new BlobStorageResult(true, fileId, s3Key, contentLength);
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
        finally
        {
            if (buffer is not null)
            {
                await buffer.DisposeAsync().ConfigureAwait(false);
            }
        }
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
    /// Issue #3498 — realigns a presigned URL's scheme with that of the configured presign endpoint.
    ///
    /// <para>
    /// The AWS SDK always emits <c>https://</c> for a presigned URL, even when the client is built
    /// against a cleartext <c>ServiceURL</c> and <c>UseHttp</c> is set. Verified against AWSSDK.S3
    /// 3.7.413: every config permutation (with/without <c>UseHttp</c>, <c>AuthenticationRegion</c>,
    /// <c>ForcePathStyle</c>, trailing slash) still signed <c>https</c>. Pointed at a plain-HTTP
    /// MinIO the browser then opens a TLS handshake against a cleartext server, the image never
    /// loads, and the card falls back to its emoji placeholder — a symptom that reads as a missing
    /// object rather than a scheme mismatch.
    /// </para>
    /// <para>
    /// Rewriting the scheme keeps the signature valid: SigV4 signs host, path and query, never the
    /// protocol. Verified end-to-end against a real MinIO — the rewritten URL returns 200 where the
    /// as-signed one dies on the TLS handshake.
    /// </para>
    /// <para>
    /// The downgrade happens ONLY when the operator explicitly configured a cleartext endpoint
    /// (MinIO in dev/E2E). Production endpoints (R2/AWS) are https, so the URL is returned untouched.
    /// </para>
    /// </summary>
    private string AlignSchemeWithPresignEndpoint(string url)
    {
        // The presign client is built against PublicEndpoint when set, and falls back to Endpoint
        // otherwise (see BlobStorageServiceFactory) — the scheme must follow the same source.
        var presignEndpoint = string.IsNullOrWhiteSpace(_options.PublicEndpoint)
            ? _options.Endpoint
            : _options.PublicEndpoint;

        if (!BlobStorageServiceFactory.UsesPlainHttp(presignEndpoint) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var signed) ||
            !string.Equals(signed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var builder = new UriBuilder(signed) { Scheme = Uri.UriSchemeHttp };

        // An explicit port (MinIO's :9000) is part of the signed Host header and must survive; an
        // IMPLICIT one must not become literal. UriBuilder materialises the original scheme's default
        // port (443), which is not http's default and would otherwise be emitted as ":443",
        // changing the Host header and invalidating the signature. Test on the ORIGINAL Uri: 443 is
        // only recognisable as a default while the scheme is still https.
        if (signed.IsDefaultPort)
        {
            builder.Port = -1;
        }

        return builder.Uri.ToString();
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

            var url = AlignSchemeWithPresignEndpoint(
                await _presignClient.GetPreSignedURLAsync(request).ConfigureAwait(false));

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

    /// <summary>
    /// Generates a pre-signed GET URL for an EXACT physical S3 object key.
    /// Deliberately skips <c>PathSecurity.ValidateIdentifier</c> and prefix
    /// discovery — the caller (business logic that composed the key) is
    /// trusted. An existence check (HEAD via <c>GetObjectMetadataAsync</c>) is
    /// performed FIRST: this is load-bearing, because returning a presigned
    /// URL for a non-existent object would render as a broken image on the
    /// client instead of falling back to a placeholder.
    /// </summary>
    public async Task<string?> GetPresignedUrlForRawKeyAsync(string rawKey, int? expirySeconds = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                return null;
            }

            try
            {
                await _s3Client.GetObjectMetadataAsync(_options.BucketName, rawKey, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (AmazonS3Exception ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                string.Equals(ex.ErrorCode, "NotFound", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(ex, "Cannot generate pre-signed URL: raw key not found in S3: {Key}", rawKey);
                return null;
            }

            var expiry = expirySeconds ?? _options.PresignedUrlExpirySeconds;

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _options.BucketName,
                Key = rawKey,
                Expires = DateTime.UtcNow.AddSeconds(expiry),
                Verb = HttpVerb.GET
            };

            var url = AlignSchemeWithPresignEndpoint(
                await _presignClient.GetPreSignedURLAsync(request).ConfigureAwait(false));

            _logger.LogInformation(
                "Generated pre-signed URL for raw key {Key} (expires in {Expiry}s)",
                rawKey, expiry);

            return url;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error generating pre-signed URL for raw key {Key}: {ErrorCode}", rawKey, ex.ErrorCode);
            return null;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // SERVICE BOUNDARY PATTERN: S3 storage service boundary - must handle all errors gracefully
            // Rationale: This is a service entry point that interacts with external S3 storage. Network and
            // S3 operations can throw various runtime exceptions (timeouts, network errors, authentication failures).
            // We must catch all exceptions to return null instead of crashing the service.
            _logger.LogError(ex, "Unexpected error generating pre-signed URL for raw key {Key}", rawKey);
            return null;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    /// <summary>
    /// Deletes an object at an EXACT physical S3 key. Deliberately skips
    /// <c>PathSecurity.ValidateIdentifier</c> and prefix discovery — the caller
    /// (business logic that composed the key) is trusted. Mirrors
    /// <see cref="GetPresignedUrlForRawKeyAsync"/>'s raw-key contract.
    /// </summary>
    public async Task<bool> DeleteRawKeyAsync(string rawKey, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                return false;
            }

            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _options.BucketName,
                Key = rawKey,
            }, ct).ConfigureAwait(false);

            _logger.LogInformation("Deleted file from S3 (raw key): {Key}", rawKey);
            return true;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "S3 error deleting raw key {Key}: {ErrorCode}", rawKey, ex.ErrorCode);
            return false;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // SERVICE BOUNDARY PATTERN: S3 storage service boundary - must handle all errors gracefully
            // Rationale: This is a service entry point that deletes files from S3 storage. Network and
            // S3 operations can throw various runtime exceptions (timeouts, network errors, authentication failures).
            // We must catch all exceptions to return false instead of crashing the service.
            _logger.LogWarning(ex, "Unexpected error deleting raw key {Key}", rawKey);
            return false;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    /// <summary>
    /// #3384 — writes an object to an EXACT physical key (write-side counterpart of
    /// <see cref="GetPresignedUrlForRawKeyAsync"/>). No <c>ValidateIdentifier</c>, no
    /// category folder, no random file-id segment: the key is written verbatim so the
    /// resolver's raw-key read reconstructs it deterministically. Mirrors the cover
    /// upload pipelines' <c>DisablePayloadSigning</c> requirement for S3-compatible
    /// providers (R2/MinIO) that don't support the streaming payload trailer.
    /// </summary>
    public async Task<bool> StoreRawKeyAsync(string rawKey, Stream stream, string contentType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawKey) || stream is null)
        {
            return false;
        }

        MemoryStream? buffered = null;
        try
        {
            // Pre-buffer non-seekable streams so PutObject can declare a length (#2271).
            Stream uploadStream;
            if (stream.CanSeek)
            {
                uploadStream = stream;
            }
            else
            {
                buffered = new MemoryStream();
                await stream.CopyToAsync(buffered, ct).ConfigureAwait(false);
                buffered.Position = 0;
                uploadStream = buffered;
            }

            var request = new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = rawKey,
                InputStream = uploadStream,
                ContentType = contentType,
                AutoCloseStream = false,
                DisablePayloadSigning = true,
            };

            await _s3Client.PutObjectAsync(request, ct).ConfigureAwait(false);
            _logger.LogInformation("Stored file in S3 (raw key): {Key}", rawKey);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "S3 error storing raw key {Key}: {ErrorCode}", rawKey, ex.ErrorCode);
            return false;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error storing raw key {Key}", rawKey);
            return false;
        }
#pragma warning restore CA1031 // Do not catch general exception types
        finally
        {
            if (buffered is not null)
            {
                await buffered.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Reads an object at an EXACT physical S3 key — the read-side counterpart of
    /// <see cref="StoreRawKeyAsync"/>. Deliberately skips <c>PathSecurity.ValidateIdentifier</c>
    /// and prefix discovery, mirroring <see cref="DeleteRawKeyAsync"/>'s raw-key contract.
    /// Needed by the cover editor (#3611) to re-read a cover's bytes before cropping.
    /// </summary>
    public async Task<Stream?> RetrieveRawKeyAsync(string rawKey, CancellationToken ct = default)
    {
        try
        {
            var getRequest = new GetObjectRequest
            {
                BucketName = _options.BucketName,
                Key = rawKey
            };

            var response = await _s3Client.GetObjectAsync(getRequest, ct).ConfigureAwait(false);

            _logger.LogInformation("Retrieved file from S3 (raw key): {Key}", rawKey);

            // Return the response stream (caller must dispose)
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning(ex, "Raw key not found in S3: {Key}", rawKey);
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
            _logger.LogError(ex, "Error retrieving raw key {Key}", rawKey);
            return null;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    private static string SanitizeFileName(string fileName)
    {
        return StringHelper.SanitizeFilename(fileName, maxLength: 200, fallbackName: "file");
    }
}
