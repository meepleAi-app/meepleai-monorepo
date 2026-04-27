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
using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for searching shared games with full-text search and filtering.
/// Uses PostgreSQL full-text search for optimal Italian language support.
/// Uses HybridCache (L1: 15min, L2: 1h) with query parameter hashing.
/// Issue #2371 Phase 2
/// </summary>
internal sealed class SearchSharedGamesQueryHandler : IRequestHandler<SearchSharedGamesQuery, PagedResult<SharedGameDto>>
{
    /// <summary>
    /// Default IsTopRated threshold when <c>SharedGameCatalog:TopRatedThreshold</c>
    /// is missing from configuration. Spec §9 decision 1 (Issue #593).
    /// </summary>
    private const decimal DefaultTopRatedThreshold = 4.5m;

    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly ILogger<SearchSharedGamesQueryHandler> _logger;
    private readonly decimal _topRatedThreshold;

    public SearchSharedGamesQueryHandler(
        MeepleAiDbContext context,
        HybridCache cache,
        IConfiguration configuration,
        ILogger<SearchSharedGamesQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        ArgumentNullException.ThrowIfNull(configuration);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Issue #593: top-rated threshold is runtime-tunable via IConfiguration.
        // Resolved once at construction (handler is a transient/scoped service per
        // MediatR conventions, so re-reading per request would just be wasteful).
        _topRatedThreshold = configuration.GetValue("SharedGameCatalog:TopRatedThreshold", DefaultTopRatedThreshold);
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

        // Try cache first (L1: 15min, L2: 1h).
        // Tagged "search-games" so event handlers (e.g. VectorDocumentIndexedForKbFlagHandler)
        // can invalidate the whole namespace on relevant domain events.
        return await _cache.GetOrCreateAsync<PagedResult<SharedGameDto>>(
            cacheKey,
            async cancel => await ExecuteSearchAsync(query, cancel).ConfigureAwait(false),
            new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromMinutes(15),  // L1
                Expiration = TimeSpan.FromHours(1)  // L2
            },
            tags: _searchGamesTags,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Cache tag used to invalidate all SearchSharedGamesQueryHandler entries
    /// when the underlying projection changes (e.g. HasKnowledgeBase flip).
    /// </summary>
    private static readonly IReadOnlyList<string> _searchGamesTags = new[] { "search-games" };

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

        // Complexity rating filter
        if (query.MinComplexity.HasValue)
        {
            dbQuery = dbQuery.Where(g => g.ComplexityRating >= query.MinComplexity.Value);
        }

        if (query.MaxComplexity.HasValue)
        {
            dbQuery = dbQuery.Where(g => g.ComplexityRating <= query.MaxComplexity.Value);
        }

        // S2 — Knowledge Base filter: only games with indexed KB
        if (query.HasKnowledgeBase.HasValue)
        {
            dbQuery = dbQuery.Where(g => g.HasKnowledgeBase == query.HasKnowledgeBase.Value);
        }

        // Issue #593 (Wave A.3a) — chip filters from `sp3-shared-games.jsx`.
        // ApprovalStatus.Approved == 2 (matches projection block constants below).
        // Cross-BC sub-queries via Game intermediate (Toolkits/AgentDefinitions
        // both link to GameEntity, not directly to SharedGame).
        const int ApprovedStatus = 2;

        if (query.HasToolkit.HasValue)
        {
            // chip "with-toolkit": at least one *non-default* Toolkit (BR-02 from
            // Issue #5144 — exclude the read-only system default) tied to any
            // approved Game of this SharedGame.
            if (query.HasToolkit.Value)
            {
                dbQuery = dbQuery.Where(g => _context.Toolkits.Any(t =>
                    !t.IsDefault &&
                    _context.Games.Any(game =>
                        game.Id == t.GameId &&
                        game.SharedGameId == g.Id &&
                        game.ApprovalStatus == ApprovedStatus)));
            }
            else
            {
                dbQuery = dbQuery.Where(g => !_context.Toolkits.Any(t =>
                    !t.IsDefault &&
                    _context.Games.Any(game =>
                        game.Id == t.GameId &&
                        game.SharedGameId == g.Id &&
                        game.ApprovalStatus == ApprovedStatus)));
            }
        }

        if (query.HasAgent.HasValue)
        {
            // chip "with-agent": at least one AgentDefinition tied to any approved
            // Game of this SharedGame.
            if (query.HasAgent.Value)
            {
                dbQuery = dbQuery.Where(g => _context.AgentDefinitions.Any(a =>
                    a.GameId.HasValue &&
                    _context.Games.Any(game =>
                        game.Id == a.GameId &&
                        game.SharedGameId == g.Id &&
                        game.ApprovalStatus == ApprovedStatus)));
            }
            else
            {
                dbQuery = dbQuery.Where(g => !_context.AgentDefinitions.Any(a =>
                    a.GameId.HasValue &&
                    _context.Games.Any(game =>
                        game.Id == a.GameId &&
                        game.SharedGameId == g.Id &&
                        game.ApprovalStatus == ApprovedStatus)));
            }
        }

        if (query.IsTopRated.HasValue)
        {
            // chip "top-rated": AverageRating >= threshold (configured, default 4.5).
            // Local capture so EF Core can parameterize the constant.
            var threshold = _topRatedThreshold;
            dbQuery = query.IsTopRated.Value
                ? dbQuery.Where(g => g.AverageRating != null && g.AverageRating >= threshold)
                : dbQuery.Where(g => g.AverageRating == null || g.AverageRating < threshold);
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

            "ComplexityRating" => query.SortDescending
                ? dbQuery.OrderByDescending(g => g.ComplexityRating).ThenBy(g => g.Title)
                : dbQuery.OrderBy(g => g.ComplexityRating).ThenBy(g => g.Title),

            _ => query.SortDescending
                ? dbQuery.OrderBy(g => g.HasKnowledgeBase ? 0 : 1).ThenByDescending(g => g.Title)
                : dbQuery.OrderBy(g => g.HasKnowledgeBase ? 0 : 1).ThenBy(g => g.Title)
        };

        // Pagination
        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        // Issue #593 (Wave A.3a): aggregate counts via cross-BC LINQ sub-queries.
        // We capture the DbContext set references locally so the projection lambda can
        // close over them without re-evaluating `_context` for every row (EF Core
        // translates the whole expression tree, but explicit locals keep intent clear).
        // ApprovalStatus.Approved == 2 (reuses `ApprovedStatus` const declared above).
        var ctxGames = _context.Games;
        var ctxToolkits = _context.Toolkits;
        var ctxAgents = _context.AgentDefinitions;
        var ctxVectors = _context.VectorDocuments;
        var topRatedThreshold = _topRatedThreshold;

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
                g.ModifiedAt,
                g.IsRagPublic,
                g.HasKnowledgeBase,
                // ToolkitsCount: per-user custom toolkits (excludes the read-only default
                // BR-02 from Issue #5144) for any approved Game linked to this SharedGame.
                ctxToolkits.Count(t =>
                    !t.IsDefault &&
                    ctxGames.Any(game =>
                        game.Id == t.GameId &&
                        game.SharedGameId == g.Id &&
                        game.ApprovalStatus == ApprovedStatus)),
                // AgentsCount: AgentDefinitions linked to any approved Game of this SharedGame.
                ctxAgents.Count(a =>
                    a.GameId.HasValue &&
                    ctxGames.Any(game =>
                        game.Id == a.GameId &&
                        game.SharedGameId == g.Id &&
                        game.ApprovalStatus == ApprovedStatus)),
                // KbsCount: VectorDocuments have a direct SharedGameId FK (Issue #5185 history),
                // no join through GameEntity is required.
                ctxVectors.Count(vd => vd.SharedGameId == g.Id),
                // NewThisWeekCount / ContributorsCount / IsNew: deferred to follow-up commit
                // within the same PR (#593) — see spec §6.1, §10.
                0,    // NewThisWeekCount  (follow-up)
                0,    // ContributorsCount (follow-up)
                // IsTopRated: AverageRating >= configured threshold (spec §9 decision 1).
                // Threshold sourced from `SharedGameCatalog:TopRatedThreshold` in
                // appsettings.json (default 4.5m). Captured locally so EF parametrizes it.
                g.AverageRating != null && g.AverageRating >= topRatedThreshold,
                false  // IsNew (follow-up)
            ))
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

        // Create compact hash for long parameter combinations.
        // The `v` prefix is the projection/query-shape schema version — bump when
        // SharedGameDto fields OR query params change so pre-existing cached entries
        // (with the old shape) are invalidated automatically without a separate flush.
        //   v2 = Issue #593 aggregate counts (Commit 1)
        //   v3 = Issue #593 chip filters: HasToolkit / HasAgent / IsTopRated (Commit 2)
        var keyComponents = $"v3|{searchTerm}|{categoryIds}|{mechanicIds}|{query.MinPlayers}|{query.MaxPlayers}|{query.MaxPlayingTime}|{query.MinComplexity}|{query.MaxComplexity}|{statusStr}|{query.PageNumber}|{query.PageSize}|{query.SortBy}|{query.SortDescending}|{query.HasKnowledgeBase?.ToString() ?? "null"}|{query.HasToolkit?.ToString() ?? "null"}|{query.HasAgent?.ToString() ?? "null"}|{query.IsTopRated?.ToString() ?? "null"}";

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyComponents)));

        return $"search-games:{hash}";
    }
}
