using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for getting all shared games in PendingApproval status.
/// Used by admin UI to display games awaiting approval.
/// Issue #2514: Approval workflow implementation
/// </summary>
internal sealed class GetPendingApprovalGamesQueryHandler : IRequestHandler<GetPendingApprovalGamesQuery, PagedResult<SharedGameDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<GetPendingApprovalGamesQueryHandler> _logger;

    public GetPendingApprovalGamesQueryHandler(
        MeepleAiDbContext context,
        ILogger<GetPendingApprovalGamesQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<SharedGameDto>> Handle(GetPendingApprovalGamesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting pending approval games: Page={Page}, PageSize={PageSize}",
            query.PageNumber,
            query.PageSize);

        // Filter by PendingApproval status
        var dbQuery = _context.SharedGames
            .AsNoTracking()
            .Where(g => g.Status == (int)GameStatus.PendingApproval)
            .OrderBy(g => g.ModifiedAt ?? g.CreatedAt); // Oldest submissions first

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
                g.ModifiedAt,
                g.IsRagPublic))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Retrieved {Count} pending approval games (Total: {Total}) for page {Page}",
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
