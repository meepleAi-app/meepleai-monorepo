using System.Text.Json;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.Observability;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Npgsql;

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
/// <para>Lifecycle (T4 + T5): poll every
/// <see cref="DomainEventOutboxOptions.PollIntervalSeconds"/>, drain up to
/// <see cref="DomainEventOutboxOptions.BatchSize"/> rows per batch in a single
/// transaction. On per-row dispatch failure, the row is re-armed Pending with an
/// exponentially-backed-off <c>NextAttemptAt</c> (T5) until the
/// <see cref="DomainEventOutboxOptions.MaxAttempts"/> budget is exhausted, after
/// which it transitions to terminal Failed for ops triage.</para>
///
/// <para>#5030 — each row is saved on its own inside the batch transaction, together with what
/// its handlers left in the change tracker. A residue the save cannot write fails ITS row (residue
/// dropped, row retried), not the batch. Before, the batch saved once at the end, so that residue
/// rolled back every row, counted no attempt, and came back on every poll. A batch that still
/// fails as a whole is counted in <c>meepleai.domain_event_outbox.batch_failed.total</c> and
/// refreshes the health snapshot anyway, so the stale-Pending gauge keeps ageing.</para>
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
    ///     <item>On failure with budget remaining (T5):
    ///           <see cref="DomainEventOutboxEntity.MarkRetry"/> with
    ///           <see cref="ComputeBackoff"/> exponential delay.</item>
    ///     <item>On failure that exhausts the budget (T5):
    ///           <see cref="DomainEventOutboxEntity.MarkFailed"/> — terminal, ops-visible.</item>
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
        // The batch context's collector: events a repository queued for changes that are then dropped
        // must be dropped with them (#5030). Optional because a test host may build the context
        // without one.
        var collector = scope.ServiceProvider.GetService<IDomainEventCollector>();

        // All stateful work runs INSIDE the execution-strategy delegate so the prod
        // NpgsqlRetryingExecutionStrategy can replay the whole batch on transient errors.
        // CRITICAL (same rationale as AuditOutboxProcessor): the Pending query MUST be
        // re-run on every attempt — EF does NOT reset entity state between retries, so
        // loading tracked rows (carrying their MarkSent/MarkFailed mutations) outside the
        // delegate would feed stale state into a retried attempt.
        //
        // Counter increments are ACCUMULATED locally during the foreach and emitted ONLY
        // after tx.CommitAsync succeeds — so a strategy retry that replays the delegate
        // doesn't double-count metrics. Empty/null pendingResult means "no batch processed"
        // (used in the empty-batch path).
        var strategy = db.Database.CreateExecutionStrategy();
        BatchOutcome batchResult;
        try
        {
            batchResult = await strategy.ExecuteAsync(
                () => DrainBatchAsync(db, collector, mediator, typeResolver, batchSize, cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // #5030: a batch that fails as a whole used to leave only a LogError per poll, and the
            // health snapshot was refreshed only after a successful batch, so the oldest-Pending age
            // the stale alert reads stayed frozen at its last value.
            // When the execution strategy gives up, the exception it throws is a wrapper: the label
            // names what actually failed.
            var cause = ex is RetryLimitExceededException { InnerException: { } inner } ? inner : ex;
            MeepleAiMetrics.DomainEventOutboxBatchFailed.Add(
                1, new KeyValuePair<string, object?>("exception_type", cause.GetType().Name));
            await TryUpdateHealthSnapshotAfterFailureAsync(db, cancellationToken).ConfigureAwait(false);
            throw;
        }

        // Emit counters OUTSIDE the strategy delegate so a strategy retry (which already
        // re-ran the foreach and reset all state) cannot double-count metrics. Health
        // snapshot is refreshed against the post-commit DB state, also outside the strategy
        // so a transient COUNT failure here doesn't replay the (already-committed) batch.
        foreach (var eventType in batchResult.DispatchedEventTypes)
        {
            MeepleAiMetrics.DomainEventOutboxDispatched.Add(
                1, new KeyValuePair<string, object?>("event_type", eventType));
        }
        foreach (var eventType in batchResult.RetriedEventTypes)
        {
            MeepleAiMetrics.DomainEventOutboxRetried.Add(
                1, new KeyValuePair<string, object?>("event_type", eventType));
        }
        foreach (var eventType in batchResult.FailedTerminalEventTypes)
        {
            MeepleAiMetrics.DomainEventOutboxFailedTerminal.Add(
                1, new KeyValuePair<string, object?>("event_type", eventType));
        }
        foreach (var (eventType, seconds) in batchResult.DispatchLatencies)
        {
            MeepleAiMetrics.DomainEventOutboxDispatchLatencySeconds.Record(
                seconds, new KeyValuePair<string, object?>("event_type", eventType));
        }

        await UpdateHealthSnapshotAsync(db, cancellationToken).ConfigureAwait(false);

        return batchResult.EmptyBatch ? 0 : batchResult.Pending;
    }

    /// <summary>
    /// One attempt at a batch: the body the execution strategy replays on a transient error. See
    /// <see cref="RunOnceAsync"/> for why the Pending query must run again on every attempt.
    /// </summary>
    private async Task<BatchOutcome> DrainBatchAsync(
        MeepleAiDbContext db,
        IDomainEventCollector? collector,
        IMediator mediator,
        IDomainEventTypeResolver typeResolver,
        int batchSize,
        CancellationToken cancellationToken)
    {
        // A replayed attempt starts clean: the previous one's transaction rolled back, so neither its
        // tracked changes nor the events queued for them may reach this attempt's saves.
        db.ChangeTracker.Clear();
        collector?.Clear();
        var now = _timeProvider.GetUtcNow();

        // FIFO drain — partial index on (status=Pending, next_attempt_at) drives the plan.
        // Order by readiness then by enqueue order so a retry-scheduled row coming due now
        // does NOT jump newer Pending rows.
        var pending = await db.DomainEventOutbox
            .AsTracking()
            .Where(r => r.Status == DomainEventOutboxStatus.Pending
                     && (r.NextAttemptAt == null || r.NextAttemptAt <= now))
            .OrderBy(r => r.NextAttemptAt ?? r.EnqueuedAt)
            .ThenBy(r => r.EnqueuedAt)
            .ThenBy(r => r.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pending.Count == 0)
        {
            // Signal the empty case to the post-commit metric emission; the caller will
            // still refresh the health snapshot once outside the strategy delegate.
            return new BatchOutcome(EmptyBatch: true, Pending: 0, Dispatched: 0, Retried: 0, FailedTerminal: 0,
                DispatchedEventTypes: System.Array.Empty<string>(),
                RetriedEventTypes: System.Array.Empty<string>(),
                FailedTerminalEventTypes: System.Array.Empty<string>(),
                DispatchLatencies: System.Array.Empty<(string, double)>());
        }

        var dispatchedEventTypes = new List<string>(pending.Count);
        var retriedEventTypes = new List<string>(pending.Count);
        var failedTerminalEventTypes = new List<string>(pending.Count);
        var dispatchLatencies = new List<(string EventType, double Seconds)>(pending.Count);

        var tx = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var _ = tx.ConfigureAwait(false);

        for (var i = 0; i < pending.Count; i++)
        {
            var row = pending[i];

            var outcome = await DispatchRowAsync(row, mediator, typeResolver, now, cancellationToken)
                .ConfigureAwait(false);

            // #5030: the row is saved on its own, with what its handlers left in the tracker. Until
            // this change the batch saved once, at the end, and a residue that save could not write
            // failed every row of the batch instead of this one.
            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Per-row resilience: an unsavable residue must not fail the batch
            catch (Exception saveEx) when (saveEx is not OperationCanceledException && !IsTransient(saveEx))
#pragma warning restore CA1031
            {
                outcome = await DropUnsavableResidueAsync(db, collector, pending, i, outcome, saveEx, now, cancellationToken)
                    .ConfigureAwait(false);
            }

            switch (outcome.Kind)
            {
                case RowOutcomeKind.Sent:
                    dispatchedEventTypes.Add(row.EventType);
                    // F-B follow-up: record end-to-end dispatch latency (EnqueuedAt → MarkSent.now).
                    // Captured inside the strategy delegate but emitted outside (same as the
                    // counters) so a strategy retry doesn't double-record samples.
                    dispatchLatencies.Add((row.EventType, (now - row.EnqueuedAt).TotalSeconds));
                    break;
                case RowOutcomeKind.Retried:
                    retriedEventTypes.Add(row.EventType);
                    break;
                case RowOutcomeKind.FailedTerminal:
                    failedTerminalEventTypes.Add(row.EventType);
                    break;
            }
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new BatchOutcome(
            EmptyBatch: false,
            Pending: pending.Count,
            Dispatched: dispatchedEventTypes.Count,
            Retried: retriedEventTypes.Count,
            FailedTerminal: failedTerminalEventTypes.Count,
            DispatchedEventTypes: dispatchedEventTypes,
            RetriedEventTypes: retriedEventTypes,
            FailedTerminalEventTypes: failedTerminalEventTypes,
            DispatchLatencies: dispatchLatencies);
    }

    /// <summary>
    /// Aggregated outcome of a single batch — separates the "what to commit" (entity state
    /// transitions, owned by the strategy delegate) from the "what to publish" (Prometheus
    /// counters, MUST be emitted exactly once even if the strategy retries the delegate).
    /// </summary>
    private sealed record BatchOutcome(
        bool EmptyBatch,
        int Pending,
        int Dispatched,
        int Retried,
        int FailedTerminal,
        IReadOnlyList<string> DispatchedEventTypes,
        IReadOnlyList<string> RetriedEventTypes,
        IReadOnlyList<string> FailedTerminalEventTypes,
        IReadOnlyList<(string EventType, double Seconds)> DispatchLatencies);

    private enum RowOutcomeKind
    {
        Sent,
        Retried,
        FailedTerminal,
        /// <summary>The row was no longer Pending when reloaded: another worker settled it.</summary>
        Skipped,
    }

    /// <param name="Kind">What happened to the row in this batch.</param>
    /// <param name="HandlerError">The handler's exception message when dispatch threw, else null.</param>
    private readonly record struct RowOutcome(RowOutcomeKind Kind, string? HandlerError);

    /// <summary>
    /// Resolves, deserialises and publishes one row, and applies its new state to the tracked
    /// entity. Nothing is saved here: the caller saves the row together with what its handlers
    /// left in the tracker.
    /// </summary>
    private async Task<RowOutcome> DispatchRowAsync(
        DomainEventOutboxEntity row,
        IMediator mediator,
        IDomainEventTypeResolver typeResolver,
        DateTimeOffset now,
        CancellationToken cancellationToken)
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
                return new RowOutcome(RowOutcomeKind.FailedTerminal, null);
            }

            var evt = (IDomainEvent?)JsonSerializer.Deserialize(
                row.PayloadJson, clrType, DomainEventJsonOptions.Default);
            if (evt is null)
            {
                row.MarkFailed("Deserialized payload was null", now);
                _logger.LogError(
                    "Domain event {EventId} ({EventType}) deserialised to null; marked Failed",
                    row.Id, row.EventType);
                return new RowOutcome(RowOutcomeKind.FailedTerminal, null);
            }

            if (evt.EventId != row.Id)
            {
                // #4841: the row's Id IS the event's EventId (Enqueue), so a mismatch means this
                // event type does not keep it across the JSON round trip, and any dedupe keyed on
                // EventId (LastProcessedEventId, SourceEventId under a unique index) will not
                // recognise a second delivery. Not fatal: the event is still delivered. The
                // architecture test over every IDomainEvent measures EventId and OccurredAt for the
                // events it can construct; this catches a type it cannot, for EventId only (OccurredAt
                // is not compared with the row's: the column is a Postgres timestamptz, which keeps
                // microseconds, while the event keeps 100 ns ticks).
                _logger.LogWarning(
                    "Domain event {EventId} ({EventType}) was rebuilt with EventId {RebuiltEventId}: its type does not keep the EventId across the outbox, dedupe keyed on it will not work for this event",
                    row.Id, row.EventType, evt.EventId);
            }

            await mediator.Publish(evt, cancellationToken).ConfigureAwait(false);
            row.MarkSent(now);
            return new RowOutcome(RowOutcomeKind.Sent, null);
        }
