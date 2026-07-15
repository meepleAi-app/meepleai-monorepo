using Amazon.S3;
using Amazon.S3.Model;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.Services.Pdf;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Issue #2947 — R2 upload pipeline for BGG-downloaded cover images. Mirrors
/// <see cref="CoverR2UploadPipeline"/> / <c>PdfCoverUploadPipeline</c>: talks
/// directly to <see cref="IAmazonS3"/> with a raw deterministic key rather than
/// going through <c>IBlobStorageService.StoreAsync</c> (which mints a random
/// fileId the resolver cannot reconstruct).
/// </summary>
internal sealed class BggCoverUploadPipeline : IBggCoverUploadPipeline
{
    // Cover assets are immutable for 1 year; re-uploads reuse the same key so
    // Cloudflare CDN cache stays warm (mirrors CoverR2UploadPipeline).
    private const string ImmutableCacheControl = "public, max-age=31536000, immutable";
    private const string DefaultExtension = ".jpg";

    private readonly IAmazonS3 _s3Client;
    private readonly S3StorageOptions _options;
    private readonly ILogger<BggCoverUploadPipeline> _logger;

    public BggCoverUploadPipeline(
        IAmazonS3 s3Client,
        S3StorageOptions options,
        ILogger<BggCoverUploadPipeline> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> UploadAsync(int bggId, byte[] imageBytes, string extension, CancellationToken ct)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            throw new ArgumentException("imageBytes must be non-null and non-empty.", nameof(imageBytes));
        }

        var ext = NormalizeExtension(extension);
        var contentType = ContentTypeFor(ext);
        // Deterministic physical key = DB key (resolver appends NO suffix for L2.5).
        var objectKey = $"bgg-covers/{bggId.ToString(CultureInfo.InvariantCulture)}/cover{ext}";

        using var stream = new MemoryStream(imageBytes, writable: false);
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false,
            // Required for S3-compatible providers (R2/MinIO) that don't support
            // STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER (mirrors CoverR2UploadPipeline).
            DisablePayloadSigning = true,
        };
        request.Headers.CacheControl = ImmutableCacheControl;

        // OperationCanceledException propagates naturally from PutObjectAsync.
        try
        {
            var response = await _s3Client.PutObjectAsync(request, ct).ConfigureAwait(false);
            _logger.LogInformation(
                "Uploaded BGG cover to R2: BggId={BggId}, Key={Key}, Size={Size} bytes, ETag={ETag}",
                bggId, objectKey, imageBytes.Length, response.ETag);
            return objectKey;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(
                ex,
                "R2 BGG cover upload failed: BggId={BggId}, Key={Key}, StatusCode={Status}, ErrorCode={ErrorCode}",
                bggId, objectKey, ex.StatusCode, ex.ErrorCode);
            throw;
        }
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return DefaultExtension;
        }

        var ext = extension.StartsWith('.') ? extension : "." + extension;
        ext = ext.ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => ext,
            _ => DefaultExtension,
        };
    }

    private static string ContentTypeFor(string normalizedExtension) => normalizedExtension switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };
}
