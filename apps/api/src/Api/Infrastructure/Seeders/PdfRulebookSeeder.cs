using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders;

/// <summary>
/// Seeds PDF rulebook documents from data/rulebook/ directory.
/// Copies files to blob storage location (pdf_uploads/{gameId}/) and creates DB entries.
/// Idempotent: skips games that already have a PDF document.
/// </summary>
internal static class PdfRulebookSeeder
{
    /// <summary>
    /// Mapping: filename in data/rulebook/ → (game name in games table, language code).
    /// </summary>
    private static readonly Dictionary<string, (string GameName, string Language)> RulebookMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["7-wonders_rulebook.pdf"] = ("7 Wonders", "en"),
        ["agricola_rulebook.pdf"] = ("Agricola", "en"),
        ["azul_rulebook.pdf"] = ("Azul", "en"),
        ["barrage_rulebook.pdf"] = ("Barrage", "en"),
        ["carcassone_rulebook.pdf"] = ("Carcassonne", "en"),
        ["cantan_en_rulebook.pdf"] = ("CATAN", "en"),
        ["pandemic_rulebook.pdf"] = ("Pandemic", "en"),
        ["root_rulebook.pdf"] = ("Root", "en"),
        ["scacchi-fide_2017_rulebook.pdf"] = ("Chess", "it"),
        ["splendor_rulebook.pdf"] = ("Splendor", "en"),
        ["terraforming-mars_rulebook.pdf"] = ("Terraforming Mars", "en"),
        ["ticket-to-ride_rulebook.pdf"] = ("Ticket to Ride", "en"),
        ["wingspan_en_rulebook.pdf"] = ("Wingspan", "en"),
    };

    /// <summary>
    /// Seeds PDF documents from data/rulebook/ into the blob storage and database.
    /// </summary>
    public static async Task SeedRulebooksAsync(
        MeepleAiDbContext db,
        Guid adminUserId,
        ILogger logger,
        string? pdfStoragePath = null,
        CancellationToken cancellationToken = default)
    {
        // Resolve source directory: data/rulebook/ relative to solution root
        var rulebookDir = ResolveRulebookDirectory();
        if (rulebookDir == null)
        {
            logger.LogWarning("Could not find data/rulebook/ directory. Skipping PDF rulebook seeding.");
            return;
        }

        // Resolve blob storage path (same logic as BlobStorageService)
        var storagePath = pdfStoragePath ?? Path.Combine(Directory.GetCurrentDirectory(), "pdf_uploads");

        logger.LogInformation("Starting PDF rulebook seed from {SourceDir}", rulebookDir);

        // Load all games by name for quick lookup
        var games = await db.Games
            .AsNoTracking()
            .ToDictionaryAsync(g => g.Name, g => g, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        // Load existing PDF documents to check idempotency
        var existingPdfs = await db.PdfDocuments
            .AsNoTracking()
            .Select(p => new { p.GameId, p.FileName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingSet = new HashSet<string>(existingPdfs.Select(p => $"{p.GameId}:{p.FileName}"), StringComparer.OrdinalIgnoreCase);

        var seeded = 0;
        var skipped = 0;

        foreach (var (fileName, (gameName, language)) in RulebookMappings)
        {
            try
            {
                // Find the game in the database
                if (!games.TryGetValue(gameName, out var game))
                {
                    logger.LogWarning("Game '{GameName}' not found in database for {FileName}. Skipping.", gameName, fileName);
                    skipped++;
                    continue;
                }

                // Check idempotency: skip if this game already has this PDF
                if (existingSet.Contains($"{game.Id}:{fileName}"))
                {
                    logger.LogDebug("PDF '{FileName}' already exists for game '{GameName}'. Skipping.", fileName, gameName);
                    skipped++;
                    continue;
                }

                // Verify source file exists
                var sourceFile = Path.Combine(rulebookDir, fileName);
                if (!File.Exists(sourceFile))
                {
                    logger.LogWarning("Source PDF not found: {SourceFile}. Skipping.", sourceFile);
                    skipped++;
                    continue;
                }

                // Generate PDF document ID
                var pdfId = Guid.NewGuid();
                var fileIdStr = pdfId.ToString("N");

                // Copy file to blob storage location: pdf_uploads/{gameId}/{pdfId:N}_{fileName}
                var gameDir = Path.Combine(storagePath, game.Id.ToString());
                Directory.CreateDirectory(gameDir);
                var destFile = Path.Combine(gameDir, $"{fileIdStr}_{fileName}");
                File.Copy(sourceFile, destFile, overwrite: true);

                var fileSize = new FileInfo(destFile).Length;

                // Create PdfDocumentEntity
                var pdfEntity = new PdfDocumentEntity
                {
                    Id = pdfId,
                    GameId = game.Id,
                    FileName = fileName,
                    FilePath = destFile,
                    FileSizeBytes = fileSize,
                    ContentType = "application/pdf",
                    UploadedByUserId = adminUserId,
                    UploadedAt = DateTime.UtcNow,
                    Language = language,
                    ProcessingStatus = "pending",
                    ProcessingState = "Pending",
                    IsPublic = true,
                    DocumentType = "base",
                    SortOrder = 0,
                };

                db.PdfDocuments.Add(pdfEntity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                seeded++;
                logger.LogInformation(
                    "Seeded PDF '{FileName}' for game '{GameName}' (ID: {PdfId}, {Size} bytes)",
                    fileName, gameName, pdfId, fileSize);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to seed PDF '{FileName}' for game '{GameName}'. Continuing.", fileName, gameName);
                skipped++;
            }
        }

        logger.LogInformation(
            "PDF rulebook seed completed: {Seeded} seeded, {Skipped} skipped",
            seeded, skipped);
    }

    /// <summary>
    /// Resolves the data/rulebook/ directory by walking up from CWD.
    /// Handles running from apps/api/src/Api/ (local dev) or /app/ (Docker).
    /// </summary>
    private static string? ResolveRulebookDirectory()
    {
        var current = Directory.GetCurrentDirectory();

        // Walk up to find the repository root (contains data/rulebook/)
        var dir = new DirectoryInfo(current);
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
