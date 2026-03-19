using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Handler for batch checking game library status.
/// Optimized for performance: single query with IN clause instead of N queries.
/// Issue: N+1 API calls optimization (game grids making 20+ individual status checks)
/// </summary>
internal class BatchCheckGamesInLibraryQueryHandler
    : IQueryHandler<BatchCheckGamesInLibraryQuery, BatchGameLibraryStatusDto>
{
    private readonly MeepleAiDbContext _dbContext;

    public BatchCheckGamesInLibraryQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<BatchGameLibraryStatusDto> Handle(
        BatchCheckGamesInLibraryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Single optimized query: fetch all matching entries in one DB call
        var entries = await _dbContext.UserLibraryEntries
            .Where(ulg => ulg.UserId == query.UserId && query.GameIds.Contains(ulg.GameId))
            .Select(ulg => new
            {
                ulg.GameId,
                ulg.IsFavorite
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Build result dictionary
        var results = new Dictionary<Guid, GameLibraryStatusSimpleDto>();

        foreach (var gameId in query.GameIds)
        {
            var entry = entries.FirstOrDefault(e => e.GameId == gameId);

            if (entry == null)
            {
                // Not in library
                results[gameId] = new GameLibraryStatusSimpleDto(
                    InLibrary: false,
                    IsFavorite: false,
                    IsOwned: false
                );
            }
            else
            {
                // In library (owned)
                results[gameId] = new GameLibraryStatusSimpleDto(
                    InLibrary: true,
                    IsFavorite: entry.IsFavorite,
                    IsOwned: true // If in UserLibraryEntries, it's owned
                );
            }
        }

        return new BatchGameLibraryStatusDto(
            Results: results,
            TotalChecked: query.GameIds.Count
        );
    }
}
