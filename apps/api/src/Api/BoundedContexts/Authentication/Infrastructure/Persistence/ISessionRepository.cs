using Api.BoundedContexts.Authentication.Domain.Entities;

namespace Api.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// Repository interface for Session entity.
/// Note: Session is not an aggregate root, but we provide repository for query convenience.
/// Sessions are managed through the User aggregate root.
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// Finds a session by its token hash.
    /// </summary>
    Task<Session?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all sessions for a specific user.
    /// </summary>
    Task<List<Session>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active (valid) sessions for a user.
    /// </summary>
    Task<List<Session>> GetActiveSessionsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new session.
    /// </summary>
    Task AddAsync(Session session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing session.
    /// </summary>
    Task UpdateAsync(Session session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all sessions for a user.
    /// </summary>
    Task RevokeAllUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
