using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
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

    public GetAllSharedGamesQueryHandler(
        MeepleAiDbContext context,
        IBlobStorageService blobStorage,
        ILogger<GetAllSharedGamesQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        foreach (var g in entities)
        {
            var coverUrl = await CoverUrlResolver
                .ResolvePublicAsync(g, _blobStorage)
                .ConfigureAwait(false);

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
                g.ImageUrl,
                g.ThumbnailUrl,
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
                0,      // KbsCount
                0,      // NewThisWeekCount
                0,      // ContributorsCount
                false,  // IsTopRated
                false,  // IsNew
                CoverUrl: coverUrl));
        }

        _logger.LogInformation(
            "Retrieved {Count} games (Total: {Total}) for page {Page}",
            games.Count,
            total,
            query.PageNumber);

        return new PagedResult<SharedGameDto>(
            Items: games,
            Total: total,
            Page: query.PageNumber,
            PageSize: query.PageSize);
    }
}
