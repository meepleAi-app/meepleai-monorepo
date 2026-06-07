using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Aggregate root for a single catalog sync run (#1861, F4-A6 BE).
/// Tracks the lifecycle of an admin-triggered or cron-triggered catalog ingestion run
/// against an external provider (BGG API, CSV import, manual entry).
/// </summary>
/// <remarks>
/// State machine — see <see cref="CatalogSyncStatus"/> for the diagram.
/// Modelled after <see cref="MechanicRecalcJob"/> pattern (ADR-051 M2.1).
/// </remarks>
public sealed class CatalogSyncRun : AggregateRoot<Guid>
{
    // === Identity / metadata ===

    public CatalogSyncProvider Provider { get; private set; }

    public CatalogSyncStatus Status { get; private set; }

    /// <summary>Human-readable title shown in admin UI (e.g. "BGG full sync", "CSV bulk: designers-curation-v3.csv").</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>User who manually triggered the run; <c>null</c> when started by cron.</summary>
    public Guid? TriggeredByUserId { get; private set; }

    // === Counters ===

    public int ItemsAdded { get; private set; }
    public int ItemsUpdated { get; private set; }
    public int ItemsFailed { get; private set; }

    // === Error capture ===

    /// <summary>Machine-readable error code (e.g. "BGG_API_RATE_LIMIT_429"). Null while run is healthy.</summary>
    public string? ErrorCode { get; private set; }

    /// <summary>Human-readable error detail. Null while run is healthy.</summary>
    public string? ErrorDetail { get; private set; }

    // === Log capture ===

    /// <summary>Optional path/ref to a log tail file (file system path or blob URI). Null when no logs persisted.</summary>
    public string? LogTailJsonPath { get; private set; }

    // === Timestamps ===

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    // ===================================================
    // Constructors
    // ===================================================

    /// <summary>EF Core / repository reconstitution — do not call directly.</summary>
    private CatalogSyncRun() : base() { }

    private CatalogSyncRun(
        Guid id,
        CatalogSyncProvider provider,
        string title,
        Guid? triggeredByUserId,
        DateTimeOffset createdAt)
        : base(id)
    {
        Provider = provider;
        Title = title;
        TriggeredByUserId = triggeredByUserId;
        CreatedAt = createdAt;
        Status = CatalogSyncStatus.Queued;
    }

    // ===================================================
    // Factory
    // ===================================================

    /// <summary>
    /// Creates a new sync run in <see cref="CatalogSyncStatus.Queued"/> status.
    /// </summary>
    /// <param name="provider">Source pipeline (BGG / CSV / Manual).</param>
    /// <param name="title">Human-readable title (max 200 chars).</param>
    /// <param name="triggeredBy">User who triggered the run; <c>null</c> for cron.</param>
    public static CatalogSyncRun Enqueue(
        CatalogSyncProvider provider,
        string title,
        Guid? triggeredBy)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        if (title.Length > 200)
        {
            throw new ArgumentException("Title must be 200 characters or fewer.", nameof(title));
        }

        if (triggeredBy.HasValue && triggeredBy.Value == Guid.Empty)
        {
            throw new ArgumentException("TriggeredByUserId cannot be Guid.Empty (use null for cron).", nameof(triggeredBy));
        }

        return new CatalogSyncRun(
            id: Guid.NewGuid(),
            provider: provider,
            title: title,
            triggeredByUserId: triggeredBy,
            createdAt: DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Rebuilds an aggregate instance from persisted state without invoking lifecycle transitions.
    /// Intended for repository hydration only.
    /// </summary>
    public static CatalogSyncRun Reconstitute(
        Guid id,
        CatalogSyncProvider provider,
        CatalogSyncStatus status,
        string title,
        Guid? triggeredByUserId,
        int itemsAdded,
        int itemsUpdated,
        int itemsFailed,
        string? errorCode,
        string? errorDetail,
        string? logTailJsonPath,
        DateTimeOffset createdAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt)
    {
        return new CatalogSyncRun
        {
            Id = id,
            Provider = provider,
            Status = status,
            Title = title,
            TriggeredByUserId = triggeredByUserId,
            ItemsAdded = itemsAdded,
            ItemsUpdated = itemsUpdated,
            ItemsFailed = itemsFailed,
            ErrorCode = errorCode,
            ErrorDetail = errorDetail,
            LogTailJsonPath = logTailJsonPath,
            CreatedAt = createdAt,
            StartedAt = startedAt,
            CompletedAt = completedAt,
        };
    }

    // ===================================================
    // Lifecycle transitions
    // ===================================================

    /// <summary>
    /// Queued → Running. Stamps <see cref="StartedAt"/>.
    /// </summary>
    public void MarkRunning()
    {
        RequireStatus("mark running", CatalogSyncStatus.Queued);
        Status = CatalogSyncStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Increments <see cref="ItemsAdded"/> by <paramref name="count"/> (default 1). Allowed only while Running.
    /// </summary>
    public void RecordItemsAdded(int count = 1)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be non-negative.");
        }

        RequireStatus("record items added", CatalogSyncStatus.Running);
        ItemsAdded += count;
    }

    /// <summary>
    /// Increments <see cref="ItemsUpdated"/> by <paramref name="count"/> (default 1). Allowed only while Running.
    /// </summary>
    public void RecordItemsUpdated(int count = 1)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be non-negative.");
        }

        RequireStatus("record items updated", CatalogSyncStatus.Running);
        ItemsUpdated += count;
    }

    /// <summary>
    /// Increments <see cref="ItemsFailed"/> by <paramref name="count"/> (default 1). Allowed only while Running.
    /// </summary>
    public void RecordItemsFailed(int count = 1)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be non-negative.");
        }

        RequireStatus("record items failed", CatalogSyncStatus.Running);
        ItemsFailed += count;
    }

    /// <summary>
    /// Optional: attach a log tail file/blob reference, populated by background worker after run completes.
    /// Allowed only while Running or already terminal (Success/Failed/TimedOut).
    /// </summary>
    public void AttachLogTail(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Log tail path cannot be empty.", nameof(path));
        }

        if (Status == CatalogSyncStatus.Queued)
        {
            throw new InvalidCatalogSyncRunTransitionException(
                Id, Status, "attach log tail",
                CatalogSyncStatus.Running,
                CatalogSyncStatus.Success,
                CatalogSyncStatus.Failed,
                CatalogSyncStatus.TimedOut);
        }

        LogTailJsonPath = path;
    }

    /// <summary>
    /// Running → Success. Stamps <see cref="CompletedAt"/>.
    /// </summary>
    public void Complete()
    {
        RequireStatus("complete", CatalogSyncStatus.Running);
        Status = CatalogSyncStatus.Success;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Queued or Running → Failed. Captures <paramref name="errorCode"/> + <paramref name="errorDetail"/>
    /// and stamps <see cref="CompletedAt"/>.
    /// </summary>
    public void Fail(string errorCode, string errorDetail)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            throw new ArgumentException("Error code is required.", nameof(errorCode));
        }

        if (string.IsNullOrWhiteSpace(errorDetail))
        {
            throw new ArgumentException("Error detail is required.", nameof(errorDetail));
        }

        if (Status is not (CatalogSyncStatus.Queued or CatalogSyncStatus.Running))
        {
            throw new InvalidCatalogSyncRunTransitionException(
                Id, Status, "fail",
                CatalogSyncStatus.Queued, CatalogSyncStatus.Running);
        }

        Status = CatalogSyncStatus.Failed;
        ErrorCode = errorCode;
        ErrorDetail = errorDetail;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Running → TimedOut. Used by background watchdog after a configurable timeout (e.g. 10min).
    /// </summary>
    public void TimeOut(string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            throw new ArgumentException("Timeout detail is required.", nameof(detail));
        }

        RequireStatus("time out", CatalogSyncStatus.Running);

        Status = CatalogSyncStatus.TimedOut;
        ErrorCode = "SYNC_TIMEOUT";
        ErrorDetail = detail;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    // ===================================================
    // Helpers
    // ===================================================

    private void RequireStatus(string operation, CatalogSyncStatus required)
    {
        if (Status != required)
        {
            throw new InvalidCatalogSyncRunTransitionException(Id, Status, operation, required);
        }
    }
}
