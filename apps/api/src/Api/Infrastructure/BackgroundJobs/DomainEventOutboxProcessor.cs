using System.Text.Json;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.Observability;
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
/// <para>Lifecycle (T4 + T5): poll every
/// <see cref="DomainEventOutboxOptions.PollIntervalSeconds"/>, drain up to
/// <see cref="DomainEventOutboxOptions.BatchSize"/> rows per batch in a single
/// transaction. On per-row dispatch failure, the row is re-armed Pending with an
/// exponentially-backed-off <c>NextAttemptAt</c> (T5) until the
/// <see cref="DomainEventOutboxOptions.MaxAttempts"/> budget is exhausted, after
/// which it transitions to terminal Failed for ops triage.</para>
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
        var batchResult = await strategy.ExecuteAsync(async () =>
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
                    FailedTerminalEventTypes: System.Array.Empty<string>());
            }

            var dispatchedEventTypes = new List<string>(pending.Count);
            var retriedEventTypes = new List<string>(pending.Count);
            var failedTerminalEventTypes = new List<string>(pending.Count);

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
                        failedTerminalEventTypes.Add(row.EventType);
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
                        failedTerminalEventTypes.Add(row.EventType);
                        _logger.LogError(
                            "Domain event {EventId} ({EventType}) deserialised to null; marked Failed",
                            row.Id, row.EventType);
                        continue;
                    }

                    await mediator.Publish(evt, cancellationToken).ConfigureAwait(false);
                    row.MarkSent(now);
                    dispatchedEventTypes.Add(row.EventType);
                }
#pragma warning disable CA1031 // Per-row resilience: poison-message must not stop the batch
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    // T5: budgeted retry. Attempts on the row still reflects the PRIOR
                    // count when we enter the catch block — both MarkRetry and MarkFailed
                    // increment it. The decision branch checks the post-increment value.
                    var nextAttemptCount = row.Attempts + 1;
                    if (nextAttemptCount >= _options.MaxAttempts)
                    {
                        row.MarkFailed(ex.Message, now);
                        failedTerminalEventTypes.Add(row.EventType);
                        _logger.LogError(ex,
                            "Domain event {EventId} ({EventType}) FAILED terminally after {Attempts} attempts",
                            row.Id, row.EventType, nextAttemptCount);
                    }
                    else
                    {
                        var backoff = ComputeBackoff(nextAttemptCount);
                        row.MarkRetry(ex.Message, now + backoff, now);
                        retriedEventTypes.Add(row.EventType);
                        _logger.LogWarning(ex,
                            "Domain event {EventId} ({EventType}) dispatch failed; scheduling retry #{Attempt} in {BackoffSeconds:F2}s",
                            row.Id, row.EventType, nextAttemptCount, backoff.TotalSeconds);
                    }
                }
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new BatchOutcome(
                EmptyBatch: false,
                Pending: pending.Count,
                Dispatched: dispatchedEventTypes.Count,
                Retried: retriedEventTypes.Count,
                FailedTerminal: failedTerminalEventTypes.Count,
                DispatchedEventTypes: dispatchedEventTypes,
                RetriedEventTypes: retriedEventTypes,
                FailedTerminalEventTypes: failedTerminalEventTypes);
        }).ConfigureAwait(false);

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

        await UpdateHealthSnapshotAsync(db, cancellationToken).ConfigureAwait(false);

        return batchResult.EmptyBatch ? 0 : batchResult.Pending;
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
        IReadOnlyList<string> FailedTerminalEventTypes);

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
