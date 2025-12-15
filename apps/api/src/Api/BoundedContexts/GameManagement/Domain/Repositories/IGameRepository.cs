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
}
