using Api.Infrastructure;
using Api.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.DocumentProcessing.Application.Jobs;

/// <summary>
/// Issue #2248 (epic #2242, Sub #6 Block B): periodic guard against the silent-failure
/// mode that #2243 Block A fixed at the publish-event level.
///
/// The invariant: PdfDocument.ProcessingState == "Ready" ⇒
/// SharedGame.HasKnowledgeBase == true.
///
/// If the tactical mediator.Publish(VectorDocumentIndexedEvent) inside
/// FinalizeProcessingAsync is ever bypassed (regression, new ingestion path
/// added without raising the event, swallowed exception in the projection
/// handler, etc.) admin UIs silently revert to showing "no agent ready" while
/// the catalog is otherwise healthy. This job catches the drift at most 10
/// minutes after it appears so the SLO=0 alert fires.
///
/// Each detected row increments
/// <see cref="MeepleAiMetrics.PdfIndexedNoKbFlagTotal"/> and logs the
/// PdfDocumentId + SharedGameId at Warning. The job is idempotent and best
/// effort — it does NOT try to repair the drift (that is the job of Sub #2
/// architecture refactor — VectorDocument.Create factory — or a manual
/// re-index for already-stuck docs).
/// </summary>
[DisallowConcurrentExecution]
internal sealed class KbFlagDriftAuditJob : IJob
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<KbFlagDriftAuditJob> _logger;

    public KbFlagDriftAuditJob(
        MeepleAiDbContext dbContext,
        ILogger<KbFlagDriftAuditJob> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cancellationToken = context.CancellationToken;

        try
        {
            // Inner join PdfDocuments (Ready) with SharedGames (HasKnowledgeBase=false)
            // via SharedGameId. Projection-only, no tracking, no entity materialization.
            // Limited to 100 rows per run — if drift is widespread the SLO=0 alert fires
            // on the first batch and the operator triages from the audit log.
            var driftedRows = await _dbContext.PdfDocuments
                .AsNoTracking()
                .Where(p => p.ProcessingState == "Ready" && p.SharedGameId != null)
                .Join(
                    _dbContext.SharedGames.AsNoTracking().Where(g => !g.HasKnowledgeBase),
                    p => p.SharedGameId,
                    g => g.Id,
                    (p, g) => new { PdfDocumentId = p.Id, SharedGameId = g.Id })
                .Take(100)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (driftedRows.Count == 0)
            {
                _logger.LogDebug(
                    "KbFlagDriftAuditJob: 0 drifted rows (FireTime={FireTime})",
                    context.FireTimeUtc);
                context.Result = new { DriftedRows = 0 };
                return;
            }

            foreach (var row in driftedRows)
            {
                MeepleAiMetrics.RecordPdfIndexedNoKbFlagDrift();
                _logger.LogWarning(
                    "KbFlagDrift detected: PdfDocument {PdfDocumentId} is Ready but " +
                    "SharedGame {SharedGameId} HasKnowledgeBase=false. " +
                    "Root cause: VectorDocumentIndexedEvent was not published for this PDF " +
                    "(#2243 Block A regression or new ingestion path bypassing the publish).",
                    row.PdfDocumentId,
                    row.SharedGameId);
            }

            _logger.LogError(
                "KbFlagDriftAuditJob: {Count} drifted rows detected — SLO=0 violated. " +
                "Investigate publish-event chain in UploadPdfCommandHandler.FinalizeProcessingAsync, " +
                "PdfProcessingPipelineService and IndexPdfCommandHandler.",
                driftedRows.Count);

            context.Result = new { DriftedRows = driftedRows.Count };
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Background job — must not throw or Quartz suspends the trigger.
        catch (Exception ex)
        {
            _logger.LogError(ex, "KbFlagDriftAuditJob: unexpected error during audit");
            context.Result = new { Error = ex.Message };
        }
#pragma warning restore CA1031
    }
}
