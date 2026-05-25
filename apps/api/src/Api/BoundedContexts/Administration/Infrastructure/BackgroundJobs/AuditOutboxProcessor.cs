using System.Text.Json;
using Api.BoundedContexts.Administration.Application;
using Api.BoundedContexts.Administration.Infrastructure.Health;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.BackgroundJobs;

/// <summary>
/// Background service that drains rows from <c>audit_outbox</c> (Pending) into
/// permanent <c>audit_logs</c> rows. The atomic write path in <c>AuditLoggingBehavior</c>
/// commits Pending outbox rows together with the mutation; this processor decouples the
/// final materialization from the request hot-path.
///
/// Lifecycle (T4): poll every 5 seconds, drain up to 100 rows per batch in a single
/// transaction. On per-row materialization failure, the row is marked Failed with the
/// exception message (visible via the dashboard); successful rows are marked Sent.
/// Idempotency hardening (T4b — ON CONFLICT DO NOTHING + Prometheus metrics) lands next.
///
/// SP5 Admin Security S1 — Task 4.
/// </summary>
internal sealed class AuditOutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditOutboxProcessor> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IAuditOutboxHealthTracker _healthTracker;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
    private const int DefaultBatchSize = 100;
    private const string OversizeFailureReason = "payload_oversize";

    public AuditOutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<AuditOutboxProcessor> logger,
        IAuditOutboxHealthTracker healthTracker,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _healthTracker = healthTracker;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(DefaultBatchSize, stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Resilience: a failing batch must not stop the host
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "AuditOutboxProcessor batch failed; will retry on next poll");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown — fall through to loop condition
            }
        }
    }

    /// <summary>
    /// Drains a single batch of Pending outbox rows. For each row:
    ///   - Deserialize PayloadJson into <see cref="AuditOutboxPayload"/>.
    ///   - If <c>Oversize == true</c>, mark Failed with reason <c>"payload_oversize"</c>
    ///     (T2b contract — truncated payload must not be persisted as authoritative audit).
    ///   - Otherwise materialize an <see cref="AuditLogEntity"/>, add to <c>audit_logs</c>,
    ///     and mark the source row Sent.
    ///   - On any per-row exception, mark Failed with the exception message (truncated to
    ///     2048 chars by <see cref="AuditOutboxEntity.MarkFailed"/>) so the batch continues.
    ///
    /// All state changes (audit_logs INSERTs + outbox status transitions) commit in a single
    /// transaction. Returns the count of rows considered in this batch (Sent + Failed).
    /// Exposed publicly for deterministic integration testing — production drives this from
    /// <see cref="ExecuteAsync"/>.
    /// </summary>
    public async Task<int> RunOnceAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be positive.");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // FIFO drain by CreatedAt — preserves the order in which AuditLoggingBehavior
        // enqueued the mutations. Tracking is required so row.MarkSent/MarkFailed state
        // transitions persist via SaveChanges.
        var pending = await db.AuditOutbox
            .AsTracking()
            .Where(r => r.Status == OutboxStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pending.Count == 0)
        {
            // Refresh metrics even on empty batches so gauges reflect "nothing Pending"
            // when the workload truly is idle (and not just a stale snapshot).
            await UpdateHealthSnapshotAsync(db, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        var now = _timeProvider.GetUtcNow();

        // T4b idempotency pre-check: outbox.Id is the GUID we will use as audit_logs.Id when
        // materializing. If a prior batch already inserted an audit_logs row for any of these
        // outbox ids (e.g. operator-triggered Sent→Pending flip, or a partial-commit recovery
        // scenario), we MUST NOT insert a duplicate. EF-native batch SELECT keeps this readable
        // and avoids the raw-SQL ON CONFLICT path while preserving the same semantic outcome.
        var pendingIds = pending.Select(r => r.Id).ToList();
        var existingAuditIds = await db.AuditLogs
            .AsNoTracking()
            .Where(l => pendingIds.Contains(l.Id))
            .Select(l => l.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingAuditIdSet = existingAuditIds.ToHashSet();

        // Single transaction for the batch: either all transitions + INSERTs commit, or none.
        // CreateExecutionStrategy is required so the prod NpgsqlRetryingExecutionStrategy can
        // retry the whole batch on transient connection errors. In Testcontainers (no retry
        // strategy configured) the delegate runs exactly once.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var tx = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var _ = tx.ConfigureAwait(false);

            foreach (var row in pending)
            {
                try
                {
                    var payload = JsonSerializer.Deserialize<AuditOutboxPayload>(row.PayloadJson)
                        ?? throw new InvalidOperationException("Deserialized payload is null.");

                    if (payload.Oversize)
                    {
                        // T2b contract: rather than persist a truncated/inaccurate audit, mark the
                        // row Failed so operators can investigate via the dashboard.
                        row.MarkFailed(OversizeFailureReason, now);
                        continue;
                    }

                    if (existingAuditIdSet.Contains(row.Id))
                    {
                        // T4b idempotency: the audit_logs row already exists for this id (the
                        // outbox.Id == audit_logs.Id contract). Skip the INSERT to avoid a
                        // duplicate-key violation, but still transition Pending → Sent so the
                        // outbox is drained.
                        row.MarkSent(now);
                        continue;
                    }

                    var auditLog = MaterializeAuditLog(payload, row.Id);
                    db.AuditLogs.Add(auditLog);
                    row.MarkSent(now);
                }
#pragma warning disable CA1031 // Per-row resilience: poison-message must not stop the batch
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    _logger.LogError(ex,
                        "Failed to materialize audit_log from outbox row {OutboxId}; marking row Failed",
                        row.Id);
                    row.MarkFailed(ex.Message, now);
                }
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            // Refresh the health snapshot AFTER the commit so observers see the post-batch
            // counts (pending may still be > 0 if batchSize was undersized for the backlog).
            await UpdateHealthSnapshotAsync(db, cancellationToken).ConfigureAwait(false);

            return pending.Count;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Queries audit_outbox aggregate counters and pushes them into the singleton
    /// <see cref="IAuditOutboxHealthTracker"/>. The ObservableGauges registered in
    /// <c>MeepleAiMetrics.AuditOutbox</c> read from the tracker on collection, so this
    /// keeps the metric values aligned with the on-disk reality on every poll.
    /// </summary>
    private async Task UpdateHealthSnapshotAsync(MeepleAiDbContext db, CancellationToken cancellationToken)
    {
        var pendingCount = await db.AuditOutbox.AsNoTracking()
            .CountAsync(r => r.Status == OutboxStatus.Pending, cancellationToken)
            .ConfigureAwait(false);
        var failedCount = await db.AuditOutbox.AsNoTracking()
            .CountAsync(r => r.Status == OutboxStatus.Failed, cancellationToken)
            .ConfigureAwait(false);

        double oldestPendingAgeSeconds = 0;
        if (pendingCount > 0)
        {
            var oldestCreatedAt = await db.AuditOutbox.AsNoTracking()
                .Where(r => r.Status == OutboxStatus.Pending)
                .MinAsync(r => r.CreatedAt, cancellationToken)
                .ConfigureAwait(false);
            oldestPendingAgeSeconds = (_timeProvider.GetUtcNow() - oldestCreatedAt).TotalSeconds;
        }

        _healthTracker.RecordSnapshot(pendingCount, oldestPendingAgeSeconds, failedCount);
    }

    /// <summary>
    /// Maps an <see cref="AuditOutboxPayload"/> into an <see cref="AuditLogEntity"/>. When
    /// the payload carries multiple snapshots (composite mutation), the FIRST snapshot's
    /// Before/After JSON is used as the canonical pair — sufficient for the current admin
    /// destructive-command surface (single-aggregate mutations). Composite-mutation
    /// support is tracked as a follow-up for S2/S3.
    /// </summary>
    private static AuditLogEntity MaterializeAuditLog(AuditOutboxPayload payload, Guid auditLogId)
    {
        var firstSnapshot = payload.Snapshots.Count > 0 ? payload.Snapshots[0] : null;

        return new AuditLogEntity
        {
            // T4b idempotency: audit_logs.Id mirrors the source outbox row Id so a retried
            // materialization is caught by the pre-INSERT existence check (or, in a future
            // raw-SQL ON CONFLICT variant, by the PK constraint).
            Id = auditLogId,
            UserId = TryParseGuid(payload.UserId),
            Action = payload.Action,
            Resource = payload.Resource,
            ResourceId = payload.ResourceId,
            Result = payload.Result,
            Details = payload.Details,
            IpAddress = payload.IpAddress,
            UserAgent = payload.UserAgent,
            BeforeJson = firstSnapshot?.BeforeJson,
            AfterJson = firstSnapshot?.AfterJson,
            ImpersonatedUserId = payload.ImpersonatedUserId,
            StepUpTokenId = payload.StepUpTokenId,
            // audit_logs.created_at is DateTime (UTC) by convention; the outbox payload Timestamp
            // is a DateTimeOffset to preserve the originating offset across serialization.
            CreatedAt = payload.Timestamp.UtcDateTime,
        };
    }

    private static Guid? TryParseGuid(string? value)
        => Guid.TryParse(value, out var g) ? g : null;
}
