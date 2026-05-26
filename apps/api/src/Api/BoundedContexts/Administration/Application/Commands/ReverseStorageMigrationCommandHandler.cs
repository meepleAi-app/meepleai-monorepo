using Amazon.S3;
using Amazon.S3.Model;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Entities;
using Api.Infrastructure;
using Api.Services.Pdf;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for <see cref="ReverseStorageMigrationCommand"/> (issue #1333).
///
/// Phase B backout flow:
/// <list type="number">
/// <item>Load rows from <c>storage_operation_outbox</c> filtered by MigrationId
/// in states {Pending, Sent}. FailedPermanent and Reverted rows are left
/// untouched (Skipped count).</item>
/// <item>For each <c>Sent</c> row: CopyObjectAsync NewKey → LegacyKey, then
/// DeleteObjectAsync NewKey. On success transition Status to <c>Reverted</c>.</item>
/// <item>For each <c>Pending</c> row: transition Status to <c>Reverted</c>
/// directly (drainer will pick the next batch and skip the row because the
/// query filters on Status = Pending).</item>
/// </list>
///
/// Idempotent: a re-run on an already-reverted migration sees no eligible rows
/// and returns Skipped=N.
/// </summary>
internal sealed class ReverseStorageMigrationCommandHandler
    : IRequestHandler<ReverseStorageMigrationCommand, ReverseStorageMigrationResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<ReverseStorageMigrationCommandHandler> _logger;

    public ReverseStorageMigrationCommandHandler(
        MeepleAiDbContext db,
        IBlobStorageService blobStorage,
        ILogger<ReverseStorageMigrationCommandHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ReverseStorageMigrationResult> Handle(
        ReverseStorageMigrationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<string>();

        if (_blobStorage is not S3BlobStorageService s3Storage)
        {
            errors.Add("Storage provider is not S3-backed; reverse-migration only applies when STORAGE_PROVIDER=s3.");
            return new ReverseStorageMigrationResult(
                MigrationId: request.MigrationId,
                TotalRows: 0,
                ReversedFromSent: 0,
                CancelledFromPending: 0,
                Skipped: 0,
                Failed: 0,
                IsDryRun: request.DryRun,
                Errors: errors);
        }

        var s3Client = s3Storage.S3Client;
        var bucket = s3Storage.Options.BucketName;

        var reversedFromSent = 0;
        var cancelledFromPending = 0;
        var skipped = 0;
        var failed = 0;
        var totalRows = 0;

        var totalCount = await _db.StorageOperationOutbox
            .Where(r => r.MigrationId == request.MigrationId)
            .CountAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Reverse migration starting: migration={MigrationId}, total rows={Count}, dryRun={DryRun}",
            request.MigrationId, totalCount, request.DryRun);

        // Review finding I-1: process rows in fixed-size chunks rather than
        // loading all into memory. A migration touching millions of objects
        // would OOM the API pod with a single ToListAsync. Per-chunk
        // SaveChanges also gives crash-safety: rows already transitioned to
        // Reverted are persisted even if the next chunk fails mid-iteration.
        const int chunkSize = 200;
        for (var offset = 0; offset < totalCount; offset += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // PERF-06: DbContext defaults to NoTracking globally.
            // ReverseSentRowAsync + the Pending branch mutate `row.Status`
            // and `row.LastError` directly; without AsTracking those changes
            // never reach the DB through SaveChanges. Mirror fix in
            // StorageOperationOutboxBackgroundService.DrainOnceAsync.
            var rows = await _db.StorageOperationOutbox
                .AsTracking()
                .Where(r => r.MigrationId == request.MigrationId)
                .OrderBy(r => r.CreatedAt)
                .Skip(offset)
                .Take(chunkSize)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var row in rows)
            {
                totalRows++;
                switch (row.Status)
                {
                    case "Sent":
                        if (request.DryRun)
                        {
                            reversedFromSent++;
                            break;
                        }
                        try
                        {
                            await ReverseSentRowAsync(s3Client, bucket, row, s3Storage.Options.EnableEncryption, cancellationToken).ConfigureAwait(false);
                            row.Status = "Reverted";
                            row.LastError = "Reverted by operator";
                            reversedFromSent++;
                        }
#pragma warning disable CA1031 // Service boundary: per-row reverse failures must not abort the batch.
                        catch (Exception ex)
                        {
                            failed++;
                            // SECURITY (test-guarded): never embed ex.Message — the
                            // underlying S3 Copy/DeleteObjectAsync throw
                            // AmazonS3Exception carrying bucket names, RequestId,
                            // region and other infrastructure details. Full diagnostic
                            // detail remains in ILogger.LogWarning. Enforced by
                            // ReverseStorageMigrationCommandHandlerTests.Handle_WhenS3CopyObjectThrowsForSentRow_ShouldNotIncludeRawExceptionMessageInError.
                            errors.Add($"Reverse failed for {row.NewKey} ({ex.GetType().Name})");
                            _logger.LogWarning(ex, "Reverse failed for {NewKey}", row.NewKey);
                        }
#pragma warning restore CA1031
                        break;

                    case "Pending":
                        if (request.DryRun)
                        {
                            cancelledFromPending++;
                            break;
                        }
                        row.Status = "Reverted";
                        row.LastError = "Cancelled before drainer picked up";
                        cancelledFromPending++;
                        break;

                    case "Reverted":
                    case "FailedPermanent":
                        skipped++;
                        break;

                    default:
                        skipped++;
                        break;
                }
            }

            if (!request.DryRun)
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation(
            "Reverse migration completed: migration={MigrationId}, reversed={Reversed}, cancelled={Cancelled}, skipped={Skipped}, failed={Failed}",
            request.MigrationId, reversedFromSent, cancelledFromPending, skipped, failed);

        return new ReverseStorageMigrationResult(
            MigrationId: request.MigrationId,
            TotalRows: totalRows,
            ReversedFromSent: reversedFromSent,
            CancelledFromPending: cancelledFromPending,
            Skipped: skipped,
            Failed: failed,
            IsDryRun: request.DryRun,
            Errors: errors);
    }

    /// <summary>
    /// Performs the inverse of the drainer's CopyObject+Delete: NewKey → LegacyKey + delete NewKey.
    /// Mirror of <c>StorageOperationOutboxBackgroundService.TryMoveAsync</c>.
    /// </summary>
    private static async Task ReverseSentRowAsync(
        IAmazonS3 s3Client,
        string bucket,
        StorageOperationOutboxEntity row,
        bool enableEncryption,
        CancellationToken cancellationToken)
    {
        await s3Client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = bucket,
            SourceKey = row.NewKey,
            DestinationBucket = bucket,
            DestinationKey = row.LegacyKey,
            ServerSideEncryptionMethod = enableEncryption
                ? ServerSideEncryptionMethod.AES256
                : ServerSideEncryptionMethod.None,
        }, cancellationToken).ConfigureAwait(false);

        try
        {
            await s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = bucket,
                Key = row.NewKey,
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception ex)
            when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // NewKey already absent (parallel cleanup): treat as success.
        }
    }
}
