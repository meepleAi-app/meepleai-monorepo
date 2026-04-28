using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for getting a shared game by ID.
/// Uses HybridCache (L1: Memory 30min, L2: Redis 2h) for performance.
/// Tagged with <c>shared-game:{id}</c> so cross-BC event handlers (toolkit/agent/KB
/// changes) can invalidate a single detail entry surgically rather than flushing
/// the whole search namespace. Issue #2371 Phase 2, extended #2373 Phase 4, #603 Wave A.4.
/// </summary>
internal sealed class GetSharedGameByIdQueryHandler : IRequestHandler<GetSharedGameByIdQuery, SharedGameDetailDto?>
{
    /// <summary>
    /// Default IsTopRated threshold; mirrors <see cref="SearchSharedGamesQueryHandler"/>
    /// (Wave A.3a → A.3b adjustment 4.5 → 4.0 to match mockup chip logic on 0..5 scale).
    /// </summary>
    private const decimal DefaultTopRatedThreshold = 4.0m;

    /// <summary>
    /// Default rolling window (days) for the IsNew flag — mirrors search handler.
    /// </summary>
    private const int DefaultNewWindowDays = 7;

    /// <summary>
    /// Top-N cap for nested toolkit previews to bound response size for very popular
    /// games while still showing a meaningful sample. Detail page renders the most
    /// recently updated entries first.
    /// </summary>
    private const int MaxToolkitPreviews = 20;

    /// <summary>
    /// Top-N cap for nested agent previews. Agents are typically fewer than toolkits
    /// so a smaller cap is appropriate.
    /// </summary>
    private const int MaxAgentPreviews = 10;

    /// <summary>
    /// Top-N cap for nested KB document previews. A single rulebook can have many
    /// indexed PDFs (base + expansions + errata); 30 covers the long tail without
    /// blowing up payload size.
    /// </summary>
    private const int MaxKbPreviews = 30;

    /// <summary>
    /// ApprovalStatus.Approved value mirroring the search handler. Using a const
    /// avoids cross-BC enum imports in the cross-context LINQ tree.
    /// </summary>
    private const int ApprovedStatus = 2;

    private readonly ISharedGameRepository _repository;
    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly ILogger<GetSharedGameByIdQueryHandler> _logger;
    private readonly decimal _topRatedThreshold;
    private readonly int _newWindowDays;

    public GetSharedGameByIdQueryHandler(
        ISharedGameRepository repository,
        MeepleAiDbContext context,
        HybridCache cache,
        IConfiguration configuration,
        ILogger<GetSharedGameByIdQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        ArgumentNullException.ThrowIfNull(configuration);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _topRatedThreshold = configuration.GetValue("SharedGameCatalog:TopRatedThreshold", DefaultTopRatedThreshold);
        var configuredWindow = configuration.GetValue("SharedGameCatalog:NewWindowDays", DefaultNewWindowDays);
        _newWindowDays = configuredWindow > 0 ? configuredWindow : DefaultNewWindowDays;
    }

