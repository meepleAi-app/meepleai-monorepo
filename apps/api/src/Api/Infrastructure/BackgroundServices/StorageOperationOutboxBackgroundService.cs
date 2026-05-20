using Amazon.S3;
using Amazon.S3.Model;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Entities;
using Api.Infrastructure;
using Api.Observability;
using Api.Services.Pdf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// Drains the <c>storage_operation_outbox</c> table (issue #1314 PR 2).
/// Mirrors <c>EmailOutboxBackgroundService</c>: poll every 30s, batch 25,
/// per-row try, exponential backoff [1m, 5m, 30m, 2h, 6h], MaxAttempts 5
/// before transition to FailedPermanent.
///
/// For each Pending row the service:
///   1. Verifies the legacy object still exists at <see cref="StorageOperationOutboxEntity.LegacyKey"/>.
///   2. Issues an <c>aws s3 mv</c>-equivalent (CopyObjectAsync + DeleteObjectAsync)
///      to move the object to <see cref="StorageOperationOutboxEntity.NewKey"/>.
///   3. On success marks the row Sent + sets SentAt.
///   4. On failure increments AttemptCount + reschedules.
///
/// Disabled by default (<see cref="StorageLayoutOptions.MigrationEnabled"/> = false)
/// so the service can be deployed to all environments at Phase 0 without
/// triggering moves. Operator flips the flag to start Phase 1.
///
/// Only runs against S3-compatible storage. For local-FS dev environments
/// (<c>STORAGE_PROVIDER=local</c>) the service still polls but logs a single
/// warning per tick and otherwise no-ops — local-layout migration is left as
/// an explicit operator action (script in <c>scripts/migrate-storage-local.sh</c>,
/// out of scope for PR 2).
/// </summary>
internal sealed class StorageOperationOutboxBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 25;
    private const int MaxAttempts = 5;

    private static readonly TimeSpan[] BackoffSchedule =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(6),
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly StorageLayoutOptions _layoutOptions;
    private readonly ILogger<StorageOperationOutboxBackgroundService> _logger;

    public StorageOperationOutboxBackgroundService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<StorageLayoutOptions> layoutOptions,
        ILogger<StorageOperationOutboxBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        ArgumentNullException.ThrowIfNull(layoutOptions);
        _layoutOptions = layoutOptions.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_layoutOptions.MigrationEnabled)
        {
            _logger.LogInformation(
                "StorageOperationOutboxBackgroundService started but disabled (StorageLayout:MigrationEnabled=false). " +
                "Service will not drain the outbox; flip the flag to enable Phase 1 migration.");
            // Keep the service alive (BackgroundService contract) but skip the
            // drain loop. Operator can restart the pod after flipping the flag.
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
            return;
        }

        _logger.LogInformation(
            "StorageOperationOutboxBackgroundService started. Poll interval: {Interval}s, batch size: {BatchSize}, max attempts: {MaxAttempts}",
            PollInterval.TotalSeconds,
            BatchSize,
            MaxAttempts);

        // Announce active layout version once per drainer lifecycle (PR 2 obs spec).
        MeepleAiMetrics.StorageLayoutVersionAnnouncementsTotal.Add(
            1,
            new KeyValuePair<string, object?>("layout_version", _layoutOptions.LayoutVersionLabel));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainOnceAsync(stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // BACKGROUND SERVICE BOUNDARY: transient errors in the drain loop
            // must not crash the host. Next poll tick retries.
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error during storage-outbox drain");
            }
