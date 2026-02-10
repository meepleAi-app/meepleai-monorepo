using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for searching shared games with full-text search and filtering.
/// Uses PostgreSQL full-text search for optimal Italian language support.
/// Uses HybridCache (L1: 15min, L2: 1h) with query parameter hashing.
/// Issue #2371 Phase 2
/// </summary>
internal sealed class SearchSharedGamesQueryHandler : IRequestHandler<SearchSharedGamesQuery, PagedResult<SharedGameDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly ILogger<SearchSharedGamesQueryHandler> _logger;

    public SearchSharedGamesQueryHandler(
        MeepleAiDbContext context,
        HybridCache cache,
        ILogger<SearchSharedGamesQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<SharedGameDto>> Handle(SearchSharedGamesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Generate cache key from query parameters
        var cacheKey = GenerateCacheKey(query);

        _logger.LogInformation(
            "Searching shared games: SearchTerm={SearchTerm}, Categories={CategoryCount}, Mechanics={MechanicCount}, Page={Page}",
            query.SearchTerm,
            query.CategoryIds?.Count ?? 0,
            query.MechanicIds?.Count ?? 0,
            query.PageNumber);

        // Try cache first (L1: 15min, L2: 1h)
        return await _cache.GetOrCreateAsync<PagedResult<SharedGameDto>>(
            cacheKey,
            async cancel => await ExecuteSearchAsync(query, cancel).ConfigureAwait(false),
            new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromMinutes(15),  // L1
                Expiration = TimeSpan.FromHours(1)  // L2
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<PagedResult<SharedGameDto>> ExecuteSearchAsync(SearchSharedGamesQuery query, CancellationToken cancellationToken)
    {
        var dbQuery = _context.SharedGames.AsNoTracking();

        // Default filter: Published games only (unless specific Status is requested)
        if (query.Status is null)
        {
            dbQuery = dbQuery.Where(g => g.Status == (int)GameStatus.Published);
        }
        else
        {
            dbQuery = dbQuery.Where(g => g.Status == (int)query.Status);
        }

        // Full-text search: SearchTerm using PostgreSQL FTS with prefix matching
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm.Trim();

            // Build prefix tsquery: "wing span" → "wing:* & span:*"
            // This enables partial word matching (e.g. "wing" matches "Wingspan")
            var words = searchTerm
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(w => string.Concat(w.Where(c => char.IsLetterOrDigit(c))))
                .Where(w => w.Length > 0)
                .Select(w => w + ":*");
            var prefixQuery = string.Join(" & ", words);

            if (!string.IsNullOrEmpty(prefixQuery))
            {
                dbQuery = dbQuery.Where(g =>
                    EF.Functions.ToTsVector("italian", g.Title + " " + g.Description)
                        .Matches(EF.Functions.ToTsQuery("italian", prefixQuery)));
            }
        }

        // Category filter: Any match
        if (query.CategoryIds is not null && query.CategoryIds.Count > 0)
        {
            dbQuery = dbQuery.Where(g =>
                g.Categories.Any(c => query.CategoryIds.Contains(c.Id)));
        }

        // Mechanic filter: Any match
        if (query.MechanicIds is not null && query.MechanicIds.Count > 0)
        {
            dbQuery = dbQuery.Where(g =>
                g.Mechanics.Any(m => query.MechanicIds.Contains(m.Id)));
        }

        // Player count filter
        if (query.MinPlayers.HasValue)
        {
            dbQuery = dbQuery.Where(g => g.MaxPlayers >= query.MinPlayers.Value);
        }

        if (query.MaxPlayers.HasValue)
        {
            dbQuery = dbQuery.Where(g => g.MinPlayers <= query.MaxPlayers.Value);
        }

        // Playing time filter
        if (query.MaxPlayingTime.HasValue)
        {
            dbQuery = dbQuery.Where(g => g.PlayingTimeMinutes <= query.MaxPlayingTime.Value);
        }

        // Apply sorting
        dbQuery = query.SortBy switch
        {
            "YearPublished" => query.SortDescending
                ? dbQuery.OrderByDescending(g => g.YearPublished).ThenBy(g => g.Title)
                : dbQuery.OrderBy(g => g.YearPublished).ThenBy(g => g.Title),

            "AverageRating" => query.SortDescending
                ? dbQuery.OrderByDescending(g => g.AverageRating).ThenBy(g => g.Title)
                : dbQuery.OrderBy(g => g.AverageRating).ThenBy(g => g.Title),

            "CreatedAt" => query.SortDescending
                ? dbQuery.OrderByDescending(g => g.CreatedAt).ThenBy(g => g.Title)
                : dbQuery.OrderBy(g => g.CreatedAt).ThenBy(g => g.Title),

            _ => query.SortDescending
                ? dbQuery.OrderByDescending(g => g.Title)
                : dbQuery.OrderBy(g => g.Title)
        };

        // Pagination
        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var games = await dbQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(g => new SharedGameDto(
                g.Id,
                g.BggId,
                g.Title,
                g.YearPublished,
                g.Description,
                g.MinPlayers,
                g.MaxPlayers,
                g.PlayingTimeMinutes,
                g.MinAge,
                g.ComplexityRating,
                g.AverageRating,
                g.ImageUrl,
                g.ThumbnailUrl,
                (GameStatus)g.Status,
                g.CreatedAt,
                g.ModifiedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Search completed: Found {Count} games (Total: {Total}) for page {Page}",
            games.Count,
            total,
            query.PageNumber);

        return new PagedResult<SharedGameDto>(
            Items: games,
            Total: total,
            Page: query.PageNumber,
            PageSize: query.PageSize);
    }

    private static string GenerateCacheKey(SearchSharedGamesQuery query)
    {
        // Build cache key from all query parameters
        var categoryIds = query.CategoryIds is not null && query.CategoryIds.Count > 0
            ? string.Join(",", query.CategoryIds.OrderBy(x => x))
            : "none";

        var mechanicIds = query.MechanicIds is not null && query.MechanicIds.Count > 0
            ? string.Join(",", query.MechanicIds.OrderBy(x => x))
            : "none";

        var searchTerm = query.SearchTerm ?? "none";
        var statusStr = query.Status?.ToString() ?? "null";

        // Create compact hash for long parameter combinations
        var keyComponents = $"{searchTerm}|{categoryIds}|{mechanicIds}|{query.MinPlayers}|{query.MaxPlayers}|{query.MaxPlayingTime}|{statusStr}|{query.PageNumber}|{query.PageSize}|{query.SortBy}|{query.SortDescending}";

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyComponents)));

        return $"search-games:{hash}";
    }
}
