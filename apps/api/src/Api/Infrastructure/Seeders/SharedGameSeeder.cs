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
        ["7-wonders_rulebook.pdf"] = new("7 Wonders", 68448, "en",
            "https://cf.geekdo-images.com/35h9Za_JvMMMtx_92kT0Jg__original/img/jt70jJDZ1y1FWJs4ZQf5FI8APVY=/0x0/filters:format(jpeg)/pic7149798.jpg",
            "https://cf.geekdo-images.com/35h9Za_JvMMMtx_92kT0Jg__small/img/BUOso8b0M1aUOkU80FWlhE8uuxc=/fit-in/200x150/filters:strip_icc()/pic7149798.jpg"),
        ["agricola_rulebook.pdf"] = new("Agricola", 31260, "en",
            "https://cf.geekdo-images.com/3L6ZtOll9W5O6-3-EwSMyw__original/img/V37KuMJlCzpxAilzN39BzeLvc9Q=/0x0/filters:format(jpeg)/pic1899157.jpg",
            "https://cf.geekdo-images.com/3L6ZtOll9W5O6-3-EwSMyw__small/img/TpNF65wCIb1n3EGW18eJJ3MlRMo=/fit-in/200x150/filters:strip_icc()/pic1899157.jpg"),
        ["azul_rulebook.pdf"] = new("Azul", 230802, "en",
            "https://cf.geekdo-images.com/aPSHJO0d0XOpQR5X-wJonw__original/img/AkbtYVc6xXJF3c9EUrakklcclKw=/0x0/filters:format(png)/pic6973671.png",
            "https://cf.geekdo-images.com/aPSHJO0d0XOpQR5X-wJonw__small/img/ccsXKrdGJw-YSClWwzVUwk5Nh9Y=/fit-in/200x150/filters:strip_icc()/pic6973671.png"),
        ["catan_rulebook.pdf"] = new("Catan", 13, "en",
            "https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__original/img/IwRwEpu1I6YfkyYjFIekCh80ntc=/0x0/filters:format(jpeg)/pic2419375.jpg",
            "https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__small/img/7a0LOL48K-2lC3IG0HyYT3XxJBs=/fit-in/200x150/filters:strip_icc()/pic2419375.jpg"),
        ["carcassone_rulebook.pdf"] = new("Carcassonne", 822, "en",
            "https://cf.geekdo-images.com/peUgu3A20LRmAXAMyDQfpQ__original/img/bP18m_PYjyFOv1IBGgMOteQUneA=/0x0/filters:format(jpeg)/pic8621446.jpg",
            "https://cf.geekdo-images.com/peUgu3A20LRmAXAMyDQfpQ__small/img/oEEslN-EGqh82sNI6Aj4_MFXYg0=/fit-in/200x150/filters:strip_icc()/pic8621446.jpg"),
        ["pandemic_rulebook.pdf"] = new("Pandemic", 30549, "en",
            "https://cf.geekdo-images.com/S3ybV1LAp-8SnHIXLLjVqA__original/img/IsrvRLpUV1TEyZsO5rC-btXaPz0=/0x0/filters:format(jpeg)/pic1534148.jpg",
            "https://cf.geekdo-images.com/S3ybV1LAp-8SnHIXLLjVqA__small/img/oqViRj6nVxK3m36NluTxU1PZkrk=/fit-in/200x150/filters:strip_icc()/pic1534148.jpg"),
        ["scacchi-fide_2017_rulebook.pdf"] = new("Chess", null, "it", null, null), // Chess not on BGG as modern game
        ["splendor_rulebook.pdf"] = new("Splendor", 148228, "en",
            "https://cf.geekdo-images.com/vNFe4JkhKAERzi4T0Ntwpw__original/img/rqcUdtu_N4v-SpI96XVmpYHnJww=/0x0/filters:format(png)/pic8234167.png",
            "https://cf.geekdo-images.com/vNFe4JkhKAERzi4T0Ntwpw__small/img/KKU_42Uswt4tKCpf1zY5kTzgr-g=/fit-in/200x150/filters:strip_icc()/pic8234167.png"),
        ["ticket-to-ride_rulebook.pdf"] = new("Ticket to Ride", 9209, "en",
            "https://cf.geekdo-images.com/kdWYkW-7AqG63HhqPL6ekA__original/img/rWF8r4JXXCQQ7QhiWHhmT-rQ3Pc=/0x0/filters:format(jpeg)/pic8937637.jpg",
            "https://cf.geekdo-images.com/kdWYkW-7AqG63HhqPL6ekA__small/img/5G46jv8MFh_BfX67iMSouTMhKxc=/fit-in/200x150/filters:strip_icc()/pic8937637.jpg"),
        ["wingspan_en_rulebook.pdf"] = new("Wingspan", 266192, "en",
            "https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__original/img/cI782Zis9cT66j2MjSHKJGnFPNw=/0x0/filters:format(jpeg)/pic4458123.jpg",
            "https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__small/img/VNToqgS2-pOGU6MuvIkMPKn_y-s=/fit-in/200x150/filters:strip_icc()/pic4458123.jpg"),
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
                // Check if already exists by title (case-insensitive) OR by BggId
                // BGG API may return titles in different casing (e.g., "CATAN" vs "Catan")
                var existing = await db.SharedGames
                    .AsTracking()
                    .FirstOrDefaultAsync(g =>
                        (EF.Functions.ILike(g.Title, gameData.Name) ||
                         (gameData.BggId.HasValue && g.BggId == gameData.BggId.Value))
                        && !g.IsDeleted, cancellationToken)
                    .ConfigureAwait(false);

                if (existing != null)
                {
                    // Update image URLs if they're still placeholders
                    if (existing.ImageUrl.Contains("placehold.co") && gameData.FallbackImageUrl != null)
                    {
                        existing.ImageUrl = gameData.FallbackImageUrl;
                        existing.ThumbnailUrl = gameData.FallbackThumbnailUrl ?? existing.ThumbnailUrl;
                        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                        logger.LogInformation("🖼️  Updated placeholder images for '{GameName}' with BGG images", gameData.Name);
                    }
                    else
                    {
                        logger.LogDebug("⏭️  SharedGame '{GameName}' already exists, skipping", gameData.Name);
                    }
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
            ImageUrl = data.FallbackImageUrl ?? $"{PlaceholderImageBase}/400x300?text=" + Uri.EscapeDataString(data.Name),
            ThumbnailUrl = data.FallbackThumbnailUrl ?? $"{PlaceholderImageBase}/150x150?text=" + Uri.EscapeDataString(data.Name),
            Status = (int)GameStatus.Published,
            RulesLanguage = data.Language,
            CreatedBy = systemUserId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    private sealed record GameSeedData(string Name, int? BggId, string Language, string? FallbackImageUrl = null, string? FallbackThumbnailUrl = null);
}