#pragma warning restore CA1031

            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [SuppressMessage("SonarAnalyzer.CSharp", "S125", Justification = "Explanatory comments about per-row flush — false-positive on em-dashes.")]
    private async Task DrainOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var blobStorage = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();

        // Drainer only operates against S3 storage — local FS migration is an
        // explicit operator action. The DI factory returns S3BlobStorageService
        // when STORAGE_PROVIDER=s3 (see BlobStorageServiceFactory).
        if (blobStorage is not S3BlobStorageService s3Storage)
        {
            _logger.LogDebug("Storage outbox drain skipped: blob storage is not S3-backed");
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // PERF-06: DbContext defaults to NoTracking globally. Drainer must
        // opt into tracking so the success-path mutations on `row.Status`,
        // `row.SentAt`, `row.AttemptCount`, `row.LastError` reach the DB
        // through SaveChanges. Without this, SaveChanges silently no-ops
        // (catch-path still works because it touches `dbContext.Entry(row)`,
        // which forces the entity into the change tracker as Modified).
        // Discovered during the Phase 1 rollout on 2026-05-20: drainer kept
        // moving objects on S3 but the outbox rows stayed Pending forever.
        var due = await dbContext.StorageOperationOutbox
            .AsTracking()
            .Where(e => e.Status == "Pending" && e.ScheduledAt <= now)
            .OrderBy(e => e.ScheduledAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (due.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Storage outbox: draining {Count} pending row(s)", due.Count);

        foreach (var row in due)
        {
            await TryMoveAsync(row, dbContext, s3Storage, cancellationToken).ConfigureAwait(false);
            // Flush per-row so a cancellation mid-batch does NOT lose mutations
            // from rows already processed (Sent / AttemptCount++).
            await dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    [SuppressMessage("SonarAnalyzer.CSharp", "S125", Justification = "Explanatory comments — false-positive on em-dashes.")]
    private async Task TryMoveAsync(
        StorageOperationOutboxEntity row,
        MeepleAiDbContext dbContext,
        S3BlobStorageService s3Storage,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var s3Client = s3Storage.S3Client;
            var bucket = s3Storage.Options.BucketName;

            // Resilience to mid-migration cleanup races (review finding #3):
            // if NewKey already exists (CopyObject succeeded on a previous
            // attempt + DeleteObject failed) AND LegacyKey is gone (external
            // cleanup, e.g. the orphan-cleanup script from PR #1316), we
            // would otherwise call CopyObjectAsync against a missing source
            // and burn retries until FailedPermanent — even though the move
            // is effectively complete. Pre-check both keys and short-circuit
            // to the delete step (or skip the move entirely).
            var newKeyExists = await KeyExistsAsync(s3Client, bucket, row.NewKey, cancellationToken).ConfigureAwait(false);
            if (!newKeyExists)
            {
                // Standard path: copy legacy → new. CopyObjectAsync is atomic
                // on the S3 side; if it succeeds the new key is durable
                // before we delete the legacy.
                await s3Client.CopyObjectAsync(new CopyObjectRequest
                {
                    SourceBucket = bucket,
                    SourceKey = row.LegacyKey,
                    DestinationBucket = bucket,
                    DestinationKey = row.NewKey,
                    ServerSideEncryptionMethod = s3Storage.Options.EnableEncryption
                        ? ServerSideEncryptionMethod.AES256
                        : ServerSideEncryptionMethod.None,
                }, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogInformation(
                    "Storage outbox: NewKey {NewKey} already exists (prior attempt or external migration); skipping copy",
                    row.NewKey);
            }

            // Delete legacy. If the key was already cleaned up externally we
            // swallow the NoSuchKey error — the migration goal (object lives
            // at NewKey, not at LegacyKey) is already satisfied.
            try
            {
                await s3Client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = bucket,
                    Key = row.LegacyKey,
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (AmazonS3Exception delEx)
                when (delEx.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation(
                    delEx,
                    "Storage outbox: LegacyKey {LegacyKey} already deleted externally; treating as success",
                    row.LegacyKey);
            }

            row.Status = "Sent";
            row.SentAt = _timeProvider.GetUtcNow().UtcDateTime;
            row.LastError = null;
            row.AttemptCount += 1;

            stopwatch.Stop();
            MeepleAiMetrics.StorageMigrationDurationMs.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("category", row.Category));
            MeepleAiMetrics.StorageMigrationObjectsMigratedTotal.Add(
                1,
                new KeyValuePair<string, object?>("status", "success"),
                new KeyValuePair<string, object?>("category", row.Category));

            _logger.LogInformation(
                "Storage outbox: moved {LegacyKey} → {NewKey} (migration {MigrationId})",
                row.LegacyKey, row.NewKey, row.MigrationId);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            row.AttemptCount += 1;
            row.LastError = TruncateError(ex.Message);

            if (row.AttemptCount >= MaxAttempts)
            {
                row.Status = "FailedPermanent";
                MeepleAiMetrics.StorageMigrationObjectsMigratedTotal.Add(
                    1,
                    new KeyValuePair<string, object?>("status", "failed_permanent"),
                    new KeyValuePair<string, object?>("category", row.Category));
                _logger.LogError(
                    ex,
                    "Storage outbox row {RowId} marked FailedPermanent after {Attempts} attempts. " +
                    "LegacyKey: {LegacyKey}; migration {MigrationId}",
                    row.Id, row.AttemptCount, row.LegacyKey, row.MigrationId);
            }
            else
            {
                MeepleAiMetrics.StorageMigrationObjectsMigratedTotal.Add(
                    1,
                    new KeyValuePair<string, object?>("status", "failed"),
                    new KeyValuePair<string, object?>("category", row.Category));
                var backoffIndex = Math.Min(row.AttemptCount - 1, BackoffSchedule.Length - 1);
                var nextAttempt = _timeProvider.GetUtcNow().UtcDateTime + BackoffSchedule[backoffIndex];

                // ScheduledAt is init-only on the entity — bump via the change
                // tracker rather than mutating the property directly.
                dbContext.Entry(row).Property(nameof(StorageOperationOutboxEntity.ScheduledAt)).CurrentValue = nextAttempt;

                _logger.LogWarning(
                    ex,
                    "Storage outbox row {RowId} attempt {Attempt}/{Max} failed; rescheduled to {NextAttempt}",
                    row.Id, row.AttemptCount, MaxAttempts, nextAttempt);
            }
        }
#pragma warning restore CA1031
    }

    private static string TruncateError(string message)
    {
        const int MaxLen = 1990;
        return message.Length <= MaxLen ? message : message[..MaxLen] + "...";
    }

    /// <summary>
    /// Lightweight existence probe via <c>ListObjectsV2</c> with prefix=key
    /// and MaxKeys=1. Returns true if S3 contains an object at exactly the
    /// supplied key. Used by the move pre-check (review finding #3) to avoid
    /// burning retries on copy-against-missing-source races.
    /// </summary>
    private static async Task<bool> KeyExistsAsync(IAmazonS3 s3Client, string bucket, string key, CancellationToken ct)
    {
        var response = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = bucket,
            Prefix = key,
            MaxKeys = 1,
        }, ct).ConfigureAwait(false);

        // ListObjectsV2 prefix-match: confirm the returned object key matches
        // exactly (Prefix is a "starts-with" filter, so e.g. requesting key
        // "a/b" could match "a/b" AND "a/bc/d").
        return response.S3Objects.Any(o => string.Equals(o.Key, key, StringComparison.Ordinal));
    }
}