    public async Task<SharedGameDetailDto?> Handle(GetSharedGameByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var cacheKey = $"shared-game:{query.GameId}";
        var cacheTag = $"shared-game:{query.GameId}";

        // Try cache first (L1: 30min, L2: 2h).
        // Tagged with the per-game key so toolkit/agent/KB event handlers can
        // invalidate this entry without touching the whole search-games namespace.
        return await _cache.GetOrCreateAsync<SharedGameDetailDto?>(
            cacheKey,
            async cancel =>
            {
                _logger.LogInformation("Cache miss for shared game: {GameId}", query.GameId);
                return await FetchGameDetailsAsync(query.GameId, cancel).ConfigureAwait(false);
            },
            new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromMinutes(30),  // L1
                Expiration = TimeSpan.FromHours(2)                // L2
            },
            tags: new[] { cacheTag },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<SharedGameDetailDto?> FetchGameDetailsAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var game = await _repository.GetByIdAsync(gameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            _logger.LogWarning("Shared game not found: {GameId}", gameId);
            return null;
        }

        GameRulesDto? rulesDto = null;
        if (game.Rules is not null)
        {
            rulesDto = new GameRulesDto(game.Rules.Content, game.Rules.Language);
        }

        var faqDtos = game.Faqs
            .OrderBy(f => f.DisplayOrder)
            .Select(f => new GameFaqDto(f.Id, f.SharedGameId, f.Question, f.Answer, f.DisplayOrder, f.UpvoteCount, f.CreatedAt, f.UpdatedAt))
            .ToList();

        var errataDtos = game.Erratas
            .OrderByDescending(e => e.PublishedDate)
            .Select(e => new GameErrataDto(e.Id, e.Description, e.PageReference, e.PublishedDate, e.CreatedAt))
            .ToList();

        var designerDtos = game.Designers.Select(d => new GameDesignerDto(d.Id, d.Name)).ToList();
        var publisherDtos = game.Publishers.Select(p => new GamePublisherDto(p.Id, p.Name)).ToList();
        var categoryDtos = game.Categories.Select(c => new GameCategorySimpleDto(c.Id, c.Name, c.Slug)).ToList();
        var mechanicDtos = game.Mechanics.Select(m => new GameMechanicSimpleDto(m.Id, m.Name, m.Slug)).ToList();

        // === A.4 (Issue #603) cross-BC projections ===
        // Capture DbContext sets once so the LINQ tree closes over stable locals.
        // Soft-delete note: Toolkit / AgentDefinition / VectorDocument do NOT have
        // IsDeleted columns (mirrors A.3a §6 comment in SearchSharedGamesQueryHandler).
        var ctxGames = _context.Games;
        var ctxToolkits = _context.Toolkits;
        var ctxAgents = _context.AgentDefinitions;
        var ctxVectors = _context.VectorDocuments;
        var ctxUsers = _context.Users;
        var newCutoff = DateTime.UtcNow.AddDays(-_newWindowDays);

        // Top-N toolkit previews — non-default, approved-Game-linked, ordered by
        // recency. Toolkit.UpdatedAt is non-nullable (set on factory + every mutator),
        // so no coalesce. OwnerName resolved via Users join (DisplayName fallback to
        // Email per UserEntity contract).
        var toolkitPreviews = await ctxToolkits
            .AsNoTracking()
            .Where(t =>
                !t.IsDefault &&
                t.OwnerUserId != null &&
                ctxGames.Any(g =>
                    g.Id == t.GameId &&
                    g.SharedGameId == gameId &&
                    g.ApprovalStatus == ApprovedStatus))
            .OrderByDescending(t => t.UpdatedAt)
            .Take(MaxToolkitPreviews)
            .Select(t => new PublishedToolkitPreviewDto(
                t.Id,
                t.DisplayName ?? string.Empty,
                t.OwnerUserId!.Value,
                ctxUsers
                    .Where(u => u.Id == t.OwnerUserId)
                    .Select(u => u.DisplayName ?? u.Email)
                    .FirstOrDefault() ?? string.Empty,
                t.UpdatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Top-N agent previews — approved-Game-linked, ordered by recency.
        // AgentDefinition.UpdatedAt is nullable (entity uses lazy-init pattern), so
        // coalesce to CreatedAt for both projection and sort. AgentDefinition.GameId
        // is exposed via private backing field `_gameId` mapped by EF; we use
        // EF.Property to access it in translatable LINQ trees.
        var agentPreviews = await ctxAgents
            .AsNoTracking()
            .Where(a =>
                EF.Property<Guid?>(a, "_gameId") != null &&
                ctxGames.Any(g =>
                    g.Id == EF.Property<Guid?>(a, "_gameId") &&
                    g.SharedGameId == gameId &&
                    g.ApprovalStatus == ApprovedStatus))
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .Take(MaxAgentPreviews)
            .Select(a => new PublishedAgentPreviewDto(
                a.Id,
                a.Name,
                a.InvocationCount,
                a.UpdatedAt ?? a.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Top-N KB previews — VectorDocument has direct SharedGameId FK (Issue #5185
        // history). Filter to fully-indexed only (status == "completed" AND
        // IndexedAt.HasValue). Language sourced from PdfDocument nav prop (the only
        // place ISO-639-1 lives — VectorDocumentEntity has no own Language column).
        var kbPreviews = await ctxVectors
            .AsNoTracking()
            .Where(vd =>
                vd.SharedGameId == gameId &&
                vd.IndexingStatus == "completed" &&
                vd.IndexedAt.HasValue)
            .OrderByDescending(vd => vd.IndexedAt)
            .Take(MaxKbPreviews)
            .Select(vd => new PublishedKbPreviewDto(
                vd.Id,
                vd.PdfDocument.Language,
                vd.ChunkCount,
                vd.IndexedAt!.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Aggregate counts — same projection logic as SearchSharedGamesQueryHandler
        // §6.1 but scoped to a single SharedGame (no per-row sub-query, single aggregation
        // round-trip). Combined into one EF query so all four counts hit DB in parallel
        // sub-selects rather than four separate round-trips.
        var aggregates = await _context.SharedGames
            .AsNoTracking()
            .Where(g => g.Id == gameId)
            .Select(g => new
            {
                ToolkitsCount = ctxToolkits.Count(t =>
                    !t.IsDefault &&
                    ctxGames.Any(game =>
                        game.Id == t.GameId &&
                        game.SharedGameId == g.Id &&
                        game.ApprovalStatus == ApprovedStatus)),
                AgentsCount = ctxAgents.Count(a =>
                    EF.Property<Guid?>(a, "_gameId") != null &&
                    ctxGames.Any(game =>
                        game.Id == EF.Property<Guid?>(a, "_gameId") &&
                        game.SharedGameId == g.Id &&
                        game.ApprovalStatus == ApprovedStatus)),
                KbsCount = ctxVectors.Count(vd =>
                    vd.SharedGameId == g.Id &&
                    vd.IndexingStatus == "completed"),
                // ContributorsCount: same A.3a approximation (distinct Toolkit.OwnerUserId)
                // — AgentDefinition / VectorDocument lack a CreatedBy column. Tracked
                // limitation; full DISTINCT(UserId) over Toolkits ∪ Agents ∪ KBs needs
                // entity changes deferred per spec.
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
                    .Count(),
                // IsNew indicator at the detail level uses the same NewThisWeekCount
                // formula as the search handler so the badge stays consistent
                // between index card and detail page.
                NewThisWeekCount =
                    ctxToolkits.Count(t =>
                        !t.IsDefault &&
                        t.CreatedAt >= newCutoff &&
                        ctxGames.Any(game =>
                            game.Id == t.GameId &&
                            game.SharedGameId == g.Id &&
                            game.ApprovalStatus == ApprovedStatus)) +
                    ctxAgents.Count(a =>
                        EF.Property<Guid?>(a, "_gameId") != null &&
                        a.CreatedAt >= newCutoff &&
                        ctxGames.Any(game =>
                            game.Id == EF.Property<Guid?>(a, "_gameId") &&
                            game.SharedGameId == g.Id &&
                            game.ApprovalStatus == ApprovedStatus)) +
                    ctxVectors.Count(vd =>
                        vd.SharedGameId == g.Id &&
                        vd.IndexedAt >= newCutoff)
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var toolkitsCount = aggregates?.ToolkitsCount ?? 0;
        var agentsCount = aggregates?.AgentsCount ?? 0;
        var kbsCount = aggregates?.KbsCount ?? 0;
        var contributorsCount = aggregates?.ContributorsCount ?? 0;
        var newThisWeekCount = aggregates?.NewThisWeekCount ?? 0;

        var hasKnowledgeBase = kbsCount > 0;
        var isTopRated = game.AverageRating.HasValue && game.AverageRating.Value >= _topRatedThreshold;
        // IsNew uses the same `>= 2` threshold as the index chip (mockup
        // sp3-shared-games.jsx:127) for cross-page consistency.
        var isNew = newThisWeekCount >= 2;

        return new SharedGameDetailDto(
            game.Id,
            game.BggId,
            game.Title,
            game.YearPublished,
            game.Description,
            game.MinPlayers,
            game.MaxPlayers,
            game.PlayingTimeMinutes,
            game.MinAge,
            game.ComplexityRating,
            game.AverageRating,
            game.ImageUrl,
            game.ThumbnailUrl,
            rulesDto,
            game.Status,
            game.CreatedBy,
            game.ModifiedBy,
            game.CreatedAt,
            game.ModifiedAt,
            faqDtos,
            errataDtos,
            designerDtos,
            publisherDtos,
            categoryDtos,
            mechanicDtos,
            // === A.4 extensions ===
            toolkitPreviews,
            agentPreviews,
            kbPreviews,
            toolkitsCount,
            agentsCount,
            kbsCount,
            contributorsCount,
            hasKnowledgeBase,
            isTopRated,
            isNew);
    }
}
