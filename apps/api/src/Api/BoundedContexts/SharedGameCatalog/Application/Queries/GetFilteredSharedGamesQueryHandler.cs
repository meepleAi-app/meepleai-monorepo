using System.Globalization;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Models;
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
    private readonly ILogger<GetFilteredSharedGamesQueryHandler> _logger;

    public GetFilteredSharedGamesQueryHandler(
        MeepleAiDbContext context,
        ILogger<GetFilteredSharedGamesQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<SharedGameDto>> Handle(GetFilteredSharedGamesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting filtered shared games: Status={Status}, Search={Search}, SubmittedBy={SubmittedBy}, Page={Page}, PageSize={PageSize}, SortBy={SortBy}",
            query.Status,
            query.Search,
            query.SubmittedBy,
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

        // Parse sort parameters
        var (sortField, isDescending) = ParseSortBy(query.SortBy);

        // Apply sorting
        IQueryable<SharedGameEntity> sortedQuery = ApplySorting(dbQuery, sortField, isDescending);

        // Count total before pagination
        var total = await sortedQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        // Apply pagination
        var games = await sortedQuery
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
