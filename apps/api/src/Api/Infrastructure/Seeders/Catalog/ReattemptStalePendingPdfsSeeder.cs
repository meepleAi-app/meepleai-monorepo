using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Issue #2908: re-enqueues PDFs stranded in <c>ProcessingState='Pending'</c> for a valid catalog
/// game but with NO active (<c>Queued</c>/<c>Processing</c>) ProcessingJob — they would otherwise
/// sit in Pending forever because <c>PdfProcessingQuartzJob</c> only polls <c>Queued</c> jobs.
///
/// Creates a <c>Queued</c> ProcessingJob (+5 steps) via the shared
/// <see cref="PdfSeeder.EnqueueProcessingJob"/> helper — identical to how PdfSeeder enqueues a new
/// PDF. Scoped to valid catalog games (excludes orphans handled by <see cref="OrphanPdfCleanupSeeder"/>),
/// idempotent (a re-enqueued PDF gains a Queued job and is excluded on re-run), and resilient.
/// </summary>
internal static class ReattemptStalePendingPdfsSeeder
{
    /// <summary>
    /// Returns the ids of PDFs in <c>Pending</c> state for a live catalog SharedGame that have no
    /// active (<c>Queued</c>/<c>Processing</c>) ProcessingJob. A stale terminal job
    /// (Completed/Failed/Cancelled) does not count as active.
    /// </summary>
    internal static Task<List<Guid>> FindStrandedPendingPdfIdsAsync(MeepleAiDbContext db, CancellationToken ct)
    {
        var validGameIds = db.SharedGames.Select(g => g.Id);
        return db.PdfDocuments
            // #3075: never re-enqueue demo mock placeholders (seed/ prefix, no real blob) — they are
            // seeded Pending for the dashboard demo; re-enqueuing would only fail on the missing blob
            // and flip them to Failed, breaking their intended demo state.
            .Where(p => !p.FilePath.StartsWith(PdfDocumentEntity.DemoMockFilePathPrefix)
                && p.ProcessingState == nameof(PdfProcessingState.Pending)
                && p.SharedGameId != null
                && validGameIds.Contains(p.SharedGameId.Value)
                && !db.ProcessingJobs.Any(j => j.PdfDocumentId == p.Id
                    && (j.Status == nameof(JobStatus.Queued) || j.Status == nameof(JobStatus.Processing))))
            .Select(p => p.Id)
            .ToListAsync(ct);
    }

    public static async Task ReattemptAsync(
        MeepleAiDbContext db,
        Guid systemUserId,
        ILogger logger,
        CancellationToken ct)
    {
        var strandedIds = await FindStrandedPendingPdfIdsAsync(db, ct).ConfigureAwait(false);
        if (strandedIds.Count == 0)
        {
            logger.LogInformation("ReattemptStalePendingPdfsSeeder: no stranded Pending PDFs found");
            return;
        }

        var enqueued = 0;
        var skipped = 0;
        foreach (var pdfId in strandedIds)
        {
            try
            {
                PdfSeeder.EnqueueProcessingJob(db, pdfId, systemUserId);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                enqueued++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "ReattemptStalePendingPdfsSeeder: failed to re-enqueue PDF {PdfId}. Continuing.", pdfId);
                db.ChangeTracker.Clear();
                skipped++;
            }
        }

        logger.LogInformation(
            "ReattemptStalePendingPdfsSeeder completed: {Enqueued} PDFs re-enqueued, {Skipped} skipped",
            enqueued, skipped);
    }
}
