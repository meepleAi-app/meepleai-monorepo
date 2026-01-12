using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for searching shared games with full-text search and filtering.
/// Uses PostgreSQL full-text search for optimal Italian language support.
/// </summary>
internal sealed class SearchSharedGamesQueryHandler : IRequestHandler<SearchSharedGamesQuery, PagedResult<SharedGameDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<SearchSharedGamesQueryHandler> _logger;

    public SearchSharedGamesQueryHandler(
        MeepleAiDbContext context,
        ILogger<SearchSharedGamesQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<SharedGameDto>> Handle(SearchSharedGamesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Searching shared games: SearchTerm={SearchTerm}, Categories={CategoryCount}, Mechanics={MechanicCount}, Page={Page}",
            query.SearchTerm,
            query.CategoryIds?.Count ?? 0,
            query.MechanicIds?.Count ?? 0,
            query.PageNumber);

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

        // Full-text search: SearchTerm using PostgreSQL FTS
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm.Trim();
            dbQuery = dbQuery.Where(g =>
                EF.Functions.ToTsVector("italian", g.Title + " " + g.Description)
                    .Matches(EF.Functions.PlainToTsQuery("italian", searchTerm)));
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
}
