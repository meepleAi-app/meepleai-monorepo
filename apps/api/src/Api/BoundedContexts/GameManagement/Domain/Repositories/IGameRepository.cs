using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

/// <summary>
/// Repository interface for Game aggregate.
/// Extends IRepository with game-specific queries.
/// </summary>
internal interface IGameRepository : IRepository<Game, Guid>
{
    /// <summary>
    /// Finds games by title (normalized search).
    /// </summary>
    Task<IReadOnlyList<Game>> FindByTitleAsync(string titlePattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paginated list of games with optional search filter.
    /// Issue: Fix empty games page - support pagination and search.
    /// </summary>
    /// <param name="search">Optional search term to filter by title</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of games list and total count</returns>
    Task<(IReadOnlyList<Game> Games, int Total)> GetPaginatedAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    );
}
