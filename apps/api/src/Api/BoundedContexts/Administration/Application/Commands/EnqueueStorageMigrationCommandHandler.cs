using Amazon.S3;
using Amazon.S3.Model;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Services.Pdf;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for <see cref="EnqueueStorageMigrationCommand"/>.
///
/// Enumerates every S3 object under <c>LegacyPrefix</c> via
/// <c>ListObjectsV2Async</c> pagination, parses each <c>legacyKey</c> into
/// its <c>resourceKey</c> segment, and calls
/// <see cref="IStorageOperationOutboxService.EnqueueAsync"/> per object.
///
/// The outbox layer's UNIQUE constraint on <c>LegacyKey</c> makes the operation
/// idempotent across re-runs: rows previously enqueued under any
/// migrationId are counted as <see cref="EnqueueStorageMigrationResult.Skipped"/>.
///
/// Only runs against S3-backed storage. On local-FS dev the handler short-circuits
/// with an explanatory error in the result envelope.
/// </summary>
internal sealed class EnqueueStorageMigrationCommandHandler
    : IRequestHandler<EnqueueStorageMigrationCommand, EnqueueStorageMigrationResult>
{
    private readonly IBlobStorageService _blobStorage;
    private readonly IStorageOperationOutboxService _outbox;
    private readonly ILogger<EnqueueStorageMigrationCommandHandler> _logger;

    public EnqueueStorageMigrationCommandHandler(
        IBlobStorageService blobStorage,
        IStorageOperationOutboxService outbox,
        ILogger<EnqueueStorageMigrationCommandHandler> logger)
    {
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EnqueueStorageMigrationResult> Handle(
        EnqueueStorageMigrationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<string>();

        if (_blobStorage is not S3BlobStorageService s3Storage)
        {
            errors.Add("Storage provider is not S3-backed; storage migration is operator-driven and applies only when STORAGE_PROVIDER=s3.");
            return new EnqueueStorageMigrationResult(
                MigrationId: request.MigrationId,
                TotalObjects: 0,
                Enqueued: 0,
                Skipped: 0,
                Failed: 0,
                IsDryRun: request.DryRun,
                Errors: errors);
        }

        var s3Client = s3Storage.S3Client;
        var bucket = s3Storage.Options.BucketName;

        var totalObjects = 0;
        var enqueued = 0;
        var skipped = 0;
        var failed = 0;
        string? continuationToken = null;

        _logger.LogInformation(
            "Storage migration enqueue starting: migration={MigrationId}, bucket={Bucket}, prefix={Prefix}, category={Category}, dryRun={DryRun}",
            request.MigrationId, bucket, request.LegacyPrefix, request.Category, request.DryRun);

        do
        {
            ListObjectsV2Response listResponse;
            try
            {
                listResponse = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = bucket,
                    Prefix = request.LegacyPrefix,
                    ContinuationToken = continuationToken,
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (AmazonS3Exception ex)
            {
                errors.Add($"S3 list-objects failed: {ex.ErrorCode} — {ex.Message}");
                _logger.LogError(ex, "S3 list-objects failed for prefix {Prefix}", request.LegacyPrefix);
                break;
            }

            foreach (var s3Object in listResponse.S3Objects)
            {
                totalObjects++;
                var legacyKey = s3Object.Key;
                var resourceKey = ExtractResourceKey(legacyKey, request.LegacyPrefix);
                if (resourceKey is null)
                {
                    errors.Add($"Skipped malformed key (cannot extract resourceKey): {legacyKey}");
                    failed++;
                    continue;
                }

                if (request.DryRun)
                {
                    enqueued++;
                    continue;
                }

                try
                {
                    var inserted = await _outbox.EnqueueAsync(
                        request.MigrationId,
                        legacyKey,
                        request.Category,
                        resourceKey,
                        scheduledAt: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (inserted)
                    {
                        enqueued++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
#pragma warning disable CA1031 // Service boundary: per-object enqueue failures must not abort the batch.
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"Enqueue failed for {legacyKey}: {ex.Message}");
                    _logger.LogWarning(ex, "Enqueue failed for {LegacyKey}", legacyKey);
                }
#pragma warning restore CA1031
            }

            var hasMore = listResponse.IsTruncated;
            continuationToken = hasMore ? listResponse.NextContinuationToken : null;
        }
        while (continuationToken is not null && !cancellationToken.IsCancellationRequested);

        _logger.LogInformation(
            "Storage migration enqueue completed: migration={MigrationId}, total={Total}, enqueued={Enqueued}, skipped={Skipped}, failed={Failed}",
            request.MigrationId, totalObjects, enqueued, skipped, failed);

        return new EnqueueStorageMigrationResult(
            MigrationId: request.MigrationId,
            TotalObjects: totalObjects,
            Enqueued: enqueued,
            Skipped: skipped,
            Failed: failed,
            IsDryRun: request.DryRun,
            Errors: errors);
    }

    /// <summary>
    /// Extracts the <c>{resourceKey}</c> segment from a legacy S3 key of shape
    /// <c>{legacyPrefix}{resourceKey}/{fileId}_{filename}</c>. Returns null if
    /// the key is malformed (missing the resourceKey segment).
    /// </summary>
    internal static string? ExtractResourceKey(string legacyKey, string legacyPrefix)
    {
        if (!legacyKey.StartsWith(legacyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var remainder = legacyKey[legacyPrefix.Length..];
        var separatorIndex = remainder.IndexOf('/', StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return null;
        }

        return remainder[..separatorIndex];
    }
}
