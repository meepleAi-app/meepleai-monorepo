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
    /// Adjusted from 4.5 → 4.0 in Wave A.3b (Issue #596) to match mockup
    /// `sp3-shared-games.jsx:126` filter `(g.rating || 0) >= 8` (0..10 scale,
    /// equivalent to ≥4.0 on the entity's 0..5 scale).
    /// </summary>
    private const decimal DefaultTopRatedThreshold = 4.0m;

    /// <summary>
    /// Default rolling window (days) for <c>NewThisWeekCount</c> when
    /// <c>SharedGameCatalog:NewWindowDays</c> is missing. Spec §9 decision 2 (Issue #593).
    /// </summary>
    private const int DefaultNewWindowDays = 7;

    /// <summary>
    /// Threshold above which <c>IsNew</c> evaluates to <c>true</c>. Mockup
    /// <c>sp3-shared-games.jsx:127</c> uses <c>newWeek &gt;= 2</c>.
    /// </summary>
    private const int IsNewMinThreshold = 2;

    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly ILogger<SearchSharedGamesQueryHandler> _logger;
    private readonly decimal _topRatedThreshold;
    private readonly int _newWindowDays;

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

        // Issue #593: rolling window for "new this week" badges. Configurable via
        // SharedGameCatalog:NewWindowDays (default 7). A negative or zero value
        // falls back to DefaultNewWindowDays (7) — a window of 0 days would
        // always return 0 new entries, defeating the chip's purpose.
        var configuredWindow = configuration.GetValue("SharedGameCatalog:NewWindowDays", DefaultNewWindowDays);
        _newWindowDays = configuredWindow > 0 ? configuredWindow : DefaultNewWindowDays;
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

        // Capture DbContext set references + computed timestamp constants once,
        // *before* the filter section, so both the filter sub-queries (e.g. IsNew)
        // and the post-filter aggregate projection can close over the same locals
        // without duplicate evaluations.
        // ----------------------------------------------------------------------
        // Soft-delete note (Spec §8 risk): Toolkit, AgentDefinition, and
        // VectorDocument do NOT carry IsDeleted/DeletedAt columns on their
        // domain entities — the project-wide soft-delete pattern (and matching
        // EF Core HasQueryFilter) is not configured for these three types.
        // No explicit `.Where(!e.IsDeleted)` guard is therefore added here.
        // If/when soft-delete is introduced (out of scope per spec §10 "no EF
        // migration in A.3a"), the filter blocks below and the projection
        // sub-queries must both be updated.
        var ctxGames = _context.Games;
        var ctxToolkits = _context.Toolkits;
        var ctxAgents = _context.AgentDefinitions;
        var ctxVectors = _context.VectorDocuments;
        var topRatedThreshold = _topRatedThreshold;
        // Rolling window cutoff for NewThisWeekCount / IsNew. Computed once per
        // query so EF Core parameterizes a single timestamp constant rather than
        // re-evaluating `DateTime.UtcNow` per row.
        var newCutoff = DateTime.UtcNow.AddDays(-_newWindowDays);

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
            dbQuery = query.IsTopRated.Value
                ? dbQuery.Where(g => g.AverageRating != null && g.AverageRating >= topRatedThreshold)
                : dbQuery.Where(g => g.AverageRating == null || g.AverageRating < topRatedThreshold);
        }

        if (query.IsNew.HasValue)
        {
            // chip "new": same NewThisWeekCount sum as the projection arm below
            // (non-default Toolkits + Agents + KBs created in the rolling window),
            // gated by `>= IsNewMinThreshold` (default 2 per mockup line 127).
            // True branch keeps games above threshold; False branch keeps the
            // complement (strictly below threshold — explicit so it survives EF
            // expression translation without DeMorgan rewrites).
            if (query.IsNew.Value)
            {
                dbQuery = dbQuery.Where(g =>
                    (ctxToolkits.Count(t =>
                        !t.IsDefault &&
                        t.CreatedAt >= newCutoff &&
                        ctxGames.Any(game =>
                            game.Id == t.GameId &&
                            game.SharedGameId == g.Id &&
                            game.ApprovalStatus == ApprovedStatus)) +
                     ctxAgents.Count(a =>
                        a.GameId.HasValue &&
                        a.CreatedAt >= newCutoff &&
                        ctxGames.Any(game =>
                            game.Id == a.GameId &&
                            game.SharedGameId == g.Id &&
                            game.ApprovalStatus == ApprovedStatus)) +
                     ctxVectors.Count(vd =>
                        vd.SharedGameId == g.Id &&
                        vd.IndexedAt >= newCutoff)
                    ) >= IsNewMinThreshold);
            }
            else
            {
                dbQuery = dbQuery.Where(g =>
                    (ctxToolkits.Count(t =>
                        !t.IsDefault &&
                        t.CreatedAt >= newCutoff &&
                        ctxGames.Any(game =>
                            game.Id == t.GameId &&
                            game.SharedGameId == g.Id &&
                            game.ApprovalStatus == ApprovedStatus)) +
                     ctxAgents.Count(a =>
                        a.GameId.HasValue &&
                        a.CreatedAt >= newCutoff &&
                        ctxGames.Any(game =>
                            game.Id == a.GameId &&
                            game.SharedGameId == g.Id &&
                            game.ApprovalStatus == ApprovedStatus)) +
                     ctxVectors.Count(vd =>
                        vd.SharedGameId == g.Id &&
                        vd.IndexedAt >= newCutoff)
                    ) < IsNewMinThreshold);
            }
        }

        // Pagination — `total` reflects the post-filter row count and is independent
        // from the sort/projection that follows.
        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        // Project to anonymous shape carrying the entity + computed aggregates *before*
        // sort/page. This lets us ORDER BY ContributorsCount / NewThisWeekCount (Spec §6.1
        // sort options "Contrib" / "New") without recomputing the sub-queries inside the
        // OrderBy expression — Postgres still computes them once per row, but the LINQ
        // tree stays readable and avoids duplicate sub-tree expansion.
        var projected = dbQuery.Select(g => new
        {
            Game = g,
            // ToolkitsCount: per-user custom toolkits (excludes the read-only default
            // BR-02 from Issue #5144) for any approved Game linked to this SharedGame.
            ToolkitsCount = ctxToolkits.Count(t =>
                !t.IsDefault &&
                ctxGames.Any(game =>
                    game.Id == t.GameId &&
                    game.SharedGameId == g.Id &&
                    game.ApprovalStatus == ApprovedStatus)),
            // AgentsCount: AgentDefinitions linked to any approved Game of this SharedGame.
            AgentsCount = ctxAgents.Count(a =>
                a.GameId.HasValue &&
                ctxGames.Any(game =>
                    game.Id == a.GameId &&
                    game.SharedGameId == g.Id &&
                    game.ApprovalStatus == ApprovedStatus)),
            // KbsCount: VectorDocuments have a direct SharedGameId FK (Issue #5185 history),
            // no join through GameEntity is required.
            KbsCount = ctxVectors.Count(vd => vd.SharedGameId == g.Id),
            // NewThisWeekCount: sum of (non-default toolkits + agents + KBs) created in the
            // last `_newWindowDays` days for any approved Game of this SharedGame
            // (KBs use direct SharedGameId; entity timestamps: Toolkit.CreatedAt,
            // AgentDefinition.CreatedAt, VectorDocument.IndexedAt). Spec §6.3.
            NewThisWeekCount =
                ctxToolkits.Count(t =>
                    !t.IsDefault &&
                    t.CreatedAt >= newCutoff &&
                    ctxGames.Any(game =>
                        game.Id == t.GameId &&
                        game.SharedGameId == g.Id &&
                        game.ApprovalStatus == ApprovedStatus)) +
                ctxAgents.Count(a =>
                    a.GameId.HasValue &&
                    a.CreatedAt >= newCutoff &&
                    ctxGames.Any(game =>
                        game.Id == a.GameId &&
                        game.SharedGameId == g.Id &&
                        game.ApprovalStatus == ApprovedStatus)) +
                ctxVectors.Count(vd =>
                    vd.SharedGameId == g.Id &&
                    vd.IndexedAt >= newCutoff),
            // ContributorsCount: distinct authors who contributed to this SharedGame.
            // Spec §6.4 envisions DISTINCT(UserId) over Toolkits ∪ Agents ∪ KBs but only
            // Toolkit.OwnerUserId is exposed on the domain model right now (AgentDefinition
            // and VectorDocument do not carry a CreatedBy/UserId field — adding one is out
            // of scope per spec §10 "No EF migration in A.3a"). We therefore approximate
            // with distinct Toolkit.OwnerUserId on approved Games. Tracked as A.3a
            // limitation; full union deferred to A.3b once user-tracking columns are added.
            ContributorsCount = ctxToolkits
                .Where(t =>
                    !t.IsDefault &&
                    t.OwnerUserId != null &&
                    ctxGames.Any(game =>
                        game.Id == t.GameId &&
                        game.SharedGameId == g.Id &&
                        game.ApprovalStatus == ApprovedStatus))
                .Select(t => t.OwnerUserId)
                .Distinct()
                .Count()
        });

        // Apply sorting on the projected shape (post-aggregate computation).
        var sorted = query.SortBy switch
        {
            "YearPublished" => query.SortDescending
                ? projected.OrderByDescending(p => p.Game.YearPublished).ThenBy(p => p.Game.Title)
                : projected.OrderBy(p => p.Game.YearPublished).ThenBy(p => p.Game.Title),

            "AverageRating" => query.SortDescending
                ? projected.OrderByDescending(p => p.Game.AverageRating).ThenBy(p => p.Game.Title)
                : projected.OrderBy(p => p.Game.AverageRating).ThenBy(p => p.Game.Title),

            "CreatedAt" => query.SortDescending
                ? projected.OrderByDescending(p => p.Game.CreatedAt).ThenBy(p => p.Game.Title)
                : projected.OrderBy(p => p.Game.CreatedAt).ThenBy(p => p.Game.Title),

            "ComplexityRating" => query.SortDescending
                ? projected.OrderByDescending(p => p.Game.ComplexityRating).ThenBy(p => p.Game.Title)
                : projected.OrderBy(p => p.Game.ComplexityRating).ThenBy(p => p.Game.Title),

            // Issue #593 Commit 1b — sort by computed aggregates.
            "Contrib" => query.SortDescending
                ? projected.OrderByDescending(p => p.ContributorsCount).ThenBy(p => p.Game.Title)
                : projected.OrderBy(p => p.ContributorsCount).ThenBy(p => p.Game.Title),

            "New" => query.SortDescending
                ? projected.OrderByDescending(p => p.NewThisWeekCount).ThenByDescending(p => p.Game.CreatedAt).ThenBy(p => p.Game.Title)
                : projected.OrderBy(p => p.NewThisWeekCount).ThenBy(p => p.Game.CreatedAt).ThenBy(p => p.Game.Title),

            _ => query.SortDescending
                ? projected.OrderBy(p => p.Game.HasKnowledgeBase ? 0 : 1).ThenByDescending(p => p.Game.Title)
                : projected.OrderBy(p => p.Game.HasKnowledgeBase ? 0 : 1).ThenBy(p => p.Game.Title)
        };

        var games = await sorted
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new SharedGameDto(
                p.Game.Id,
                p.Game.BggId,
                p.Game.Title,
                p.Game.YearPublished,
                p.Game.Description,
                p.Game.MinPlayers,
                p.Game.MaxPlayers,
                p.Game.PlayingTimeMinutes,
                p.Game.MinAge,
                p.Game.ComplexityRating,
                p.Game.AverageRating,
                p.Game.ImageUrl,
                p.Game.ThumbnailUrl,
                (GameStatus)p.Game.Status,
                p.Game.CreatedAt,
                p.Game.ModifiedAt,
                p.Game.IsRagPublic,
                p.Game.HasKnowledgeBase,
                p.ToolkitsCount,
                p.AgentsCount,
                p.KbsCount,
                p.NewThisWeekCount,
                p.ContributorsCount,
                // IsTopRated: AverageRating >= configured threshold (spec §9 decision 1).
                p.Game.AverageRating != null && p.Game.AverageRating >= topRatedThreshold,
                // IsNew: derived from NewThisWeekCount per mockup sp3-shared-games.jsx:127.
                p.NewThisWeekCount >= IsNewMinThreshold))
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
        //   v4 = Issue #593 NewThisWeekCount + ContributorsCount + IsNew + sort options
        //        "Contrib" / "New" (Commit 1b — projection shape changed)
        var keyComponents = $"v4|{searchTerm}|{categoryIds}|{mechanicIds}|{query.MinPlayers}|{query.MaxPlayers}|{query.MaxPlayingTime}|{query.MinComplexity}|{query.MaxComplexity}|{statusStr}|{query.PageNumber}|{query.PageSize}|{query.SortBy}|{query.SortDescending}|{query.HasKnowledgeBase?.ToString() ?? "null"}|{query.HasToolkit?.ToString() ?? "null"}|{query.HasAgent?.ToString() ?? "null"}|{query.IsTopRated?.ToString() ?? "null"}|{query.IsNew?.ToString() ?? "null"}";

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyComponents)));

        return $"search-games:{hash}";
    }
}
