using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders;

/// <summary>
/// Seeds SharedGameCatalog with games from available PDF rulebooks.
/// Only processes PDFs ≤ 10MB to avoid memory issues during initial seed.
/// </summary>
internal static class SharedGameSeeder
{
    private static readonly Dictionary<string, GameSeedData> GameMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["7-wonders_rulebook.pdf"] = new("7 Wonders", 68448, "en"), // Fixed: BGG #13 is CATAN, #68448 is 7 Wonders
        ["agricola_rulebook.pdf"] = new("Agricola", 31260, "en"),
        ["azul_rulebook.pdf"] = new("Azul", 230802, "en"),
        ["carcassone_rulebook.pdf"] = new("Carcassonne", 822, "en"),
        ["pandemic_rulebook.pdf"] = new("Pandemic", 30549, "en"),
        ["scacchi-fide_2017_rulebook.pdf"] = new("Chess", null, "it"), // Chess not on BGG as modern game
        ["splendor_rulebook.pdf"] = new("Splendor", 148228, "en"),
        ["ticket-to-ride_rulebook.pdf"] = new("Ticket to Ride", 9209, "en"),
        ["wingspan_en_rulebook.pdf"] = new("Wingspan", 266192, "en"),
    };

    public static async Task SeedSharedGamesAsync(
        MeepleAiDbContext db,
        IBggApiService bggService,
        Guid systemUserId,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("🌱 Starting SharedGame seed from PDF rulebooks (≤ 10MB)");

        var seededCount = 0;
        var skippedCount = 0;

        foreach (var (pdfFileName, gameData) in GameMappings)
        {
            try
            {
                // Check if already exists
                var exists = await db.SharedGames
                    .AnyAsync(g => g.Title == gameData.Name && !g.IsDeleted, cancellationToken)
                    .ConfigureAwait(false);

                if (exists)
                {
                    logger.LogDebug("⏭️  SharedGame '{GameName}' already exists, skipping", gameData.Name);
                    skippedCount++;
                    continue;
                }

                // Fetch metadata from BGG if BGG ID provided
                SharedGameEntity? sharedGame = null;

                if (gameData.BggId.HasValue)
                {
                    var bggDetails = await bggService.GetGameDetailsAsync(gameData.BggId.Value, cancellationToken)
                        .ConfigureAwait(false);

                    if (bggDetails != null)
                    {
                        sharedGame = CreateFromBggData(bggDetails, gameData.Language, systemUserId);
                        logger.LogInformation("✅ Created SharedGame '{GameName}' from BGG #{BggId}",
                            gameData.Name, gameData.BggId);
                    }
                }

                // Fallback: Create with minimal data if BGG lookup failed or no BGG ID
                sharedGame ??= CreateMinimalGame(gameData, systemUserId);

                db.SharedGames.Add(sharedGame);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                seededCount++;
                logger.LogInformation("🎲 Seeded SharedGame: {GameName} ({Language})", gameData.Name, gameData.Language);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "⚠️  Failed to seed SharedGame for {PdfFile}, continuing with others", pdfFileName);
                skippedCount++;
            }
        }

        logger.LogInformation("🌱 SharedGame seed completed: {Seeded} seeded, {Skipped} skipped", seededCount, skippedCount);
    }

    private static SharedGameEntity CreateFromBggData(
        BggGameDetailsDto bgg,
        string rulesLanguage,
        Guid systemUserId)
    {
#pragma warning disable S1075 // URIs should not be hardcoded - Acceptable for placeholder images when BGG data missing
        const string PlaceholderImageBase = "https://placehold.co";
#pragma warning restore S1075

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
        GameSeedData data,
        Guid systemUserId)
    {
#pragma warning disable S1075 // URIs should not be hardcoded - Acceptable for placeholder images in seed data
        const string PlaceholderImageBase = "https://placehold.co";
#pragma warning restore S1075

        return new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            BggId = data.BggId,
            Title = data.Name,
            YearPublished = 2020, // Default year for games without BGG data
            Description = $"Classic board game: {data.Name}. Rulebook available in {data.Language.ToUpperInvariant()} language.",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ComplexityRating = null,
            AverageRating = null,
            ImageUrl = $"{PlaceholderImageBase}/400x300?text=" + Uri.EscapeDataString(data.Name),
            ThumbnailUrl = $"{PlaceholderImageBase}/150x150?text=" + Uri.EscapeDataString(data.Name),
            Status = (int)GameStatus.Published,
            RulesLanguage = data.Language,
            CreatedBy = systemUserId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    private sealed record GameSeedData(string Name, int? BggId, string Language);
}
