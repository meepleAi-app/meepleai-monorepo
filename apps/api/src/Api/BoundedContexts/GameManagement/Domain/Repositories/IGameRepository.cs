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

    /// <summary>
    /// Gets all games linked to the specified SharedGameCatalog entry.
    /// Used by the cross-BC handler for SharedGameDeleted events.
    /// Spec-panel recommendation C-2.
    /// </summary>
    Task<IReadOnlyList<Game>> GetBySharedGameIdAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a caller-supplied game identifier against BOTH the primary key
    /// <c>Game.Id</c> and the bridge FK <c>Game.SharedGameId</c>, preferring a
    /// direct PK match.
    ///
    /// <para>
    /// Rationale: public surfaces (e.g. <c>GET /api/v1/library</c>) expose the
    /// <c>SharedGameId</c> as the user-facing <c>gameId</c>, while write/read
    /// paths like <c>/games/{id}/agents</c> target the <c>games</c> table PK.
    /// Without this fallback the frontend hits a spurious 404 when the two
    /// identifiers diverge (which they do in production, though seeders happen
    /// to align them in dev).
    /// </para>
    ///
    /// Same resolution pattern as <c>CreateChatThreadCommandHandler</c> (PR #414).
    /// </summary>
    Task<Game?> GetByIdOrSharedGameIdAsync(Guid id, CancellationToken cancellationToken = default);
}
