using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure.Entities.DocumentProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Seed-time self-healing for the PDF catalog (runs in the Catalog layer, after PdfSeeder).
/// Complements the deterministic-id fix (#2904) — which stops NEW orphans from forming — by
/// repairing state left behind by earlier non-idempotent re-seeds:
/// <list type="bullet">
/// <item><see cref="CleanupOrphanPdfsAsync"/> (#2907): hard-deletes pdf_documents whose
/// SharedGameId no longer resolves to any shared_games row.</item>
/// <item><see cref="ReenqueueStalePendingPdfsAsync"/> (#2908): enqueues a Queued ProcessingJob
/// for valid-catalog PDFs stuck in Pending with no active job, so PdfProcessingQuartzJob
/// (every 10s) picks them up through the RAG pipeline.</item>
/// </list>
/// Both are idempotent — a no-op once the catalog is healthy.
/// </summary>
internal static class SeedMaintenanceSeeder
{
    /// <summary>
    /// #2907: hard-delete orphan PDF documents whose <c>SharedGameId</c> points to a game that no
    /// longer exists (physically absent — <see cref="EntityFrameworkQueryableExtensions.IgnoreQueryFilters{TEntity}"/>
    /// so a merely soft-deleted game is NOT treated as an orphan). Associated processing_jobs
    /// cascade via FK; blob files are shared/idempotent in the seed bucket and are NOT touched.
    /// </summary>
    /// <returns>The number of orphan documents removed.</returns>
    public static async Task<int> CleanupOrphanPdfsAsync(
        MeepleAiDbContext db,
        ILogger logger,
        CancellationToken ct)
    {
        var gameIdSet = (await db.SharedGames
            .IgnoreQueryFilters()
            .Select(g => g.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false)).ToHashSet();

        var orphans = (await db.PdfDocuments
            .Where(p => p.SharedGameId != null)
            .ToListAsync(ct)
            .ConfigureAwait(false))
            .Where(p => !gameIdSet.Contains(p.SharedGameId!.Value))
            .ToList();

        if (orphans.Count == 0)
        {
            logger.LogInformation("SeedMaintenance: no orphan PDF documents to clean up (#2907)");
            return 0;
        }

        // Remove the orphans' processing_jobs (and their steps) explicitly rather than relying on
        // the DB-level ON DELETE CASCADE — this keeps the delete provider-agnostic (the InMemory
        // test provider does not enforce FK cascades) and covered by unit tests.
        var orphanIds = orphans.Select(p => p.Id).ToHashSet();
        var orphanJobs = await db.Set<ProcessingJobEntity>()
            .Include(j => j.Steps)
            .Where(j => orphanIds.Contains(j.PdfDocumentId))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (orphanJobs.Count > 0)
            db.Set<ProcessingJobEntity>().RemoveRange(orphanJobs);

        db.PdfDocuments.RemoveRange(orphans);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation(
            "SeedMaintenance: hard-deleted {Count} orphan PDF document(s) with a dangling shared_game_id (#2907)",
            orphans.Count);
        return orphans.Count;
    }

    /// <summary>
    /// #2908: enqueue a Queued ProcessingJob (+ the five pipeline steps, matching PdfSeeder) for
    /// valid-catalog PDFs stuck in <see cref="PdfProcessingState.Pending"/> with no active
    /// (Queued/Processing) job. Scoped to games that still exist so orphans are never re-processed
    /// (<see cref="CleanupOrphanPdfsAsync"/> removes those). Idempotent.
    /// </summary>
    /// <returns>The number of PDFs re-enqueued.</returns>
    public static async Task<int> ReenqueueStalePendingPdfsAsync(
        MeepleAiDbContext db,
        Guid systemUserId,
        ILogger logger,
        CancellationToken ct)
    {
        var pending = nameof(PdfProcessingState.Pending);
        var queued = nameof(JobStatus.Queued);
        var processing = nameof(JobStatus.Processing);

        // Deliberately WITHOUT IgnoreQueryFilters (asymmetric with CleanupOrphanPdfsAsync): a
        // soft-deleted game is excluded here, so its PDFs are NOT re-processed — we don't index a
        // removed game. Cleanup, by contrast, keeps those PDFs (the game may be restored); if it is
        // un-deleted a later seed run re-enqueues its Pending PDFs. Both paths are conservative.
        var validGameIds = (await db.SharedGames
            .Select(g => g.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false)).ToHashSet();

        var pendingPdfs = await db.PdfDocuments
            .Where(p => p.SharedGameId != null && p.ProcessingState == pending)
            .Select(p => new { p.Id, SharedGameId = p.SharedGameId!.Value })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var activeJobPdfIds = (await db.Set<ProcessingJobEntity>()
            .Where(j => j.Status == queued || j.Status == processing)
            .Select(j => j.PdfDocumentId)
            .ToListAsync(ct)
            .ConfigureAwait(false)).ToHashSet();

        var toEnqueue = pendingPdfs
            .Where(p => validGameIds.Contains(p.SharedGameId) && !activeJobPdfIds.Contains(p.Id))
            .ToList();

        if (toEnqueue.Count == 0)
        {
            logger.LogInformation("SeedMaintenance: no stale Pending PDFs to re-enqueue (#2908)");
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var pdf in toEnqueue)
        {
            var job = new ProcessingJobEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdf.Id,
                UserId = systemUserId,
                Status = queued,
                Priority = 0,
                CreatedAt = now,
                MaxRetries = 3,
                RetryCount = 0,
            };
            job.Steps = new List<ProcessingStepEntity>
            {
                new() { Id = Guid.NewGuid(), ProcessingJobId = job.Id, StepName = nameof(ProcessingStepType.Upload),  Status = "Pending" },
                new() { Id = Guid.NewGuid(), ProcessingJobId = job.Id, StepName = nameof(ProcessingStepType.Extract), Status = "Pending" },
                new() { Id = Guid.NewGuid(), ProcessingJobId = job.Id, StepName = nameof(ProcessingStepType.Chunk),   Status = "Pending" },
                new() { Id = Guid.NewGuid(), ProcessingJobId = job.Id, StepName = nameof(ProcessingStepType.Embed),   Status = "Pending" },
                new() { Id = Guid.NewGuid(), ProcessingJobId = job.Id, StepName = nameof(ProcessingStepType.Index),   Status = "Pending" },
            };
            db.Set<ProcessingJobEntity>().Add(job);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        logger.LogInformation(
            "SeedMaintenance: re-enqueued {Count} stale Pending PDF(s) with no active job (#2908)",
            toEnqueue.Count);
        return toEnqueue.Count;
    }
}
