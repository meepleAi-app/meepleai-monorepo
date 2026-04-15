using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Infrastructure.Seeders.Catalog.SeedBlob;
using Api.Services.Pdf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Seeds PDF rulebook documents from the seed blob bucket (meepleai-seeds) using the YAML manifest.
/// Creates PdfDocumentEntity records in <see cref="PdfProcessingState.Pending"/> state AND
/// enqueues a corresponding ProcessingJob so PdfProcessingQuartzJob (runs every 10s) picks them
/// up through the full RAG pipeline (extract → chunk → embed → index).
///
/// IMPORTANT: PdfProcessingQuartzJob reads from <c>processing_jobs WHERE Status='Queued'</c>,
/// not from <c>pdf_documents WHERE ProcessingState='Pending'</c>. Without an explicit
/// ProcessingJob row the seeded PDFs would stay in Pending forever (StalePdfRecoveryService
/// only picks them up after 2 minutes of staleness and runs only once at boot, so they slip
/// through the crack).
///
/// Idempotent: skips PDFs where GameId + FileName already exists with matching hash.
/// Hash drift: deletes old document cascade and reinserts when hash changes.
/// </summary>
internal static class PdfSeeder
{
    /// <summary>
    /// Seeds PDF documents for games in the manifest that have a <c>PdfBlobKey</c> field set.
    /// Reads from the seed blob bucket and stores into the primary blob storage.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="manifest">The loaded seed manifest (used to resolve game-PDF mappings).</param>
    /// <param name="gameMap">Dictionary mapping BggId to GameEntity.Id, produced by GameSeeder.</param>
    /// <param name="systemUserId">System/admin user ID used for the UploadedByUserId FK.</param>
    /// <param name="primaryBlob">Primary blob storage service (destination for PDFs).</param>
    /// <param name="seedBlob">Seed blob reader (source bucket for seed PDFs).</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SeedAsync(
        MeepleAiDbContext db,
        SeedManifest manifest,
        Dictionary<int, Guid> gameMap,
        Guid systemUserId,
        IBlobStorageService primaryBlob,
        ISeedBlobReader seedBlob,
        ILogger logger,
        CancellationToken ct)
    {
        if (!seedBlob.IsConfigured)
        {
            logger.LogInformation("PdfSeeder: seed blob reader not configured. Skipping PDF seeding.");
            return;
        }

        // Build list of games that have a PdfBlobKey entry in the manifest
        var pdfEntries = manifest.Catalog.Games
            .Where(g => !string.IsNullOrWhiteSpace(g.PdfBlobKey) && g.BggId is > 0)
            .ToList();

        if (pdfEntries.Count == 0)
        {
            logger.LogInformation("PdfSeeder: no blob PDF entries found in manifest. Skipping.");
            return;
        }

        logger.LogInformation("PdfSeeder: processing {Count} blob PDF entries from manifest", pdfEntries.Count);

        // Load existing PDF documents for idempotency check (GameId + FileName → ContentHash)
        var existingPdfs = await db.PdfDocuments
            .AsNoTracking()
            .Select(p => new { p.Id, p.GameId, p.FileName, p.ContentHash, p.FilePath })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var existingMap = existingPdfs
            .Where(p => p.GameId.HasValue)
            .ToDictionary(
                p => $"{p.GameId}:{p.FileName}",
                p => new { p.Id, p.ContentHash, p.FilePath },
                StringComparer.OrdinalIgnoreCase);

        var seeded = 0;
        var skipped = 0;

        foreach (var entry in pdfEntries)
        {
            var fileName = entry.Pdf ?? Path.GetFileName(entry.PdfBlobKey!);
            var blobKey = entry.PdfBlobKey!;
            var manifestHash = entry.PdfSha256;

            try
            {
                // Resolve GameEntity.Id from the gameMap built by GameSeeder
                if (!gameMap.TryGetValue(entry.BggId!.Value, out var gameId))
                {
                    logger.LogWarning(
                        "PdfSeeder: no GameEntity found for BggId={BggId} ('{Title}'). Skipping blob PDF.",
                        entry.BggId, entry.Title);
                    skipped++;
                    continue;
                }

                var idempotencyKey = $"{gameId}:{fileName}";
                var gameIdStr = gameId.ToString("N");

                // Idempotency: check if GameId + FileName pair already exists
                if (existingMap.TryGetValue(idempotencyKey, out var existing))
                {
                    // Hash match → skip (no changes)
                    if (!string.IsNullOrEmpty(manifestHash) &&
                        string.Equals(existing.ContentHash, manifestHash, StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogDebug(
                            "PdfSeeder: PDF '{FileName}' for game '{Title}' has matching hash. Skipping.",
                            fileName, entry.Title);
                        skipped++;
                        continue;
                    }

                    // Hash drift → delete old document cascade and reinsert
                    logger.LogInformation(
                        "PdfSeeder: hash drift detected for '{FileName}' (game '{Title}'). Replacing.",
                        fileName, entry.Title);

                    await DeletePdfCascadeAsync(db, primaryBlob, existing.Id, gameIdStr, existing.FilePath, logger, ct)
                        .ConfigureAwait(false);

                    existingMap.Remove(idempotencyKey);
                }

                // Verify blob exists in seed bucket
                if (!await seedBlob.ExistsAsync(blobKey, ct).ConfigureAwait(false))
                {
                    logger.LogWarning(
                        "PdfSeeder: blob '{BlobKey}' not found in seed bucket for game '{Title}'. Skipping.",
                        blobKey, entry.Title);
                    skipped++;
                    continue;
                }

                // Stream from seed bucket → store into primary blob
                var stream = await seedBlob.OpenReadAsync(blobKey, ct).ConfigureAwait(false);
                await using var _ = stream.ConfigureAwait(false);
                var result = await primaryBlob.StoreAsync(stream, fileName, gameIdStr, ct).ConfigureAwait(false);

                if (!result.Success)
                {
                    logger.LogWarning(
                        "PdfSeeder: failed to store blob for '{FileName}' (game '{Title}'): {Error}",
                        fileName, entry.Title, result.ErrorMessage);
                    skipped++;
                    continue;
                }

                // Look up SharedGameId from the Games table
                var sharedGameId = await db.Games
                    .AsNoTracking()
                    .Where(g => g.Id == gameId)
                    .Select(g => g.SharedGameId)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                // Create PdfDocumentEntity in Pending state
                var pdfEntity = new PdfDocumentEntity
                {
                    Id = Guid.NewGuid(),
                    GameId = gameId,
                    SharedGameId = sharedGameId,
                    FileName = fileName,
                    FilePath = result.FilePath ?? string.Empty,
                    FileSizeBytes = result.FileSizeBytes,
                    ContentType = "application/pdf",
                    Language = entry.Language,
                    ProcessingState = nameof(PdfProcessingState.Pending),
                    IsPublic = true,
                    DocumentType = "base",
                    SortOrder = 0,
                    DocumentCategory = "Rulebook",
                    IsActiveForRag = true,
                    ProcessingPriority = "Normal",
                    UploadedAt = DateTime.UtcNow,
                    UploadedByUserId = systemUserId,
                    ContentHash = manifestHash,
                    VersionLabel = entry.PdfVersion,
                };

                db.PdfDocuments.Add(pdfEntity);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                // Enqueue a ProcessingJob so PdfProcessingQuartzJob picks this PDF up.
                // We write the EF entity directly (not the ProcessingJob.Create aggregate
                // factory) to bypass the MaxQueueSize=100 guard — a seed run can push
                // more than 100 PDFs and those limits are meant for user-driven enqueue,
                // not batch seeding. We also initialise the five standard pipeline steps
                // so the job row matches what EnqueuePdfCommandHandler would have produced.
                var now = DateTimeOffset.UtcNow;
                var jobEntity = new ProcessingJobEntity
                {
                    Id = Guid.NewGuid(),
                    PdfDocumentId = pdfEntity.Id,
                    UserId = systemUserId,
                    Status = nameof(JobStatus.Queued),
                    Priority = 0,
                    CreatedAt = now,
                    MaxRetries = 3,
                    RetryCount = 0,
                };
                jobEntity.Steps = new List<ProcessingStepEntity>
                {
                    new() { Id = Guid.NewGuid(), ProcessingJobId = jobEntity.Id, StepName = nameof(ProcessingStepType.Upload),  Status = "Pending" },
                    new() { Id = Guid.NewGuid(), ProcessingJobId = jobEntity.Id, StepName = nameof(ProcessingStepType.Extract), Status = "Pending" },
                    new() { Id = Guid.NewGuid(), ProcessingJobId = jobEntity.Id, StepName = nameof(ProcessingStepType.Chunk),   Status = "Pending" },
                    new() { Id = Guid.NewGuid(), ProcessingJobId = jobEntity.Id, StepName = nameof(ProcessingStepType.Embed),   Status = "Pending" },
                    new() { Id = Guid.NewGuid(), ProcessingJobId = jobEntity.Id, StepName = nameof(ProcessingStepType.Index),   Status = "Pending" },
                };
                db.Set<ProcessingJobEntity>().Add(jobEntity);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                // Track for subsequent iterations
                existingMap[idempotencyKey] = new { pdfEntity.Id, pdfEntity.ContentHash, pdfEntity.FilePath };

                seeded++;
                logger.LogInformation(
                    "PdfSeeder: stored blob '{FileName}' for game '{Title}' (GameId={GameId}, PdfId={PdfId}, JobId={JobId}, {Size} bytes) queued for processing",
                    fileName, entry.Title, gameId, pdfEntity.Id, jobEntity.Id, result.FileSizeBytes);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "PdfSeeder: failed to seed blob PDF '{FileName}' for game '{Title}'. Continuing.",
                    fileName, entry.Title);
                db.ChangeTracker.Clear();
                skipped++;
            }
        }

        logger.LogInformation(
            "PdfSeeder completed: {Seeded} queued for RAG processing, {Skipped} skipped",
            seeded, skipped);
    }

    /// <summary>
    /// Deletes an existing PdfDocumentEntity and its primary blob (best effort).
    /// Used when hash drift is detected to replace old content.
    /// </summary>
    private static async Task DeletePdfCascadeAsync(
        MeepleAiDbContext db,
        IBlobStorageService primaryBlob,
        Guid pdfId,
        string gameIdStr,
        string? filePath,
        ILogger logger,
        CancellationToken ct)
    {
        // Best-effort delete from primary blob
        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                // Extract fileId from filePath — use the path segment as-is
                await primaryBlob.DeleteAsync(filePath, gameIdStr, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "PdfSeeder: best-effort blob delete failed for '{FilePath}'", filePath);
            }
        }

        // Delete from database
        var entity = await db.PdfDocuments.FindAsync(new object[] { pdfId }, ct).ConfigureAwait(false);
        if (entity is not null)
        {
            db.PdfDocuments.Remove(entity);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
