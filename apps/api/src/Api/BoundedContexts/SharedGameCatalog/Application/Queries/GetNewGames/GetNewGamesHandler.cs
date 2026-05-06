using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;

/// <summary>
/// Returns the most recently added non-deleted shared games, ordered by CreatedAt descending.
/// Limit is clamped to [1, 20].
/// Issue #728: Discover dashboard — "New Games" widget.
/// </summary>
internal sealed class GetNewGamesHandler : IQueryHandler<GetNewGamesQuery, IReadOnlyList<NewGameDto>>
{
    private const int MaxLimit = 20;
    private const int MinLimit = 1;

    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetNewGamesHandler> _logger;

    public GetNewGamesHandler(MeepleAiDbContext dbContext, ILogger<GetNewGamesHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<NewGameDto>> Handle(GetNewGamesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit, MinLimit, MaxLimit);

        _logger.LogInformation("Fetching {Limit} new shared games", limit);

        return await _dbContext.SharedGames.AsNoTracking()
            .Where(g => !g.IsDeleted)
            .OrderByDescending(g => g.CreatedAt)
            .Take(limit)
            .Select(g => new NewGameDto(
                g.Id,
                g.Title,
                g.ImageUrl,
                g.CreatedAt,
                g.AverageRating
            ))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
