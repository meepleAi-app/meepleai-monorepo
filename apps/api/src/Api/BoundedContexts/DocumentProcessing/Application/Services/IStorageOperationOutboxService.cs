using Api.Services.Pdf;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Caller-facing surface to enqueue a single blob storage move (legacy → new
/// layout) onto the outbox. Issue #1314 PR 2.
///
/// Rows are durably persisted (UNIQUE index on <c>LegacyKey</c> dedupes
/// re-runs of the migration script) and drained out-of-band by
/// <c>StorageOperationOutboxBackgroundService</c>, which performs the actual
/// <c>aws s3 mv legacy → new</c> with retry + exponential backoff.
/// </summary>
public interface IStorageOperationOutboxService
{
    /// <summary>
    /// Enqueue a single storage move. Returns true if a new row was inserted,
    /// false if a row with the same legacy key already exists (idempotent).
    /// </summary>
    /// <param name="migrationId">Correlation id for the migration batch this row belongs to.</param>
    /// <param name="legacyKey">Source S3 key under <c>pdf_uploads/...</c>.</param>
    /// <param name="category">Blob category — drives the target prefix via <c>ToS3Folder()</c>.</param>
    /// <param name="resourceKey">Resource id under which both legacy and new keys live.</param>
    /// <param name="scheduledAt">Earliest time the drainer should attempt the move (UTC). Defaults to now.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>True if a new row was inserted; false if a duplicate legacy key was detected.</returns>
    Task<bool> EnqueueAsync(
        Guid migrationId,
        string legacyKey,
        BlobCategory category,
        string resourceKey,
        DateTime? scheduledAt = null,
        CancellationToken cancellationToken = default);
}
