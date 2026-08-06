using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// One-shot startup recovery for <c>processing_jobs</c> rows orphaned by a restart (Issue #3588).
///
/// The queue worker (<c>PdfProcessingQuartzJob</c>) runs in-process: when the container dies
/// mid-pipeline, its row stays <c>Processing</c> forever. Nothing reclaimed it —
/// <c>StalePdfRecoveryService</c> only resets <c>pdf_documents</c>, and the monitor's auto-degrade
/// path was itself broken. Because the worker skips its whole cycle while
/// <c>Processing &gt;= MaxConcurrentWorkers</c>, a couple of orphans permanently wedge the queue:
/// on staging a single one blocked ingest for 96 minutes.
///
/// Requeue (not degrade to Failed) is the right reaction here: an orphan did not fail, it lost its
/// worker. This mirrors the manual recovery applied on staging.
///
/// <para><b>Assumption — single API instance.</b> At boot no in-process worker can have survived,
/// so every <c>Processing</c> row is by definition orphaned. Running several API replicas against
/// one database would break that: a booting replica would requeue jobs another replica is actively
/// working. That assumption already underpins <c>StalePdfRecoveryService</c>, which reprocesses
/// every stale PDF at startup. Scaling out means gating both on a worker/lease identity.</para>
/// </summary>
internal sealed class OrphanedProcessingJobRecoveryService : BackgroundService
{
    /// <summary>
    /// Short warm-up so migrations and the DB connection are ready. Deliberately shorter than
    /// <c>StalePdfRecoveryService</c>'s 30s: orphans must be requeued before that service starts
    /// reprocessing PDFs, so the two do not chase the same documents.
    /// </summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrphanedProcessingJobRecoveryService> _logger;

    public OrphanedProcessingJobRecoveryService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrphanedProcessingJobRecoveryService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            var recovered = await RecoverOrphanedJobsAsync(stoppingToken).ConfigureAwait(false);

            if (recovered == 0)
                _logger.LogInformation("[OrphanedJobRecovery] No orphaned Processing jobs found");
            else
                _logger.LogWarning(
                    "[OrphanedJobRecovery] Requeued {Count} job(s) orphaned by a restart (Issue #3588)",
                    recovered);
        }
#pragma warning disable CA1031 // Startup recovery must never crash the host
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "[OrphanedJobRecovery] Failed to recover orphaned processing jobs");
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Requeues every <c>Processing</c> job and rewinds its PDF so the pipeline can claim it again.
    /// Exposed as internal for integration testing (InternalsVisibleTo Api.Tests).
    /// </summary>
    internal async Task<int> RecoverOrphanedJobsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var orphans = await db.Set<ProcessingJobEntity>()
            .AsNoTracking()
            .Where(j => j.Status == nameof(JobStatus.Processing))
            .Select(j => new { j.Id, j.PdfDocumentId })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (orphans.Count == 0)
            return 0;

        var jobIds = orphans.Select(o => o.Id).ToList();
        var pdfIds = orphans.Select(o => o.PdfDocumentId).Distinct().ToList();

        // Both writes must land together. Requeuing the job while leaving its PDF in a mid-pipeline
        // state would be worse than doing nothing: the worker would pick the job up, the pipeline's
        // atomic Pending-claim would refuse a non-Pending document and return silently, and the
        // worker would then mark the job Completed even though nothing was processed.
        //
        // CreateExecutionStrategy() is mandatory: production configures
        // NpgsqlRetryingExecutionStrategy, which rejects user-opened transactions unless the whole
        // unit is wrapped so a transient failure can retry it as a block.
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            await using var _ = tx.ConfigureAwait(false);

            var requeued = await db.Set<ProcessingJobEntity>()
                .Where(j => jobIds.Contains(j.Id))
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(j => j.Status, nameof(JobStatus.Queued))
                        .SetProperty(j => j.StartedAt, (DateTimeOffset?)null)
                        .SetProperty(j => j.LastProgressAt, (DateTimeOffset?)null)
                        .SetProperty(j => j.CurrentStep, (string?)null),
                    ct)
                .ConfigureAwait(false);

            // Rewind only non-terminal documents: a PDF that reached Ready or Failed while its job
            // row was left behind must keep its outcome.
            await db.Set<PdfDocumentEntity>()
                .Where(p => pdfIds.Contains(p.Id)
                         && p.ProcessingState != nameof(PdfProcessingState.Ready)
                         && p.ProcessingState != nameof(PdfProcessingState.Failed))
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(p => p.ProcessingState, nameof(PdfProcessingState.Pending))
                        .SetProperty(p => p.ProcessingError, (string?)null),
                    ct)
                .ConfigureAwait(false);

            await tx.CommitAsync(ct).ConfigureAwait(false);

            return requeued;
        }).ConfigureAwait(false);
    }
}
