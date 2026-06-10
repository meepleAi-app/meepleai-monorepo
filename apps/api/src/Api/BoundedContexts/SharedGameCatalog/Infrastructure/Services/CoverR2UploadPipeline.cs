using Amazon.S3;
using Amazon.S3.Model;
using Api.Services.Pdf;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Issue #1823 Phase B (ADR DEC-3h) — Cloudflare R2 upload pipeline for
/// Wikidata-derived cover images. See <see cref="ICoverR2UploadPipeline"/>
/// for the contract and the documented double-suffix pitfall.
/// </summary>
internal sealed class CoverR2UploadPipeline : ICoverR2UploadPipeline
{
    // ADR DEC-3h — cover assets are immutable for 1 year; re-uploads use the
    // same key so Cloudflare CDN cache stays warm.
    private const string ImmutableCacheControl = "public, max-age=31536000, immutable";
    private const string WebpContentType = "image/webp";

    private readonly IAmazonS3 _s3Client;
    private readonly S3StorageOptions _options;
    private readonly ILogger<CoverR2UploadPipeline> _logger;

    public CoverR2UploadPipeline(
        IAmazonS3 s3Client,
        S3StorageOptions options,
        ILogger<CoverR2UploadPipeline> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> UploadAsync(Guid gameId, byte[] webpBytes, CancellationToken ct)
    {
        if (webpBytes is null || webpBytes.Length == 0)
        {
            throw new ArgumentException(
                "WebP bytes must be non-null and non-empty.",
                nameof(webpBytes));
        }

        var gameIdSegment = gameId.ToString("D", CultureInfo.InvariantCulture);
        // Physical R2 object key — INCLUDES .webp suffix (matches Content-Type).
        var objectKey = $"covers/{gameIdSegment}/cover.webp";
        // DB-safe key — EXCLUDES .webp suffix. CoverUrlResolver.cs:73-79 will
        // append ".webp" when minting presigned URLs; storing the suffix here
        // would yield "covers/.../cover.webp.webp" → 404.
        var dbKey = $"covers/{gameIdSegment}/cover";

        using var stream = new MemoryStream(webpBytes, writable: false);
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = stream,
            ContentType = WebpContentType,
            AutoCloseStream = false,
            // Required for S3-compatible providers (R2/MinIO) that don't support
            // STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER. Matches the pattern
            // already used by S3BlobStorageService.StoreAsync.
            DisablePayloadSigning = true,
        };

        // ADR DEC-3h — immutable cache.
        request.Headers.CacheControl = ImmutableCacheControl;

        // OperationCanceledException propagates naturally from PutObjectAsync —
        // do not wrap it in a try/catch (caller must observe cancellation).
        try
        {
            var response = await _s3Client
                .PutObjectAsync(request, ct)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Uploaded Wikidata cover to R2: GameId={GameId}, Key={Key}, Size={Size} bytes, ETag={ETag}",
                gameId,
                objectKey,
                webpBytes.Length,
                response.ETag);

            return dbKey;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(
                ex,
                "R2 cover upload failed: GameId={GameId}, Key={Key}, StatusCode={Status}, ErrorCode={ErrorCode}",
                gameId,
                objectKey,
                ex.StatusCode,
                ex.ErrorCode);
            throw;
        }
    }
}
