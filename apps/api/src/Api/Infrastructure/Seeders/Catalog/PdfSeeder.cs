using Api.BoundedContexts.DocumentProcessing.Application.Services;
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
/// Idempotent: skips PDFs where SharedGameId + FileName already exists with matching hash.
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
    /// <param name="gameMap">Dictionary mapping BggId to SharedGame.Id, produced by GameSeeder (post-Phase2d: legacy GameEntity bridge removed).</param>
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

        // Post-Phase2d: legacy GameEntity is gone; SharedGameId IS what was previously GameId.
        // The gameIdToSharedId lookup collapses to an identity mapping.
        var gameIdToSharedId = await db.SharedGames
            .AsNoTracking()
            .Select(g => g.Id)
            .ToDictionaryAsync(id => id, id => id, ct)
            .ConfigureAwait(false);

        // Load existing PDF documents for idempotency check (SharedGameId + FileName → ContentHash)
        var existingPdfs = await db.PdfDocuments
            .AsNoTracking()
            .Select(p => new { p.Id, p.SharedGameId, p.FileName, p.ContentHash, p.FilePath })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var existingMap = existingPdfs
            .Where(p => p.SharedGameId.HasValue)
            .ToDictionary(
                p => $"{p.SharedGameId}:{p.FileName}",
                p => new { p.Id, p.ContentHash, p.FilePath },
                StringComparer.OrdinalIgnoreCase);

        var seeded = 0;
        var skipped = 0;
        var repaired = 0;

        foreach (var entry in pdfEntries)
        {
            var fileName = entry.Pdf ?? Path.GetFileName(entry.PdfBlobKey!);
            var blobKey = entry.PdfBlobKey!;
            var manifestHash = entry.PdfSha256;

            try
            {
                // Resolve SharedGame.Id from the gameMap built by GameSeeder (post-Phase2d: GameEntity bridge removed).
                if (!gameMap.TryGetValue(entry.BggId!.Value, out var gameId))
                {
                    logger.LogWarning(
                        "PdfSeeder: no SharedGame found for BggId={BggId} ('{Title}'). Skipping blob PDF.",
                        entry.BggId, entry.Title);
                    skipped++;
                    continue;
                }

                // Validate the SharedGameId exists (identity-mapped lookup post-Phase2d).
                // PdfDocumentEntity now stores SharedGameId directly after the 2026-04-19 migration,
                // so this is the key field for both idempotency and persistence.
                if (!gameIdToSharedId.TryGetValue(gameId, out var sharedGameId))
                {
                    logger.LogWarning(
                        "PdfSeeder: SharedGameId {GameId} ('{Title}') not found in catalog. Skipping blob PDF.",
                        gameId, entry.Title);
                    skipped++;
                    continue;
                }

                var idempotencyKey = $"{sharedGameId}:{fileName}";

                // Idempotency: check if SharedGameId + FileName pair already exists
                if (existingMap.TryGetValue(idempotencyKey, out var existing))
                {
                    // Hash match → the DB record is up to date, so normally we skip. BUT
                    // repair mode (#2666): a DB-only snapshot restore re-creates the record
                    // while the actual blob stays absent from the runtime bucket, leaving the
                    // PDF stuck in Failed with "PDF file not found in blob storage". Verify the
                    // blob is really present before skipping; if it's gone, re-upload it from the
                    // seed bucket against the EXISTING id and reset the record to Pending so the
                    // pipeline reprocesses it. Idempotent: once repaired the blob is present and
                    // the next run skips normally.
                    if (!string.IsNullOrEmpty(manifestHash) &&
                        string.Equals(existing.ContentHash, manifestHash, StringComparison.OrdinalIgnoreCase))
                    {
                        // fileId is embedded in the stored FilePath (pdfs/{resourceKey}/{fileId}_{name});
                        // extract it so ExistsAsync can locate the blob for the EXISTING record.
                        var existingFileId = PdfStorageKey.FileIdFromPath(existing.FilePath);
                        var blobPresent = !string.IsNullOrEmpty(existingFileId)
                            && await primaryBlob.ExistsAsync(existingFileId, BlobCategory.Pdf, PdfStorageKey.ForPdf(existing.Id), ct).ConfigureAwait(false);

                        if (blobPresent)
                        {
                            logger.LogDebug(
                                "PdfSeeder: PDF '{FileName}' for game '{Title}' has matching hash and blob present. Skipping.",
                                fileName, entry.Title);
                            skipped++;
                            continue;
                        }

                        // Blob missing from the runtime bucket → attempt repair from the seed bucket.
                        if (await TryRepairMissingBlobAsync(
                                db, primaryBlob, seedBlob, existing.Id, blobKey, fileName,
                                entry, systemUserId, logger, ct).ConfigureAwait(false))
                        {
                            repaired++;
                        }
                        else
                        {
                            skipped++;
                        }
                        continue;
                    }

                    // Hash drift → delete old document cascade and reinsert
                    logger.LogInformation(
                        "PdfSeeder: hash drift detected for '{FileName}' (game '{Title}'). Replacing.",
                        fileName, entry.Title);

                    await DeletePdfCascadeAsync(db, primaryBlob, existing.Id, PdfStorageKey.ForPdf(existing.Id), existing.FilePath, logger, ct)
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

                // Pre-generate pdfId so StoreAsync and PdfDocumentEntity share the same bucket key.
                // Post-migration (2026-04-19) PDFs live under pdfs/{pdfId}/ — all reads use
                // PdfStorageKey.ForPdf(pdf.Id), so the seeder must also write to the pdfId bucket
                // or the pipeline (extract/download) will 404 on seeded files.
                var pdfId = Guid.NewGuid();

                // Stream from seed bucket → store into primary blob
                var stream = await seedBlob.OpenReadAsync(blobKey, ct).ConfigureAwait(false);
                await using var _ = stream.ConfigureAwait(false);
                var result = await primaryBlob.StoreAsync(stream, fileName, BlobCategory.Pdf, PdfStorageKey.ForPdf(pdfId), ct).ConfigureAwait(false);

                if (!result.Success)
                {
                    logger.LogWarning(
                        "PdfSeeder: failed to store blob for '{FileName}' (game '{Title}'): {Error}",
                        fileName, entry.Title, result.ErrorMessage);
                    skipped++;
                    continue;
                }

                // Create PdfDocumentEntity in Pending state
                // Community-seeded PDFs are stored against SharedGameId (community catalog id).
                var pdfEntity = new PdfDocumentEntity
                {
                    Id = pdfId,
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
            "PdfSeeder completed: {Seeded} queued for RAG processing, {Repaired} repaired (missing blob re-uploaded), {Skipped} skipped",
            seeded, repaired, skipped);
    }

    /// <summary>
    /// Repair mode (#2666): re-uploads a PDF blob that is missing from the runtime bucket
    /// (typically after a DB-only snapshot restore) using the EXISTING pdf id, then resets
    /// the record to <see cref="PdfProcessingState.Pending"/> and re-enqueues a ProcessingJob
    /// so the RAG pipeline reprocesses it (extract → chunk → embed → index → Ready).
    ///
    /// Uses <paramref name="existingId"/> for BOTH the storage bucket key and the DB record so
    /// the persisted FilePath (pdfs/{existingId}/...) keeps matching what the database already
    /// references — minting a fresh Guid would orphan the blob from the record.
    ///
    /// Returns true when the record was repaired; false when it could not be repaired (blob is
    /// missing in the seed bucket too, the re-upload failed, or the record vanished mid-run).
    /// Never throws for the recoverable cases: irrecoverable blobs are logged and left untouched.
    /// </summary>
    private static async Task<bool> TryRepairMissingBlobAsync(
        MeepleAiDbContext db,
        IBlobStorageService primaryBlob,
        ISeedBlobReader seedBlob,
        Guid existingId,
        string blobKey,
        string fileName,
        SeedManifestGame entry,
        Guid systemUserId,
        ILogger logger,
        CancellationToken ct)
    {
        // The blob is gone from the runtime bucket — it can only be repaired if the source
        // still exists in the seed bucket. If neither has it, the PDF is irrecoverable here.
        if (!await seedBlob.ExistsAsync(blobKey, ct).ConfigureAwait(false))
        {
            logger.LogWarning(
                "PdfSeeder: PDF '{FileName}' (pdfId {Id}, game '{Title}') blob missing in both runtime and seed bucket, cannot repair. Leaving record untouched.",
                fileName, existingId, entry.Title);
            return false;
        }

        // Re-upload against the EXISTING id (NOT a new Guid) so pdfs/{existingId}/ stays
        // consistent with the FilePath the DB record already points at.
        var stream = await seedBlob.OpenReadAsync(blobKey, ct).ConfigureAwait(false);
        await using var _ = stream.ConfigureAwait(false);
        var result = await primaryBlob.StoreAsync(stream, fileName, BlobCategory.Pdf, PdfStorageKey.ForPdf(existingId), ct).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogWarning(
                "PdfSeeder: repair failed to re-upload blob for '{FileName}' (pdfId {Id}, game '{Title}'): {Error}",
                fileName, existingId, entry.Title, result.ErrorMessage);
            return false;
        }

        // Load the existing record tracked and reset it to Pending, clearing every error field
        // left over from the previous Failed run so the pipeline reprocesses it cleanly.
        var pdfEntity = await db.PdfDocuments
            .FirstOrDefaultAsync(p => p.Id == existingId, ct)
            .ConfigureAwait(false);
        if (pdfEntity is null)
        {
            logger.LogWarning(
                "PdfSeeder: repair could not load PdfDocument {Id} ('{FileName}') for update. Skipping.",
                existingId, fileName);
            return false;
        }

        pdfEntity.ProcessingState = nameof(PdfProcessingState.Pending);
        pdfEntity.FilePath = result.FilePath ?? string.Empty;
        pdfEntity.FileSizeBytes = result.FileSizeBytes;
        pdfEntity.ProcessingError = null;
        pdfEntity.ErrorCategory = null;
        pdfEntity.FailedAtState = null;
        pdfEntity.RetryCount = 0;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Re-enqueue a ProcessingJob with the five standard pipeline steps (identical to the
        // new-record flow) so PdfProcessingQuartzJob picks the repaired PDF up again.
        var now = DateTimeOffset.UtcNow;
        var jobEntity = new ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = existingId,
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

        logger.LogInformation(
            "PdfSeeder: repaired: re-uploaded missing blob for '{FileName}' (pdfId {Id}, JobId={JobId}, {Size} bytes) and reset to Pending",
            fileName, existingId, jobEntity.Id, result.FileSizeBytes);
        return true;
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
        // Best-effort delete from primary blob.
        //
        // filePath shape (S3 + local): "pdf_uploads/{resourceKey}/{fileId}_{sanitizedFileName}".
        // DeleteAsync expects the bare fileId (without prefix/underscore/filename).
        // Review finding #3: previously the full filePath was passed as fileId, which
        // failed PathSecurity.ValidateIdentifier silently — fix below extracts just the
        // fileId GUID-without-hyphens segment.
        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                var fileId = PdfStorageKey.FileIdFromPath(filePath);
                if (!string.IsNullOrEmpty(fileId))
                {
                    await primaryBlob.DeleteAsync(fileId, BlobCategory.Pdf, gameIdStr, ct).ConfigureAwait(false);
                }
                else
                {
                    logger.LogWarning("PdfSeeder: cannot extract fileId from path '{FilePath}', skipping blob delete", filePath);
                }
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
