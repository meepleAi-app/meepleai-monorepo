using Amazon.S3;
using Amazon.S3.Model;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Services.Pdf;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// <see cref="IPdfCoverUploadPipeline"/> implementation modeled on
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services.CoverR2UploadPipeline"/>:
/// talks directly to <see cref="IAmazonS3"/> using the raw <see cref="S3StorageOptions"/>
/// (no <see cref="Api.Services.Pdf.IBlobStorageService"/> indirection), because the L4
/// read side (<c>CoverUrlResolver.ResolvePublicAsync</c>) expects a flat
/// <c>{dbKey}-preview.webp</c> object key rather than the folder-based
/// <c>IBlobStorageService</c> layout.
/// </summary>
internal sealed class PdfCoverUploadPipeline : IPdfCoverUploadPipeline
{
    // Cover assets are immutable for 1 year; re-uploads use the same key so
    // Cloudflare CDN cache stays warm (mirrors CoverR2UploadPipeline).
    private const string ImmutableCacheControl = "public, max-age=31536000, immutable";
    private const string WebpContentType = "image/webp";

    private readonly IAmazonS3 _s3Client;
    private readonly S3StorageOptions _options;
    private readonly ILogger<PdfCoverUploadPipeline> _logger;

    public PdfCoverUploadPipeline(IAmazonS3 s3Client, S3StorageOptions options, ILogger<PdfCoverUploadPipeline> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> UploadAsync(string dbKey, byte[] webpBytes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dbKey))
        {
            throw new ArgumentException("DB key must be non-null and non-empty.", nameof(dbKey));
        }

        if (webpBytes is null || webpBytes.Length == 0)
        {
            throw new ArgumentException("WebP bytes must be non-null and non-empty.", nameof(webpBytes));
        }

        // The resolver (CoverUrlResolver.ResolvePublicAsync) appends -preview.webp
        // to the DB-stored key to derive the physical R2 object — this is the
        // write side of that convention.
        var objectKey = $"{dbKey}-preview.webp";

        using var stream = new MemoryStream(webpBytes, writable: false);
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = stream,
            ContentType = WebpContentType,
            AutoCloseStream = false,
            DisablePayloadSigning = true,
        };
        request.Headers.CacheControl = ImmutableCacheControl;

        await _s3Client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("PdfCoverUploadPipeline: uploaded cover WebP to {ObjectKey}", objectKey);

        return dbKey;
    }
}
