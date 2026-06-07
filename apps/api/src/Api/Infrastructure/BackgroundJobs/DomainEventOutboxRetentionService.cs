using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.BackgroundJobs;

/// <summary>
/// Background service that purges <c>Sent</c> <c>domain_event_outbox</c> rows older than
/// <see cref="DomainEventOutboxOptions.SentRetentionDays"/> (default 30 days). Implements
/// the TTL contract documented in <c>DomainEventOutboxEntityConfiguration.cs</c> and
/// resolves follow-up issue #1966.
///
/// <para><b>Why we need this</b>: at the spec'd arrival rate (~100/s sustained, see
/// <c>audits/2026-06-06-issue-1535-event-outbox-kickoff.md</c>) the Sent partition would
/// grow ~260M rows over 30 days. The <c>ix_domain_event_outbox_sent_dispatched_at</c>
/// partial index keeps the dashboard hot-paths bounded, but vacuum overhead, replication
/// lag, pg_dump size, and RTO/RPO all degrade linearly without a cleanup job.</para>
///
/// <para><b>What it does NOT do</b>:</para>
/// <list type="bullet">
///   <item>Does NOT touch <c>Failed</c> rows. Operator intervention is the only path
///         that removes Failed rows — either via <c>POST /retry</c> (re-arm to Pending)
///         or by accepting the failure and manually deleting after triage.</item>
///   <item>Does NOT touch <c>Pending</c> rows. The processor owns Pending lifecycle.</item>
///   <item>Does NOT archive purged data anywhere — operators who need long-term audit of
///         dispatched events should rely on <c>domain_event_logs</c> (Issue #661) which
///         is the durable record. The outbox is operational state, not an audit trail.</item>
/// </list>
///
/// <para><b>Lifecycle</b>: <see cref="ExecuteAsync"/> wakes once per
/// <see cref="DomainEventOutboxOptions.RetentionIntervalHours"/> (default 1h), calls
/// <see cref="RunOnceAsync"/>, then sleeps. <see cref="RunOnceAsync"/> loops chunks of
/// <see cref="DomainEventOutboxOptions.RetentionBatchSize"/> rows until the eligible set
/// is empty, so a single tick fully drains. Chunked DELETE avoids long-running
/// transactions that would block writes from the dispatch hot path.</para>
/// </summary>
internal sealed class DomainEventOutboxRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DomainEventOutboxRetentionService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly DomainEventOutboxOptions _options;

    public DomainEventOutboxRetentionService(
        IServiceScopeFactory scopeFactory,
        ILogger<DomainEventOutboxRetentionService> logger,
        IOptions<DomainEventOutboxOptions> options,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, _options.RetentionIntervalHours));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var purged = await RunOnceAsync(stoppingToken).ConfigureAwait(false);
                if (purged > 0)
                {
                    _logger.LogInformation(
                        "Domain event outbox retention purged {Purged} rows older than {Days} days",
                        purged, _options.SentRetentionDays);
                }
            }
#pragma warning disable CA1031 // Resilience: a failing purge must not stop the host
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex,
                    "DomainEventOutboxRetentionService tick failed; will retry on next poll");
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown — fall through to loop condition
            }
        }
    }

    /// <summary>
    /// Drains the eligible Sent-old set in chunks of
    /// <see cref="DomainEventOutboxOptions.RetentionBatchSize"/> rows until the next
    /// chunk returns zero. Returns the total number of rows purged across all chunks.
    /// Exposed publicly for deterministic integration testing — production drives this
    /// from <see cref="ExecuteAsync"/>.
    /// </summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var cutoff = now.AddDays(-_options.SentRetentionDays);
        var chunkSize = Math.Max(1, _options.RetentionBatchSize);
        var total = 0;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        while (!cancellationToken.IsCancellationRequested)
        {
            // Each iteration: SELECT the next chunk of eligible ids, batch-delete them,
            // record the per-event_type counts on the Prometheus counter. The strategy
            // runs PER chunk so a transient error retries only the current chunk, not
            // the entire drain — important when the eligible set is millions of rows.
            var strategy = db.Database.CreateExecutionStrategy();
            var purgedThisChunk = await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();

                // Read the chunk WITH the event_type so we can tag the metric counter
                // without a second query. The partial index ix_domain_event_outbox_sent_dispatched_at
                // (shipped F15) covers this predicate efficiently.
                var chunk = await db.DomainEventOutbox
                    .AsNoTracking()
                    .Where(r => r.Status == DomainEventOutboxStatus.Sent
                             && r.DispatchedAt != null
                             && r.DispatchedAt < cutoff)
                    .OrderBy(r => r.DispatchedAt)
                    .Take(chunkSize)
                    .Select(r => new { r.Id, r.EventType })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (chunk.Count == 0)
                {
                    return 0;
                }

                var idsToDelete = chunk.Select(c => c.Id).ToList();
                await db.DomainEventOutbox
                    .Where(r => idsToDelete.Contains(r.Id))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Emit the counter outside the strategy delegate would be cleaner BUT
                // chunked counts are not double-emitted on retry: ExecuteDeleteAsync is
                // a single SQL statement that either committed or didn't. If the
                // strategy retries this delegate, the SELECT in the retry returns 0 (the
                // prior DELETE already committed) and we exit the chunk loop. So
                // in-delegate emission is safe here.
                foreach (var entry in chunk)
                {
                    MeepleAiMetrics.DomainEventOutboxPurged.Add(
                        1,
                        new KeyValuePair<string, object?>("event_type", entry.EventType));
                }

                return chunk.Count;
            }).ConfigureAwait(false);

            if (purgedThisChunk == 0)
            {
                break;
            }

            total += purgedThisChunk;

            // If the chunk was smaller than the batch ceiling we definitely drained the
            // eligible set. Avoid the extra round-trip to confirm "0 in next chunk".
            if (purgedThisChunk < chunkSize)
            {
                break;
            }
        }

        return total;
    }
}
