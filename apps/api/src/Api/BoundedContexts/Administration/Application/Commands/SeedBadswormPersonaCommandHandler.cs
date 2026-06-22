using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for SeedBadswormPersonaCommand.
///
/// Populates 10 library entries for badsworm@gmail.com (project owner, persona used
/// for the Nanolith libro-game dogfood demo) plus mocked PdfDocument rows with
/// distinct ProcessingState values so the UI can render every KB lifecycle state.
///
/// Library composition:
///   1. Nanolith            — KB Ready  (libro game, hero use case)
///   2. Catan               — KB Ready
///   3. Root                — KB Ready
///   4. Wingspan            — KB Ready
///   5. Pandemic            — KB Ready
///   6. Ark Nova            — KB Ready
///   7. 7 Wonders           — KB Embedding (processing)
///   8. Spirit Island       — KB Pending   (uploaded, not started)
///   9. Terraforming Mars   — no KB
///  10. Brass: Birmingham   — no KB
///
/// Idempotency:
///   - skip if badsworm user not found
///   - skip if badsworm already has ≥10 library entries
///   - per-game: insert library entry only if missing (lookup by (UserId, SharedGameId))
///   - per-pdf:  insert mock document only if missing (lookup by (UploadedByUserId, SharedGameId))
///
/// The PdfDocument rows are mock placeholders (no real file blob). They drive the
/// dashboard "GIOCHI" section count and the KB-state visual cues; full RAG queries
/// against these games will return empty results until the embedding pipeline runs.
/// </summary>
internal sealed class SeedBadswormPersonaCommandHandler : ICommandHandler<SeedBadswormPersonaCommand>
{
    private const string BadswormEmail = "badsworm@gmail.com";
    private const int OwnedStateInt = 3; // GameStateType.Owned
    private const int ExpectedLibrarySize = 10;

    private static readonly LibraryGameSpec[] LibrarySpec =
    {
        new("Nanolith",            "Ready",     "it"),
        new("Catan",               "Ready",     "en"),
        new("Root",                "Ready",     "en"),
        new("Wingspan",            "Ready",     "en"),
        new("Pandemic",            "Ready",     "en"),
        new("Ark Nova",            "Ready",     "en"),
        new("7 Wonders",           "Embedding", "en"),
        new("Spirit Island",       "Pending",   "en"),
        new("Terraforming Mars",   null,        "en"),
        new("Brass: Birmingham",   null,        "en"),
    };

    private readonly MeepleAiDbContext _db;
    private readonly ILogger<SeedBadswormPersonaCommandHandler> _logger;

    public SeedBadswormPersonaCommandHandler(
        MeepleAiDbContext db,
        ILogger<SeedBadswormPersonaCommandHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SeedBadswormPersonaCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var badsworm = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == BadswormEmail, cancellationToken)
            .ConfigureAwait(false);

        if (badsworm == null)
        {
            _logger.LogInformation(
                "Badsworm user not found — skipping persona seed (run SeedBadswormUser first)");
            return;
        }

        var existingCount = await _db.UserLibraryEntries
            .CountAsync(e => e.UserId == badsworm.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existingCount >= ExpectedLibrarySize)
        {
            _logger.LogInformation(
                "Badsworm persona already has {Count} library entries — skipping",
                existingCount);
            return;
        }

        var libraryAdded = 0;
        var pdfsAdded = 0;
        var missingGames = new List<string>();

        foreach (var spec in LibrarySpec)
        {
            var sharedGame = await _db.SharedGames
                .Where(g => !g.IsDeleted && g.Title == spec.Title)
                .Select(g => new { g.Id, g.Title })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (sharedGame == null)
            {
                missingGames.Add(spec.Title);
                continue;
            }

            var entryExists = await _db.UserLibraryEntries
                .AnyAsync(
                    e => e.UserId == badsworm.Id && e.SharedGameId == sharedGame.Id,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!entryExists)
            {
                _db.UserLibraryEntries.Add(new UserLibraryEntryEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = badsworm.Id,
                    SharedGameId = sharedGame.Id,
                    AddedAt = DateTime.UtcNow,
                    CurrentState = OwnedStateInt,
                    StateChangedAt = DateTime.UtcNow,
                    OwnershipDeclaredAt = DateTime.UtcNow,
                });
                libraryAdded++;
            }

            if (spec.PdfState is null)
            {
                continue;
            }

            var pdfExists = await _db.PdfDocuments
                .AnyAsync(
                    p => p.UploadedByUserId == badsworm.Id && p.SharedGameId == sharedGame.Id,
                    cancellationToken)
                .ConfigureAwait(false);

            if (pdfExists)
            {
                continue;
            }

            var isReady = string.Equals(spec.PdfState, "Ready", StringComparison.Ordinal);
            _db.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = Guid.NewGuid(),
                FileName = $"rulebook-{Slugify(spec.Title)}.pdf",
                FilePath = $"seed/badsworm/{Slugify(spec.Title)}/rulebook.pdf",
                FileSizeBytes = 250_000,
                ContentType = "application/pdf",
                UploadedByUserId = badsworm.Id,
                UploadedAt = DateTime.UtcNow,
                SharedGameId = sharedGame.Id,
                ProcessingState = spec.PdfState,
                ProcessedAt = isReady ? DateTime.UtcNow : null,
                IsActiveForRag = isReady,
                Language = spec.Language,
                IsPublic = false,
                DocumentCategory = "Rulebook",
                LicenseType = 0,
                ProcessingPriority = "Normal",
            });
            pdfsAdded++;
        }

        if (libraryAdded == 0 && pdfsAdded == 0)
        {
            _logger.LogInformation(
                "Badsworm persona seed: no changes (library entries={Count}, pdfs already present)",
                existingCount);
        }
        else
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Badsworm persona seeded: +{Library} library entries, +{Pdfs} pdf documents",
                libraryAdded, pdfsAdded);
        }

        if (missingGames.Count > 0)
        {
            _logger.LogWarning(
                "Badsworm persona seed: {Count} games missing from catalog (run CatalogSeeder first): {Titles}",
                missingGames.Count,
                string.Join(", ", missingGames));
        }
    }

    private static string Slugify(string title)
    {
        return title
            .Replace(' ', '-')
            .Replace(':', '-')
            .Replace("--", "-")
            .ToLowerInvariant();
    }

    private sealed record LibraryGameSpec(string Title, string? PdfState, string Language);
}
