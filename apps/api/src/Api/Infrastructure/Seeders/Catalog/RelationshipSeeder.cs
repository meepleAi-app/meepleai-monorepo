using System.Text.RegularExpressions;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Seeds many-to-many relationships (categories, mechanics, designers, publishers)
/// for SharedGameEntity records from manifest data.
/// Uses dictionary caches + DB lookup with case-insensitive matching for deduplication.
/// </summary>
internal static partial class RelationshipSeeder
{
    /// <summary>
    /// Seeds all four relationship types for a single game from its manifest entry.
    /// </summary>
    public static async Task SeedRelationshipsAsync(
        MeepleAiDbContext db,
        SharedGameEntity game,
        SeedManifestGame entry,
        RelationshipCaches caches,
        ILogger logger,
        CancellationToken ct)
    {
        if (entry.Categories is { Count: > 0 })
        {
            foreach (var name in entry.Categories)
            {
                var entity = await GetOrCreateCategoryAsync(db, name, caches, ct).ConfigureAwait(false);
                if (!game.Categories.Any(c => c.Id == entity.Id))
                    game.Categories.Add(entity);
            }

            logger.LogDebug("Linked {Count} categories to '{Game}'", entry.Categories.Count, game.Title);
        }

        if (entry.Mechanics is { Count: > 0 })
        {
            foreach (var name in entry.Mechanics)
            {
                var entity = await GetOrCreateMechanicAsync(db, name, caches, ct).ConfigureAwait(false);
                if (!game.Mechanics.Any(m => m.Id == entity.Id))
                    game.Mechanics.Add(entity);
            }

            logger.LogDebug("Linked {Count} mechanics to '{Game}'", entry.Mechanics.Count, game.Title);
        }

        if (entry.Designers is { Count: > 0 })
        {
            foreach (var name in entry.Designers)
            {
                var entity = await GetOrCreateDesignerAsync(db, name, caches, ct).ConfigureAwait(false);
                if (!game.Designers.Any(d => d.Id == entity.Id))
                    game.Designers.Add(entity);
            }

            logger.LogDebug("Linked {Count} designers to '{Game}'", entry.Designers.Count, game.Title);
        }

        if (entry.Publishers is { Count: > 0 })
        {
            foreach (var name in entry.Publishers)
            {
                var entity = await GetOrCreatePublisherAsync(db, name, caches, ct).ConfigureAwait(false);
                if (!game.Publishers.Any(p => p.Id == entity.Id))
                    game.Publishers.Add(entity);
            }

            logger.LogDebug("Linked {Count} publishers to '{Game}'", entry.Publishers.Count, game.Title);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    internal static async Task<GameCategoryEntity> GetOrCreateCategoryAsync(
        MeepleAiDbContext db,
        string name,
        RelationshipCaches caches,
        CancellationToken ct)
    {
        var trimmed = name.Trim();

        if (caches.Categories.TryGetValue(trimmed, out var cached))
            return cached;

        var existing = await db.Set<GameCategoryEntity>()
            .FirstOrDefaultAsync(c => EF.Functions.ILike(c.Name, trimmed), ct)
            .ConfigureAwait(false);

        if (existing != null)
        {
            caches.Categories[trimmed] = existing;
            return existing;
        }

        var entity = new GameCategoryEntity
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            Slug = GenerateSlug(trimmed),
            CreatedAt = DateTime.UtcNow
        };

        db.Set<GameCategoryEntity>().Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        caches.Categories[trimmed] = entity;
        return entity;
    }

    internal static async Task<GameMechanicEntity> GetOrCreateMechanicAsync(
        MeepleAiDbContext db,
        string name,
        RelationshipCaches caches,
        CancellationToken ct)
    {
        var trimmed = name.Trim();

        if (caches.Mechanics.TryGetValue(trimmed, out var cached))
            return cached;

        var existing = await db.Set<GameMechanicEntity>()
            .FirstOrDefaultAsync(m => EF.Functions.ILike(m.Name, trimmed), ct)
            .ConfigureAwait(false);

        if (existing != null)
        {
            caches.Mechanics[trimmed] = existing;
            return existing;
        }

        var entity = new GameMechanicEntity
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            Slug = GenerateSlug(trimmed),
            CreatedAt = DateTime.UtcNow
        };

        db.Set<GameMechanicEntity>().Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        caches.Mechanics[trimmed] = entity;
        return entity;
    }

    internal static async Task<GameDesignerEntity> GetOrCreateDesignerAsync(
        MeepleAiDbContext db,
        string name,
        RelationshipCaches caches,
        CancellationToken ct)
    {
        var trimmed = name.Trim();

        if (caches.Designers.TryGetValue(trimmed, out var cached))
            return cached;

        var existing = await db.Set<GameDesignerEntity>()
            .FirstOrDefaultAsync(d => EF.Functions.ILike(d.Name, trimmed), ct)
            .ConfigureAwait(false);

        if (existing != null)
        {
            caches.Designers[trimmed] = existing;
            return existing;
        }

        var entity = new GameDesignerEntity
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            CreatedAt = DateTime.UtcNow
        };

        db.Set<GameDesignerEntity>().Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        caches.Designers[trimmed] = entity;
        return entity;
    }

    internal static async Task<GamePublisherEntity> GetOrCreatePublisherAsync(
        MeepleAiDbContext db,
        string name,
        RelationshipCaches caches,
        CancellationToken ct)
    {
        var trimmed = name.Trim();

        if (caches.Publishers.TryGetValue(trimmed, out var cached))
            return cached;

        var existing = await db.Set<GamePublisherEntity>()
            .FirstOrDefaultAsync(p => EF.Functions.ILike(p.Name, trimmed), ct)
            .ConfigureAwait(false);

        if (existing != null)
        {
            caches.Publishers[trimmed] = existing;
            return existing;
        }

        var entity = new GamePublisherEntity
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            CreatedAt = DateTime.UtcNow
        };

        db.Set<GamePublisherEntity>().Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        caches.Publishers[trimmed] = entity;
        return entity;
    }

    /// <summary>
    /// Converts a human-readable name into a URL-friendly slug.
    /// Used for categories and mechanics (designers/publishers don't have a Slug field).
    /// </summary>
    internal static string GenerateSlug(string name)
    {
        var slug = name.Trim().ToLowerInvariant();
        slug = DashLikeCharRegex().Replace(slug, "-");
        slug = NonAlphanumericRegex().Replace(slug, "");
        slug = WhitespaceRegex().Replace(slug, "-");
        slug = ConsecutiveHyphensRegex().Replace(slug, "-");
        return slug.Trim('-');
    }

    [GeneratedRegex("[\\u2013\\u2014\\u2015]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DashLikeCharRegex();

    [GeneratedRegex("[^a-z0-9\\s-]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex("\\s+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("-+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ConsecutiveHyphensRegex();
}

/// <summary>
/// Holds in-memory caches for relationship entities to minimize DB lookups during seeding.
/// Keys are compared case-insensitively.
/// </summary>
internal sealed class RelationshipCaches
{
    public Dictionary<string, GameCategoryEntity> Categories { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, GameMechanicEntity> Mechanics { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, GameDesignerEntity> Designers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, GamePublisherEntity> Publishers { get; } = new(StringComparer.OrdinalIgnoreCase);
}
