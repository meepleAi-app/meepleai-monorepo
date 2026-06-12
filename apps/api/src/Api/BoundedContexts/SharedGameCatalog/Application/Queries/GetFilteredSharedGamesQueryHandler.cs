using System.Globalization;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Models;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for filtering shared games with pagination and sorting.
/// Supports filtering by status, search term, submitter, and customizable sorting.
/// Issue #3533: Admin API Endpoints - Approval Queue Management
/// </summary>
internal sealed class GetFilteredSharedGamesQueryHandler : IRequestHandler<GetFilteredSharedGamesQuery, PagedResult<SharedGameDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<GetFilteredSharedGamesQueryHandler> _logger;

    public GetFilteredSharedGamesQueryHandler(
        MeepleAiDbContext context,
        IBlobStorageService blobStorage,
        ILogger<GetFilteredSharedGamesQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<SharedGameDto>> Handle(GetFilteredSharedGamesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting filtered shared games: Status={Status}, Search={Search}, SubmittedBy={SubmittedBy}, CategoryId={CategoryId}, Page={Page}, PageSize={PageSize}, SortBy={SortBy}",
            query.Status,
            query.Search,
            query.SubmittedBy,
            query.CategoryId,
            query.PageNumber,
            query.PageSize,
            query.SortBy);

        var dbQuery = _context.SharedGames.AsNoTracking();

        // Apply filters
        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(g => g.Status == (int)query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchTerm = $"%{query.Search.Trim()}%";
            dbQuery = dbQuery.Where(g => EF.Functions.ILike(g.Title, searchTerm) ||
                                         EF.Functions.ILike(g.Description, searchTerm));
        }

        if (query.SubmittedBy.HasValue)
        {
            dbQuery = dbQuery.Where(g => g.CreatedBy == query.SubmittedBy.Value);
        }

        if (query.CategoryId.HasValue)
        {
            dbQuery = dbQuery.Where(g =>
                g.Categories.Any(c => c.Id == query.CategoryId.Value));
        }

        // Parse sort parameters
        var (sortField, isDescending) = ParseSortBy(query.SortBy);

        // Apply sorting
        IQueryable<SharedGameEntity> sortedQuery = ApplySorting(dbQuery, sortField, isDescending);

        // Count total before pagination
        var total = await sortedQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        // Apply pagination — materialize entities first so we can call the async
        // CoverUrlResolver (EF expression trees cannot invoke async methods).
        var entities = await sortedQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Issue #1852 (Gap A): resolve cover URL (L4 → L2 priority) per entity sequentially.
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
                CoverUrl: coverUrl));
        }

        _logger.LogInformation(
            "Retrieved {Count} filtered shared games (Total: {Total}) for page {Page}",
            games.Count,
            total,
            query.PageNumber);

        return new PagedResult<SharedGameDto>(
            Items: games,
            Total: total,
            Page: query.PageNumber,
            PageSize: query.PageSize);
    }

    private static (string SortField, bool IsDescending) ParseSortBy(string? sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return ("modifiedat", true); // Default: sort by newest first, descending
        }

        var parts = sortBy.Split(':');
        var fieldName = parts[0].Trim();
        var direction = parts.Length > 1 ? parts[1].Trim() : "asc";

        var isDescending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);

        return (fieldName, isDescending);
    }

    private static IQueryable<SharedGameEntity> ApplySorting(IQueryable<SharedGameEntity> query, string sortField, bool isDescending)
    {
        var normalizedField = sortField.ToLower(CultureInfo.InvariantCulture);

        return normalizedField switch
        {
            "title" => isDescending ? query.OrderByDescending(g => g.Title) : query.OrderBy(g => g.Title),
            "createdat" => isDescending ? query.OrderByDescending(g => g.CreatedAt) : query.OrderBy(g => g.CreatedAt),
            "modifiedat" => isDescending ? query.OrderByDescending(g => g.ModifiedAt ?? g.CreatedAt) : query.OrderBy(g => g.ModifiedAt ?? g.CreatedAt),
            "status" => isDescending ? query.OrderByDescending(g => g.Status) : query.OrderBy(g => g.Status),
            "yearpublished" => isDescending ? query.OrderByDescending(g => g.YearPublished) : query.OrderBy(g => g.YearPublished),
            _ => query.OrderByDescending(g => g.ModifiedAt ?? g.CreatedAt) // Default sort
        };
    }
}
