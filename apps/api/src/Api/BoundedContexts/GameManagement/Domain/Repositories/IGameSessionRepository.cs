using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

/// <summary>
/// Repository interface for GameSession aggregate.
/// </summary>
public interface IGameSessionRepository : IRepository<GameSession, Guid>
{
    /// <summary>
    /// Finds active sessions for a specific game.
    /// </summary>
    Task<IReadOnlyList<GameSession>> FindActiveByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all sessions for a specific game.
    /// </summary>
    Task<IReadOnlyList<GameSession>> FindByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds sessions by player name.
    /// </summary>
    Task<IReadOnlyList<GameSession>> FindByPlayerNameAsync(string playerName, CancellationToken cancellationToken = default);
}