#pragma warning disable CA1031 // Per-row resilience: poison-message must not stop the batch
        catch (Exception ex)
#pragma warning restore CA1031
        {
            var kind = ScheduleRetryOrFail(row, ex.Message, now, ex);
            return new RowOutcome(kind, ex.Message);
        }
    }

    /// <summary>
    /// T5: budgeted retry. <see cref="DomainEventOutboxEntity.Attempts"/> still reflects the PRIOR
    /// count on entry — both MarkRetry and MarkFailed increment it. The decision branch checks the
    /// post-increment value.
    /// </summary>
    private RowOutcomeKind ScheduleRetryOrFail(DomainEventOutboxEntity row, string error, DateTimeOffset now, Exception ex)
    {
        var nextAttemptCount = row.Attempts + 1;
        if (nextAttemptCount >= _options.MaxAttempts)
        {
            row.MarkFailed(error, now);
            _logger.LogError(ex,
                "Domain event {EventId} ({EventType}) FAILED terminally after {Attempts} attempts",
                row.Id, row.EventType, nextAttemptCount);
            return RowOutcomeKind.FailedTerminal;
        }

        var backoff = ComputeBackoff(nextAttemptCount);
        row.MarkRetry(error, now + backoff, now);
        _logger.LogWarning(ex,
            "Domain event {EventId} ({EventType}) dispatch failed; scheduling retry #{Attempt} in {BackoffSeconds:F2}s",
            row.Id, row.EventType, nextAttemptCount, backoff.TotalSeconds);
        return RowOutcomeKind.Retried;
    }

    /// <summary>
    /// #5030 — the save after row <paramref name="index"/> failed: what its handlers left in the
    /// tracker cannot be written. Drops the tracker and records the failure on a fresh copy of the
    /// row; clearing the tracker detaches the rows still to process, so they are attached again.
    /// <para>
    /// What a handler of this row already SAVED stays, exactly as when a handler throws: handlers
    /// with an external side effect rely on that to keep their "already sent" marker across a
    /// sibling's failure, and a retry runs every handler of the event again. Only the unsaved
    /// residue is dropped — with the domain events a repository queued for it, which the context
    /// keeps after a failed save and would otherwise enqueue as outbox rows on the next one. The
    /// failed save does not poison the transaction: inside a user transaction EF Core wraps
    /// <c>SaveChanges</c> in a savepoint of its own and rolls back to it.
    /// </para>
    /// </summary>
    private async Task<RowOutcome> DropUnsavableResidueAsync(
        MeepleAiDbContext db,
        IDomainEventCollector? collector,
        List<DomainEventOutboxEntity> pending,
        int index,
        RowOutcome outcome,
        Exception saveEx,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var row = pending[index];
        db.ChangeTracker.Clear();
        // Every earlier row was saved, and a successful save empties the collector: what is left was
        // queued by this row's handlers, for changes that were just discarded.
        collector?.Clear();

        var saveError = saveEx.GetBaseException().Message;
        _logger.LogWarning(saveEx,
            "Domain event {EventId} ({EventType}): the changes left by its handlers could not be saved and were discarded",
            row.Id, row.EventType);

        var reloaded = await db.DomainEventOutbox
            .AsTracking()
            .SingleAsync(r => r.Id == row.Id, cancellationToken)
            .ConfigureAwait(false);

        var result = new RowOutcome(RowOutcomeKind.Skipped, outcome.HandlerError);
        if (reloaded.Status == DomainEventOutboxStatus.Pending)
        {
            var error = outcome.HandlerError is null
                ? saveError
                : $"{outcome.HandlerError} (its unsaved changes were discarded: {saveError})";
            result = result with { Kind = ScheduleRetryOrFail(reloaded, error, now, saveEx) };
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        for (var next = index + 1; next < pending.Count; next++)
        {
            db.DomainEventOutbox.Attach(pending[next]);
        }

        return result;
    }

    /// <summary>
    /// A transient database error (deadlock, serialization failure, lost connection) is not a residue
    /// the handlers left: it propagates, and the execution strategy replays the batch as before #5030.
    /// </summary>
    private static bool IsTransient(Exception ex) =>
        ex.GetBaseException() is NpgsqlException { IsTransient: true };

    /// <summary>
    /// #5030 — refreshes the health snapshot after a batch failed as a whole. Best-effort: when the
    /// failure is the database itself, this fails too, and the batch's exception is the one that
    /// matters.
    /// </summary>
    private async Task TryUpdateHealthSnapshotAfterFailureAsync(MeepleAiDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            db.ChangeTracker.Clear();
            await UpdateHealthSnapshotAsync(db, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Best-effort: the batch's own exception is rethrown by the caller
        catch (Exception snapshotEx)
#pragma warning restore CA1031
        {
            _logger.LogWarning(snapshotEx, "DomainEventOutboxProcessor could not refresh the health snapshot after a failed batch");
        }
    }

    /// <summary>
    /// T5 exponential backoff with jitter. The unjittered delay grows as
    /// <c>InitialBackoffMs * 2^(attempt-1)</c> seconds; a symmetric ±20% jitter is
    /// applied to avoid thundering-herd retries when many rows fail simultaneously
    /// (e.g. transient consumer outage). The
    /// <see cref="DomainEventOutboxOptions.MaxBackoffSeconds"/> cap is applied AFTER
    /// jitter so the final delay is a STRICT ceiling (no leak through the jitter band).
    /// </summary>
    /// <param name="attempt">1-based attempt counter — the value Attempts will hold
    /// AFTER the impending <see cref="DomainEventOutboxEntity.MarkRetry"/> call.</param>
    private TimeSpan ComputeBackoff(int attempt)
    {
        var unboundedSeconds = (_options.InitialBackoffMs / 1000.0) * Math.Pow(2, attempt - 1);
        var jitterFactor = 1.0 + ((Random.Shared.NextDouble() * 0.4) - 0.2); // [0.8, 1.2]
        var jittered = unboundedSeconds * jitterFactor;
        var seconds = Math.Min(jittered, _options.MaxBackoffSeconds);
        return TimeSpan.FromSeconds(seconds);
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
