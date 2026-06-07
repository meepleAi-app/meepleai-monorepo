using System.Text.Json;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.SharedKernel.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.BackgroundJobs;

/// <summary>
/// Background service that drains rows from <c>domain_event_outbox</c> (Pending → Sent)
/// by invoking <c>MediatR.Publish</c> with the deserialised <see cref="IDomainEvent"/>.
///
/// <para>Issue #1535 — replaces the inline MediatR.Publish previously done inside
/// <c>MeepleAiDbContext.SaveChangesAsync</c>. The atomic write path in T3 (DbContext
/// routing) commits Pending outbox rows together with the aggregate mutation; this
/// processor decouples the final dispatch from the request hot-path, breaking the race
/// that motivated #1535 (an outer audit transaction can no longer roll back AFTER
/// side-effects have escaped).</para>
///
/// <para>Lifecycle (T4): poll every
/// <see cref="DomainEventOutboxOptions.PollIntervalSeconds"/>, drain up to
/// <see cref="DomainEventOutboxOptions.BatchSize"/> rows per batch in a single
/// transaction. Per-row dispatch failure currently transitions the row directly to
/// Failed; T5 will introduce exponential-backoff retry semantics + the
/// <see cref="DomainEventOutboxOptions.MaxAttempts"/> dead-letter threshold.</para>
///
/// <para>Mirror of <c>AuditOutboxProcessor</c> (PR #1532) — kept separate because the
/// two outboxes will diverge in their failure semantics (audit is "best-effort write";
/// domain events are "guaranteed at-least-once dispatch").</para>
/// </summary>
internal sealed class DomainEventOutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DomainEventOutboxProcessor> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly DomainEventOutboxOptions _options;
    private readonly IDomainEventOutboxHealthTracker _healthTracker;

    public DomainEventOutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<DomainEventOutboxProcessor> logger,
        IOptions<DomainEventOutboxOptions> options,
        IDomainEventOutboxHealthTracker healthTracker,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
        _healthTracker = healthTracker;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(_options.BatchSize, stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Resilience: a failing batch must not stop the host
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "DomainEventOutboxProcessor batch failed; will retry on next poll");
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown — fall through to loop condition
            }
        }
    }

    /// <summary>
    /// Drains a single batch of ready Pending outbox rows. For each row:
    ///   <list type="bullet">
    ///     <item>Resolve the CLR type via <see cref="IDomainEventTypeResolver"/>.</item>
    ///     <item>Deserialize <c>PayloadJson</c> into the resolved type.</item>
    ///     <item>Invoke <c>MediatR.Publish</c> with the resulting <see cref="IDomainEvent"/>.</item>
    ///     <item>On success: <see cref="DomainEventOutboxEntity.MarkSent"/>.</item>
    ///     <item>On failure (T4 scope): <see cref="DomainEventOutboxEntity.MarkFailed"/>.
    ///           T5 will swap this branch for retry-with-backoff semantics.</item>
    ///   </list>
    /// All state transitions commit in a single transaction. Returns the number of rows
    /// considered in this batch. Exposed publicly for deterministic integration testing —
    /// production drives this from <see cref="ExecuteAsync"/>.
    /// </summary>
    public async Task<int> RunOnceAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be positive.");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var typeResolver = scope.ServiceProvider.GetRequiredService<IDomainEventTypeResolver>();

        // All stateful work runs INSIDE the execution-strategy delegate so the prod
        // NpgsqlRetryingExecutionStrategy can replay the whole batch on transient errors.
        // CRITICAL (same rationale as AuditOutboxProcessor): the Pending query MUST be
        // re-run on every attempt — EF does NOT reset entity state between retries, so
        // loading tracked rows (carrying their MarkSent/MarkFailed mutations) outside the
        // delegate would feed stale state into a retried attempt.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var now = _timeProvider.GetUtcNow();

            // FIFO drain — partial index on (status=Pending, next_attempt_at) drives the plan.
            // Order by readiness then by enqueue order so a retry-scheduled row coming due now
            // does NOT jump newer Pending rows.
            var pending = await db.DomainEventOutbox
                .AsTracking()
                .Where(r => r.Status == DomainEventOutboxStatus.Pending
                         && (r.NextAttemptAt == null || r.NextAttemptAt <= now))
                .OrderBy(r => r.EnqueuedAt)
                .ThenBy(r => r.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (pending.Count == 0)
            {
                // Refresh health snapshot on empty batches too — gauges must read "quiet
                // system" values when there really is nothing to dispatch.
                await UpdateHealthSnapshotAsync(db, cancellationToken).ConfigureAwait(false);
                return 0;
            }

            var tx = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var _ = tx.ConfigureAwait(false);

            foreach (var row in pending)
            {
                try
                {
                    var clrType = typeResolver.Resolve(row.EventType);
                    if (clrType is null)
                    {
                        // Poison-message: alias does not map to a CLR type (e.g. event class
                        // deleted after the row was written). Mark Failed so ops can replay
                        // or discard via the dashboard.
                        row.MarkFailed($"Unknown event type alias: {row.EventType}", now);
                        _logger.LogError(
                            "Domain event {EventId} has unknown alias {EventType}; marked Failed",
                            row.Id, row.EventType);
                        continue;
                    }

                    var evt = (IDomainEvent?)JsonSerializer.Deserialize(
                        row.PayloadJson, clrType, DomainEventJsonOptions.Default);
                    if (evt is null)
                    {
                        row.MarkFailed("Deserialized payload was null", now);
                        _logger.LogError(
                            "Domain event {EventId} ({EventType}) deserialised to null; marked Failed",
                            row.Id, row.EventType);
                        continue;
                    }

                    await mediator.Publish(evt, cancellationToken).ConfigureAwait(false);
                    row.MarkSent(now);
                }
#pragma warning disable CA1031 // Per-row resilience: poison-message must not stop the batch
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    // T4 scope: any per-row failure terminates the row. T5 will swap this for
                    // retry-with-exponential-backoff up to MaxAttempts, then MarkFailed.
                    row.MarkFailed(ex.Message, now);
                    _logger.LogError(ex,
                        "Failed to dispatch domain event {EventId} ({EventType}); marked Failed",
                        row.Id, row.EventType);
                }
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            // Snapshot AFTER the commit so observers see post-batch counts.
            await UpdateHealthSnapshotAsync(db, cancellationToken).ConfigureAwait(false);

            return pending.Count;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Queries the outbox aggregate counters and pushes them into the singleton
    /// <see cref="IDomainEventOutboxHealthTracker"/>. The ObservableGauges registered in
    /// <c>MeepleAiMetrics.DomainEventOutbox</c> (T6) read from the tracker on metric
    /// collection — keeping the snapshot fresh after every poll bounds the gauge lag to
    /// one poll interval.
    /// </summary>
    private async Task UpdateHealthSnapshotAsync(MeepleAiDbContext db, CancellationToken cancellationToken)
    {
        var pendingCount = await db.DomainEventOutbox.AsNoTracking()
            .CountAsync(r => r.Status == DomainEventOutboxStatus.Pending, cancellationToken)
            .ConfigureAwait(false);
        var failedCount = await db.DomainEventOutbox.AsNoTracking()
            .CountAsync(r => r.Status == DomainEventOutboxStatus.Failed, cancellationToken)
            .ConfigureAwait(false);

        double oldestPendingAgeSeconds = 0;
        if (pendingCount > 0)
        {
            var oldestEnqueuedAt = await db.DomainEventOutbox.AsNoTracking()
                .Where(r => r.Status == DomainEventOutboxStatus.Pending)
                .MinAsync(r => r.EnqueuedAt, cancellationToken)
                .ConfigureAwait(false);
            oldestPendingAgeSeconds = (_timeProvider.GetUtcNow() - oldestEnqueuedAt).TotalSeconds;
        }

        _healthTracker.RecordSnapshot(pendingCount, oldestPendingAgeSeconds, failedCount);
    }
}
