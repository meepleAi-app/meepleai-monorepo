using Api.BoundedContexts.GameManagement.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

/// <summary>
/// Repository interface for GameStrategy read operations.
/// Issue #4903: Game strategies API endpoint.
/// </summary>
internal interface IGameStrategyRepository
{
    /// <summary>
    /// Gets a paginated list of strategies for a shared game.
    /// </summary>
    Task<(IReadOnlyList<GameStrategy> Items, int TotalCount)> GetBySharedGameIdAsync(
        Guid sharedGameId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
