using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Seeds SharedGameEntity and GameEntity bridge records from the YAML manifest.
/// Manifest-driven replacement for legacy SharedGameSeeder.
/// </summary>
internal static class GameSeeder
{
    /// <summary>
    /// Seeds SharedGame + GameEntity bridge for each game in the manifest.
    /// Idempotent: skips games that already exist (by title or BggId).
    ///
    /// Issue #2123 — BGG ToS compliance: cover URLs are NEVER seeded inline.
    /// <see cref="SharedGameEntity.ImageUrl"/> / <see cref="SharedGameEntity.ThumbnailUrl"/>
    /// are set to <c>null</c> on every create path; covers are resolved at runtime
    /// by <see cref="Api.BoundedContexts.SharedGameCatalog.Application.Services.CoverUrlResolver"/>
    /// from R2-hosted assets (PDF, BGG-reuploaded, Wikidata) populated by the
    /// catalog enrichment pipeline (#1823 M3-M8).
    /// </summary>
    /// <returns>Dictionary mapping BggId to GameEntity.Id for downstream seeders.</returns>
    public static async Task<Dictionary<int, Guid>> SeedAsync(
        MeepleAiDbContext db,
        IBggApiService bggService,
        Guid systemUserId,
        SeedManifest manifest,
        ILogger logger,
        CancellationToken ct)
    {
        logger.LogInformation("GameSeeder: seeding {Count} games from manifest", manifest.Catalog.Games.Count);

        var gameMap = new Dictionary<int, Guid>();
        var seededCount = 0;
        var skippedCount = 0;
        var caches = new RelationshipCaches();

        foreach (var entry in manifest.Catalog.Games)
        {
            try
            {
                // Issue #2123: "enhanced" semantic now derived from presence of Description;
                // the legacy `bggEnhanced` boolean flag has been removed from the manifest model.
                var hasEnhancedMetadata = !string.IsNullOrWhiteSpace(entry.Description);

                // Check if already exists by title (case-insensitive) OR by BggId
                var existing = await db.SharedGames
                    .AsTracking()
                    .Include(g => g.Categories)
                    .Include(g => g.Mechanics)
                    .Include(g => g.Designers)
                    .Include(g => g.Publishers)
                    .FirstOrDefaultAsync(g =>
                        (EF.Functions.ILike(g.Title, entry.Title) ||
                         (entry.BggId.HasValue && entry.BggId > 0 && g.BggId == entry.BggId.Value))
                        && !g.IsDeleted, ct)
                    .ConfigureAwait(false);

                if (existing != null)
                {
                    if (hasEnhancedMetadata &&
                        existing.Categories.Count == 0 && existing.Mechanics.Count == 0 &&
                        existing.Designers.Count == 0 && existing.Publishers.Count == 0)
                    {
                        await RelationshipSeeder.SeedRelationshipsAsync(db, existing, entry, caches, logger, ct).ConfigureAwait(false);
                        await db.SaveChangesAsync(ct).ConfigureAwait(false);
                        logger.LogInformation("Backfilled relationships for existing game '{GameName}'", entry.Title);
                    }
                    else
                    {
                        logger.LogDebug("SharedGame '{GameName}' already exists, skipping", entry.Title);
                    }

                    // Post-Phase2d: gameMap now stores SharedGame.Id directly (no GameEntity bridge)
                    if (entry.BggId is > 0)
                        gameMap[entry.BggId.Value] = existing.Id;

                    skippedCount++;
                    continue;
                }

                // Create game from best available data source
                SharedGameEntity? sharedGame = null;

                if (hasEnhancedMetadata)
                {
                    sharedGame = CreateFromEnhancedData(entry, systemUserId);
                    logger.LogInformation("Created SharedGame '{GameName}' from enhanced YAML data", entry.Title);
                }
                else if (entry.BggId is > 0)
                {
                    var bggDetails = await bggService.GetGameDetailsAsync(entry.BggId.Value, ct)
                        .ConfigureAwait(false);

                    if (bggDetails != null)
                    {
                        sharedGame = CreateFromBggData(bggDetails, entry.Language, systemUserId);
                        logger.LogInformation("Created SharedGame '{GameName}' from BGG #{BggId}",
                            entry.Title, entry.BggId);
                    }
                }

                sharedGame ??= CreateMinimalGame(entry, systemUserId);

                db.SharedGames.Add(sharedGame);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                if (hasEnhancedMetadata)
                {
                    await RelationshipSeeder.SeedRelationshipsAsync(db, sharedGame, entry, caches, logger, ct).ConfigureAwait(false);
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                }

                // Post-Phase2d: gameMap now stores SharedGame.Id directly (no GameEntity bridge)
                if (entry.BggId is > 0)
                    gameMap[entry.BggId.Value] = sharedGame.Id;

                seededCount++;
                logger.LogInformation("Seeded SharedGame: {GameName} ({Language})", entry.Title, entry.Language);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to seed SharedGame for '{GameName}', continuing with others", entry.Title);
                // Clear the change tracker so any failed "Added" entity does not
                // leak into subsequent SaveChangesAsync calls.
                db.ChangeTracker.Clear();
                // Also clear relationship caches — detached entities cause identity conflicts
                // when re-attached in subsequent iterations after ChangeTracker.Clear().
                caches.Clear();
                skippedCount++;
            }
        }

        logger.LogInformation("GameSeeder completed: {Seeded} seeded, {Skipped} skipped", seededCount, skippedCount);
        return gameMap;
    }

