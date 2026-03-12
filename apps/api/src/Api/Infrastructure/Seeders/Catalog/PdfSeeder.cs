using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Seeds PDF rulebook documents from data/rulebook/ directory using the YAML manifest.
/// Creates PdfDocumentEntity records in <see cref="PdfProcessingState.Pending"/> state.
/// The existing PdfProcessingQuartzJob (runs every 10s) will automatically pick up
/// and process them through the full RAG pipeline (extract → chunk → embed → index).
/// Idempotent: skips PDFs where GameId + FileName already exists.
/// </summary>
internal static class PdfSeeder
{
    /// <summary>
    /// Seeds PDF documents for games in the manifest that have a <c>pdf</c> field set.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="manifest">The loaded seed manifest (used to resolve game-PDF mappings).</param>
    /// <param name="gameMap">Dictionary mapping BggId to GameEntity.Id, produced by GameSeeder.</param>
    /// <param name="systemUserId">System/admin user ID used for the UploadedByUserId FK.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SeedAsync(
        MeepleAiDbContext db,
        SeedManifest manifest,
        Dictionary<int, Guid> gameMap,
        Guid systemUserId,
        ILogger logger,
        CancellationToken ct)
    {
        // Resolve the data/rulebook/ directory by walking up from CWD
        var rulebookDir = ResolveRulebookDirectory();
        if (rulebookDir == null)
        {
            logger.LogWarning("PdfSeeder: could not find data/rulebook/ directory. Skipping PDF seeding.");
            return;
        }

        // Build list of games that have a PDF entry in the manifest
        var pdfEntries = manifest.Catalog.Games
            .Where(g => !string.IsNullOrWhiteSpace(g.Pdf) && g.BggId is > 0)
            .ToList();

        if (pdfEntries.Count == 0)
        {
            logger.LogInformation("PdfSeeder: no PDF entries found in manifest. Skipping.");
            return;
        }

        logger.LogInformation("PdfSeeder: processing {Count} PDF entries from manifest", pdfEntries.Count);

        // Load existing PDF documents for idempotency check (GameId + FileName)
        var existingPdfs = await db.PdfDocuments
            .AsNoTracking()
            .Select(p => new { p.GameId, p.FileName })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var existingSet = new HashSet<string>(
            existingPdfs.Select(p => $"{p.GameId}:{p.FileName}"),
            StringComparer.OrdinalIgnoreCase);

        var seeded = 0;
        var skipped = 0;

        foreach (var entry in pdfEntries)
        {
            var fileName = entry.Pdf!;

            try
            {
                // Resolve GameEntity.Id from the gameMap built by GameSeeder
                if (!gameMap.TryGetValue(entry.BggId!.Value, out var gameId))
                {
                    logger.LogWarning(
                        "PdfSeeder: no GameEntity found for BggId={BggId} ('{Title}'). Skipping PDF '{FileName}'.",
                        entry.BggId, entry.Title, fileName);
                    skipped++;
                    continue;
                }

                // Idempotency: skip if this GameId + FileName pair already exists
                if (existingSet.Contains($"{gameId}:{fileName}"))
                {
                    logger.LogDebug(
                        "PdfSeeder: PDF '{FileName}' already exists for game '{Title}' (GameId={GameId}). Skipping.",
                        fileName, entry.Title, gameId);
                    skipped++;
                    continue;
                }

                // Verify source PDF file exists on disk
                var sourceFile = Path.Combine(rulebookDir, fileName);
                if (!File.Exists(sourceFile))
                {
                    logger.LogWarning(
                        "PdfSeeder: source PDF not found at '{SourceFile}' for game '{Title}'. Skipping.",
                        sourceFile, entry.Title);
                    skipped++;
                    continue;
                }

                var fileSize = new FileInfo(sourceFile).Length;

                // Create PdfDocumentEntity in Pending state.
                // The PdfProcessingQuartzJob will pick it up and drive it through the pipeline:
                //   Pending → Uploading → Extracting → Chunking → Embedding → Indexing → Ready
                var pdfEntity = new PdfDocumentEntity
                {
                    Id = Guid.NewGuid(),
                    GameId = gameId,
                    FileName = fileName,
                    FilePath = sourceFile,
                    FileSizeBytes = fileSize,
                    ContentType = "application/pdf",
                    Language = entry.Language,
                    ProcessingState = nameof(PdfProcessingState.Pending),
                    ProcessingStatus = "pending",
                    IsPublic = true,
                    DocumentType = "base",
                    SortOrder = 0,
                    DocumentCategory = "Rulebook",
                    IsActiveForRag = true,
                    ProcessingPriority = "Normal",
                    UploadedAt = DateTime.UtcNow,
                    UploadedByUserId = systemUserId,
                };

                db.PdfDocuments.Add(pdfEntity);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                // Add to in-memory idempotency set so subsequent iterations won't re-insert
                existingSet.Add($"{gameId}:{fileName}");

                seeded++;
                logger.LogInformation(
                    "PdfSeeder: queued PDF '{FileName}' for game '{Title}' (GameId={GameId}, PdfId={PdfId}, {Size} bytes) in Pending state",
                    fileName, entry.Title, gameId, pdfEntity.Id, fileSize);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "PdfSeeder: failed to seed PDF '{FileName}' for game '{Title}'. Continuing.",
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
    /// Resolves the data/rulebook/ directory by walking up from the current working directory.
    /// Handles running from apps/api/src/Api/ (local dev) or /app/ (Docker).
    /// </summary>
    private static string? ResolveRulebookDirectory()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "data", "rulebook");
            if (Directory.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        return null;
    }
}
