using Api.Infrastructure;
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
#pragma warning disable S4487 // Field is consumed by RunOnceAsync once step 3 lands; kept now to lock the ctor shape.
    private readonly IServiceScopeFactory _scopeFactory;
#pragma warning restore S4487
    private readonly ILogger<AuditOutboxProcessor> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
    private const int DefaultBatchSize = 100;

    public AuditOutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<AuditOutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
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
    /// Drains a single batch of Pending outbox rows: materializes each into an AuditLogEntity
    /// and marks the source row Sent. Returns the count of rows processed (Sent + Failed in
    /// this batch). Exposed for integration testing — the production path calls this from
    /// <see cref="ExecuteAsync"/>.
    /// </summary>
    public Task<int> RunOnceAsync(int batchSize, CancellationToken cancellationToken)
    {
        // T4 step 1 (TDD-first): the test that asserts drain semantics MUST fail until step 3
        // implements the batch loop. NotSupportedException (per MA0025) surfaces the missing
        // implementation clearly in the test runner output instead of returning a misleading 0.
        throw new NotSupportedException(
            "AuditOutboxProcessor.RunOnceAsync — T4 step 3 implementation pending.");
    }
}