    internal static SharedGameEntity CreateFromBggData(
        BggGameDetailsDto bgg,
        string rulesLanguage,
        Guid systemUserId)
    {
        return new SharedGameEntity
        {
            Id = GenerateDeterministicGameId(bgg.BggId, bgg.Name),
            BggId = bgg.BggId,
            Title = bgg.Name,
            YearPublished = bgg.YearPublished ?? DateTime.UtcNow.Year,
            Description = bgg.Description ?? $"Classic board game: {bgg.Name}",
            MinPlayers = bgg.MinPlayers ?? 2,
            MaxPlayers = bgg.MaxPlayers ?? 4,
            PlayingTimeMinutes = bgg.PlayingTime ?? bgg.MaxPlayTime ?? 60,
            MinAge = bgg.MinAge ?? 8,
            ComplexityRating = bgg.AverageWeight is > 0 ? (decimal)bgg.AverageWeight.Value : null,
            // Issue #2272: BGG returns AverageRating=0 for games with no votes; the Postgres
            // constraint chk_shared_games_rating accepts NULL or 1.0..10.0, so normalize 0→null.
            AverageRating = bgg.AverageRating is > 0 ? (decimal)bgg.AverageRating.Value : null,
            // Issue #2123: cover URLs are NEVER seeded inline. CoverUrlResolver
            // resolves at runtime from R2 (PDF / Wikidata) or returns null (FE renders placeholder).
            ImageUrl = null,
            ThumbnailUrl = null,
            Status = (int)GameStatus.Published,
            RulesLanguage = rulesLanguage,
            CreatedBy = systemUserId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    internal static SharedGameEntity CreateFromEnhancedData(
        SeedManifestGame entry,
        Guid systemUserId)
    {
        return new SharedGameEntity
        {
            Id = GenerateDeterministicGameId(entry.BggId, entry.Title),
            BggId = entry.BggId is > 0 ? entry.BggId.Value : null,
            Title = entry.Title,
            YearPublished = Math.Clamp(entry.YearPublished ?? 2020, 1901, 2100),
            Description = entry.Description ?? $"Classic board game: {entry.Title}",
            MinPlayers = entry.MinPlayers ?? 2,
            MaxPlayers = entry.MaxPlayers ?? 4,
            PlayingTimeMinutes = entry.PlayingTime ?? 60,
            MinAge = entry.MinAge ?? 10,
            ComplexityRating = entry.AverageWeight is > 0 ? (decimal)entry.AverageWeight.Value : null,
            // Issue #2272: same sentinel-0 normalization as CreateFromBggData (manifest YAML
            // may also carry averageRating: 0 for games with no votes).
            AverageRating = entry.AverageRating is > 0 ? (decimal)entry.AverageRating.Value : null,
            // Issue #2123: cover URLs are NEVER seeded inline. CoverUrlResolver
            // resolves at runtime from R2 (PDF / Wikidata) or returns null (FE renders placeholder).
            ImageUrl = null,
            ThumbnailUrl = null,
            RulesExternalUrl = entry.RulesUrl,
            Status = (int)GameStatus.Published,
            RulesLanguage = entry.Language,
            CreatedBy = systemUserId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    private static SharedGameEntity CreateMinimalGame(
        SeedManifestGame entry,
        Guid systemUserId)
    {
        return new SharedGameEntity
        {
            Id = GenerateDeterministicGameId(entry.BggId, entry.Title),
            BggId = entry.BggId is > 0 ? entry.BggId.Value : null,
            Title = entry.Title,
            YearPublished = 2020,
            Description = $"Classic board game: {entry.Title}. Rulebook available in {entry.Language.ToUpperInvariant()} language.",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ComplexityRating = null,
            AverageRating = null,
            // Issue #2123: cover URLs are NEVER seeded inline. CoverUrlResolver
            // resolves at runtime from R2 (PDF / Wikidata) or returns null (FE renders placeholder).
            ImageUrl = null,
            ThumbnailUrl = null,
            Status = (int)GameStatus.Published,
            RulesLanguage = entry.Language,
            CreatedBy = systemUserId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    /// <summary>
    /// Deterministic SharedGame id derived from the game's stable identity (bggId when present,
    /// otherwise the normalized title). Re-seeding after a DB wipe therefore reuses the same id,
    /// so PdfSeeder recognizes existing PDFs (no orphans, embeddings preserved) — Issue #2904 / #964.
    /// SHA-256 of a namespaced key truncated to 16 bytes; NOT for cryptographic use.
    /// </summary>
    internal static Guid GenerateDeterministicGameId(int? bggId, string title)
    {
        var key = bggId is > 0
            ? $"sharedgame:bgg:{bggId.Value}"
            : $"sharedgame:title:{(title ?? string.Empty).Trim().ToLowerInvariant()}";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
        return new Guid(hash[..16]);
    }
}
