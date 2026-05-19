using Api.Services.Pdf;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Entities;

/// <summary>
/// Persistence model for the storage-operation outbox (issue #1314 PR 2).
///
/// Each row represents a pending or completed move of a single S3 / local blob
/// from the legacy layout (<c>pdf_uploads/{resourceKey}/{fileId}_{filename}</c>)
/// to the new categorized layout (<c>{category.ToS3Folder()}/{resourceKey}/{fileId}_{filename}</c>).
///
/// The outbox decouples DB commit from the side effect (aws s3 mv): a row is
/// inserted atomically with whatever DB transaction owns the source object,
/// and a background drainer (<c>StorageOperationOutboxBackgroundService</c>)
/// executes the actual S3 move with retry + exponential backoff.
///
/// Mirrored from <c>EmailOutboxEntity</c> (UserNotifications BC), same state
/// machine: <c>Pending → Sent | FailedPermanent</c>.
/// </summary>
public class StorageOperationOutboxEntity
{
    public Guid Id { get; init; }

    /// <summary>
    /// Correlation id for the migration batch this row belongs to (UUID).
    /// Set once per migration run; used in structured logs + metrics for
    /// per-run progress dashboards.
    /// </summary>
    public Guid MigrationId { get; init; }

    /// <summary>
    /// Source S3 key under the legacy layout
    /// (e.g. <c>pdf_uploads/{gameId}/{fileId}_{filename}</c>).
    /// </summary>
    public string LegacyKey { get; init; } = string.Empty;

    /// <summary>
    /// Target S3 key under the new categorized layout
    /// (e.g. <c>pdfs/{resourceKey}/{fileId}_{filename}</c>).
    /// </summary>
    public string NewKey { get; init; } = string.Empty;

    /// <summary>
    /// Blob category of the source object. Drives the target prefix via
    /// <c>BlobCategoryExtensions.ToS3Folder()</c>. Stored as string for
    /// schema-evolution friendliness (adding a new <see cref="BlobCategory"/>
    /// value does not require a DB migration).
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// The resource key under which both legacy and new keys live
    /// (the second path segment in either layout).
    /// </summary>
    public string ResourceKey { get; init; } = string.Empty;

    /// <summary>
    /// Earliest time at which the drainer should attempt this move (UTC).
    /// Bumped forward by exponential backoff on transient failures.
    /// </summary>
    public DateTime ScheduledAt { get; init; }

    /// <summary>
    /// Time at which the move completed successfully, or null if pending/failed.
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// Number of move attempts so far (used by retry/backoff logic).
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Last error message captured during the move, or null on success.
    /// </summary>
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Lifecycle status. One of: Pending | Sent | FailedPermanent.
    /// </summary>
    public string Status { get; set; } = "Pending";
}
