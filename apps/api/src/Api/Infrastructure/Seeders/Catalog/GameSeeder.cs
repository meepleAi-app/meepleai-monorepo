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
#pragma warning disable S1075 // URIs should not be hardcoded - Acceptable for placeholder images in seed data
    private const string PlaceholderImageBase = "https://placehold.co";
#pragma warning restore S1075

    /// <summary>
    /// Seeds SharedGame + GameEntity bridge for each game in the manifest.
    /// Idempotent: skips games that already exist (by title or BggId).
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

        foreach (var entry in manifest.Catalog.Games)
        {
            try
            {
                // Check if already exists by title (case-insensitive) OR by BggId
                var existing = await db.SharedGames
                    .AsTracking()
                    .FirstOrDefaultAsync(g =>
                        (EF.Functions.ILike(g.Title, entry.Title) ||
                         (entry.BggId.HasValue && entry.BggId > 0 && g.BggId == entry.BggId.Value))
                        && !g.IsDeleted, ct)
                    .ConfigureAwait(false);

                if (existing != null)
                {
                    // Update image URLs if they're still placeholders
                    if (existing.ImageUrl.Contains("placehold.co") && entry.FallbackImageUrl != null)
                    {
                        existing.ImageUrl = entry.FallbackImageUrl;
                        existing.ThumbnailUrl = entry.FallbackThumbnailUrl ?? existing.ThumbnailUrl;
                        await db.SaveChangesAsync(ct).ConfigureAwait(false);
                        logger.LogInformation("Updated placeholder images for '{GameName}' with manifest images", entry.Title);
                    }
                    else
                    {
                        logger.LogDebug("SharedGame '{GameName}' already exists, skipping", entry.Title);
                    }

                    // Ensure GameEntity bridge exists and record in map
                    var existingBridge = await EnsureGameEntityBridgeAsync(db, existing, entry, systemUserId, ct)
                        .ConfigureAwait(false);
                    if (entry.BggId is > 0)
                        gameMap[entry.BggId.Value] = existingBridge.Id;

                    skippedCount++;
                    continue;
                }

                // Fetch metadata from BGG if BGG ID provided
                SharedGameEntity? sharedGame = null;

                if (entry.BggId is > 0)
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

                // Fallback: Create with minimal data if BGG lookup failed or no valid BGG ID
                sharedGame ??= CreateMinimalGame(entry, systemUserId);

                db.SharedGames.Add(sharedGame);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                // Create GameEntity bridge
                var bridge = await EnsureGameEntityBridgeAsync(db, sharedGame, entry, systemUserId, ct)
                    .ConfigureAwait(false);
                if (entry.BggId is > 0)
                    gameMap[entry.BggId.Value] = bridge.Id;

                seededCount++;
                logger.LogInformation("Seeded SharedGame: {GameName} ({Language})", entry.Title, entry.Language);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to seed SharedGame for '{GameName}', continuing with others", entry.Title);
                // Clear the change tracker so any failed "Added" entity does not
                // leak into subsequent SaveChangesAsync calls.
                db.ChangeTracker.Clear();
                skippedCount++;
            }
        }

        logger.LogInformation("GameSeeder completed: {Seeded} seeded, {Skipped} skipped", seededCount, skippedCount);
        return gameMap;
    }

    private static async Task<GameEntity> EnsureGameEntityBridgeAsync(
        MeepleAiDbContext db,
        SharedGameEntity sharedGame,
        SeedManifestGame entry,
        Guid systemUserId,
        CancellationToken ct)
    {
        var existingBridge = await db.Games
            .FirstOrDefaultAsync(g => g.SharedGameId == sharedGame.Id, ct)
            .ConfigureAwait(false);

        if (existingBridge != null)
            return existingBridge;

        var bridge = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = sharedGame.Title,
            CreatedAt = DateTime.UtcNow,
            YearPublished = sharedGame.YearPublished,
            MinPlayers = sharedGame.MinPlayers,
            MaxPlayers = sharedGame.MaxPlayers,
            MinPlayTimeMinutes = sharedGame.PlayingTimeMinutes,
            MaxPlayTimeMinutes = sharedGame.PlayingTimeMinutes,
            BggId = sharedGame.BggId,
            ImageUrl = sharedGame.ImageUrl,
            IconUrl = sharedGame.ThumbnailUrl,
            Language = entry.Language,
            VersionType = "base",
            VersionNumber = "1.0",
            SharedGameId = sharedGame.Id,
            IsPublished = true,
            ApprovalStatus = (int)GameStatus.Published
        };

        db.Games.Add(bridge);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return bridge;
    }

    private static SharedGameEntity CreateFromBggData(
        BggGameDetailsDto bgg,
        string rulesLanguage,
        Guid systemUserId)
    {
        return new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            BggId = bgg.BggId,
            Title = bgg.Name,
            YearPublished = bgg.YearPublished ?? DateTime.UtcNow.Year,
            Description = bgg.Description ?? $"Classic board game: {bgg.Name}",
            MinPlayers = bgg.MinPlayers ?? 2,
            MaxPlayers = bgg.MaxPlayers ?? 4,
            PlayingTimeMinutes = bgg.PlayingTime ?? bgg.MaxPlayTime ?? 60,
            MinAge = bgg.MinAge ?? 8,
            ComplexityRating = bgg.AverageWeight.HasValue ? (decimal)bgg.AverageWeight.Value : null,
            AverageRating = bgg.AverageRating.HasValue ? (decimal)bgg.AverageRating.Value : null,
            ImageUrl = bgg.ImageUrl ?? $"{PlaceholderImageBase}/400x300?text=No+Image",
            ThumbnailUrl = bgg.ThumbnailUrl ?? $"{PlaceholderImageBase}/150x150?text=No+Image",
            Status = (int)GameStatus.Published,
            RulesLanguage = rulesLanguage,
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
            Id = Guid.NewGuid(),
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
            ImageUrl = entry.FallbackImageUrl ?? $"{PlaceholderImageBase}/400x300?text=" + Uri.EscapeDataString(entry.Title),
            ThumbnailUrl = entry.FallbackThumbnailUrl ?? $"{PlaceholderImageBase}/150x150?text=" + Uri.EscapeDataString(entry.Title),
            Status = (int)GameStatus.Published,
            RulesLanguage = entry.Language,
            CreatedBy = systemUserId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}
