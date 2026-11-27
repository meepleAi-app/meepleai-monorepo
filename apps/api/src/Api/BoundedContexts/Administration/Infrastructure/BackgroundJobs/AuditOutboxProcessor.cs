using System.Text.Json;
using Api.BoundedContexts.Administration.Application;
using Api.BoundedContexts.Administration.Infrastructure.Health;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.BoundedContexts.Administration.Infrastructure.BackgroundJobs;

/// <summary>
/// Background service that drains rows from <c>audit_outbox</c> (Pending) into
/// permanent <c>audit_logs</c> rows. The atomic write path in <c>AuditLoggingBehavior</c>
/// commits Pending outbox rows together with the mutation; this processor decouples the
/// final materialization from the request hot-path.
///
/// Lifecycle (T4): poll every 5 seconds, drain up to 100 rows per batch in a single
/// transaction, saving each row on its own (#5156). On per-row materialization failure, or
/// when the database refuses the row, it is marked Failed with the exception message (visible
/// via the dashboard); successful rows are marked Sent.
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
    ///   - Save the row. When the database refuses it, mark it Failed with the database's
    ///     reason (#5156); a transient error propagates and the strategy replays the batch.
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

        // All stateful work (query → mutate → save → commit) runs INSIDE the execution-strategy
        // delegate. CreateExecutionStrategy is required so the prod NpgsqlRetryingExecutionStrategy
        // can retry the whole batch on transient connection errors. CRITICAL: the Pending query and
        // the idempotency pre-check MUST be re-run on every attempt — EF does NOT reset entity state
        // between retries, so loading tracked rows (carrying their MarkSent/MarkFailed mutations) or
        // the existence set OUTSIDE the delegate would feed stale state into a retried attempt (a row
        // mutated to Sent in a failed attempt could then reach MarkFailed → InvalidOperationException,
        // aborting the whole batch). In Testcontainers (no retry configured) the delegate runs once.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // Start each attempt from clean tracking state so a retry never observes mutations
            // (or added audit_logs entities) left over from a prior failed attempt.
            db.ChangeTracker.Clear();

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

            var tx = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var _ = tx.ConfigureAwait(false);

            foreach (var row in pending)
            {
                var auditLog = Settle(db, row, existingAuditIdSet, now);

                // #5156: each row is saved on its own, as the domain-event outbox does since #5030. The batch used to
                // be saved once, so a row the database refused failed every row of the batch: it stayed Pending at the
                // head of the queue and held them all back again at the next pass.
                try
                {
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // Per-row resilience: a row the database refuses must not fail the batch
                catch (Exception saveEx) when (saveEx is not OperationCanceledException && !IsTransient(saveEx))
#pragma warning restore CA1031
                {
                    await FailTheRefusedRowAsync(db, row, auditLog, saveEx, now, cancellationToken).ConfigureAwait(false);
                }
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            // Refresh the health snapshot AFTER the commit so observers see the post-batch
            // counts (pending may still be > 0 if batchSize was undersized for the backlog).
            await UpdateHealthSnapshotAsync(db, cancellationToken).ConfigureAwait(false);

            return pending.Count;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies to the tracked <paramref name="row"/> the state its payload calls for, and adds its audit_logs row when
    /// there is one to write. Nothing is saved here. Returns the added row, or null.
    /// </summary>
    private AuditLogEntity? Settle(
        MeepleAiDbContext db, AuditOutboxEntity row, HashSet<Guid> existingAuditIds, DateTimeOffset now)
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
                return null;
            }

            if (existingAuditIds.Contains(row.Id))
            {
                // T4b idempotency: the audit_logs row already exists for this id (the
                // outbox.Id == audit_logs.Id contract). Skip the INSERT to avoid a
                // duplicate-key violation, but still transition Pending → Sent so the
                // outbox is drained.
                row.MarkSent(now);
                return null;
            }

            var auditLog = MaterializeAuditLog(payload, row.Id);
            db.AuditLogs.Add(auditLog);
            row.MarkSent(now);
            return auditLog;
        }
#pragma warning disable CA1031 // Per-row resilience: poison-message must not stop the batch
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex,
                "Failed to materialize audit_log from outbox row {OutboxId}; marking row Failed",
                row.Id);
            row.MarkFailed(ex.Message, now);
            return null;
        }
    }

    /// <summary>
    /// #5156 — the save of <paramref name="row"/> failed: the database refused what it was to write. The row is marked
    /// Failed with the database's reason, so that it no longer sits Pending at the head of the queue.
    /// <para>
    /// Inside the batch's transaction EF Core wraps each <c>SaveChanges</c> in a savepoint of its own and rolls back to
    /// it, so the transaction stays usable (#5030). The tracker does not: the refused audit_logs row is still Added, and
    /// the outbox row still carries the Sent it was given. The first is dropped, the second read again.
    /// </para>
    /// </summary>
    private async Task FailTheRefusedRowAsync(
        MeepleAiDbContext db,
        AuditOutboxEntity row,
        AuditLogEntity? auditLog,
        Exception saveEx,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        _logger.LogError(saveEx,
            "The database refused the audit_log of outbox row {OutboxId}; marking row Failed", row.Id);

        if (auditLog is not null)
        {
            db.Entry(auditLog).State = EntityState.Detached;
        }

        await db.Entry(row).ReloadAsync(cancellationToken).ConfigureAwait(false);

        // Read again, a row that is no longer Pending was settled by someone else: it is left as it is.
        if (row.Status == OutboxStatus.Pending)
        {
            row.MarkFailed(saveEx.GetBaseException().Message, now);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A transient database error (deadlock, serialization failure, lost connection) is not a row the database refuses:
    /// it propagates, and the execution strategy replays the batch.
    /// </summary>
    private static bool IsTransient(Exception ex) =>
        ex.GetBaseException() is NpgsqlException { IsTransient: true };

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
        }.FitToColumns(); // #5156: the payload keeps the whole values; the columns take what fits.
    }

    private static Guid? TryParseGuid(string? value)
        => Guid.TryParse(value, out var g) ? g : null;
}
