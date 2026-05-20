using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Handles get all games query — now backed by SharedGames table.
/// Issue #1320 (P2c): Migrated from IGameRepository to SharedGames via EF.
/// </summary>
internal class GetAllGamesQueryHandler : IQueryHandler<GetAllGamesQuery, PaginatedGamesResponse>
{
    private readonly MeepleAiDbContext _context;

    public GetAllGamesQueryHandler(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PaginatedGamesResponse> Handle(GetAllGamesQuery query, CancellationToken cancellationToken)
    {
        // Validate and sanitize pagination parameters
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize < 1 ? 20 : query.PageSize, 1, 100);

        var dbQuery = _context.SharedGames.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            dbQuery = dbQuery.Where(g => EF.Functions.ILike(g.Title, $"%{query.Search}%"));
        }

        dbQuery = dbQuery.OrderBy(g => g.Title);

        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var rawGames = await dbQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new
            {
                g.Id,
                g.Title,
                g.YearPublished,
                g.MinPlayers,
                g.MaxPlayers,
                g.PlayingTimeMinutes,
                g.BggId,
                g.CreatedAt,
                g.ImageUrl
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var games = rawGames
            .Select(g => new GameDto(
                Id: g.Id,
                Title: g.Title,
                Publisher: null,
                YearPublished: g.YearPublished,
                MinPlayers: g.MinPlayers,
                MaxPlayers: g.MaxPlayers,
                MinPlayTimeMinutes: g.PlayingTimeMinutes,
                MaxPlayTimeMinutes: g.PlayingTimeMinutes,
                BggId: g.BggId,
                CreatedAt: g.CreatedAt,
                IconUrl: null,
                ImageUrl: g.ImageUrl,
                SharedGameId: null,
                IsPublished: false,
                ApprovalStatus: null,
                PublishedAt: null
            ))
            .ToList();

        var totalPages = total > 0 ? (int)Math.Ceiling((double)total / pageSize) : 0;

        return new PaginatedGamesResponse(
            Games: games,
            Total: total,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages
        );
    }
}
