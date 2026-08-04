using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Infrastructure;
using Api.Models;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for getting all shared games with optional status filter and pagination.
/// Used by admin UI list view to display games without search/filter complexity.
/// </summary>
internal sealed class GetAllSharedGamesQueryHandler : IRequestHandler<GetAllSharedGamesQuery, PagedResult<SharedGameDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<GetAllSharedGamesQueryHandler> _logger;
    private readonly IGameTitleResolver _titleResolver;

    public GetAllSharedGamesQueryHandler(
        MeepleAiDbContext context,
        IBlobStorageService blobStorage,
        ILogger<GetAllSharedGamesQueryHandler> logger,
        IGameTitleResolver titleResolver)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _titleResolver = titleResolver ?? throw new ArgumentNullException(nameof(titleResolver));
    }

    public async Task<PagedResult<SharedGameDto>> Handle(GetAllSharedGamesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting all shared games: Status={Status}, Page={Page}, PageSize={PageSize}",
            query.Status?.ToString() ?? "All",
            query.PageNumber,
            query.PageSize);

        var dbQuery = _context.SharedGames.AsNoTracking();

        // Apply status filter if specified
        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(g => g.Status == (int)query.Status.Value);
        }

        // Sort by title by default
        dbQuery = dbQuery.OrderBy(g => g.Title);

        // Pagination
        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var entities = await dbQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Issue #1852 (Gap A): CoverUrlResolver is async (presigned URL mint); cannot be called
        // inside an EF expression tree. Materialize first, then resolve covers sequentially.
        var games = new List<SharedGameDto>(entities.Count);

        // Issue #2243 (epic #2242) Block B: pre-compute KbsCount per page (real chunk count from
        // VectorDocuments) instead of hardcoding 0. Single batched query avoids N+1.
        var pageGameIds = entities.Select(e => e.Id).ToList();
        var kbsCountByGame = await _context.VectorDocuments
            .AsNoTracking()
            .Where(v => v.SharedGameId != null
                && pageGameIds.Contains(v.SharedGameId.Value)
                && v.IndexingStatus == "completed")
            .GroupBy(v => v.SharedGameId!.Value)
            .Select(grp => new { GameId = grp.Key, Count = grp.Count() })
            .ToDictionaryAsync(x => x.GameId, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

        foreach (var g in entities)
        {
            var cover = await CoverUrlResolver
                .ResolvePublicWithSourceAsync(g, _blobStorage)
                .ConfigureAwait(false);
            var (coverLicense, coverAttribution, coverSourceUrl) = CoverAttribution.ForWinningSource(cover.Kind, g);

            games.Add(new SharedGameDto(
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
                // Issue #2123 — tombstone fields (entity columns now nullable post Phase A).
                g.ImageUrl ?? string.Empty,
                g.ThumbnailUrl ?? string.Empty,
                (GameStatus)g.Status,
                g.CreatedAt,
                g.ModifiedAt,
                g.IsRagPublic,
                g.HasKnowledgeBase,
                // Issue #593 (Wave A.3a) — aggregate fields not computed by this handler.
                // Explicit zero/false values required: EF Core expression trees forbid
                // default-argument elision (CS0854).
                0,      // ToolkitsCount
                0,      // AgentsCount
                kbsCountByGame.GetValueOrDefault(g.Id, 0),  // KbsCount — issue #2243 Block B
                0,      // NewThisWeekCount
                0,      // ContributorsCount
                false,  // IsTopRated
                false,  // IsNew
                CoverUrl: cover.Url,
                // Epic #3470 Slice 1d-a — attribution follows the WINNING source (was
                // emitted unconditionally); all-null unless the Wikidata cover won.
                CoverLicense: coverLicense,
                CoverAttribution: coverAttribution,
                CoverSourceUrl: coverSourceUrl));
        }

        // Issue #2339 (Wave 4 Task 13 — DEC-WIRING): enrich SharedGameDto.Translations
        // for the page. Batch one round-trip via IGameTitleResolver.GetByGameIdsAsync.
        var enriched = await _titleResolver
            .EnrichAsync(games, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Retrieved {Count} games (Total: {Total}) for page {Page}",
            enriched.Count,
            total,
            query.PageNumber);

        return new PagedResult<SharedGameDto>(
            Items: enriched,
            Total: total,
            Page: query.PageNumber,
            PageSize: query.PageSize);
    }
}
