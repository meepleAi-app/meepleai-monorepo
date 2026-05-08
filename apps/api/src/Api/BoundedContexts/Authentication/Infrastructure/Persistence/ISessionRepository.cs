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
    /// Finds a session by its ID.
    /// </summary>
    Task<Session?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a session by its token hash.
    /// </summary>
    Task<Session?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all sessions for a specific user.
    /// </summary>
    Task<IReadOnlyList<Session>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active (valid) sessions for a user.
    /// </summary>
    Task<IReadOnlyList<Session>> GetActiveSessionsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new session.
    /// </summary>
    Task AddAsync(Session session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing session.
    /// </summary>
    Task UpdateAsync(Session session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the LastSeenAt timestamp for a session without tracking the entity.
    /// </summary>
    Task UpdateLastSeenAsync(Guid sessionId, DateTime lastSeenAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all sessions for a user.
    /// </summary>
    Task RevokeAllUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// C7: revokes every session for the user except the one identified by
    /// <paramref name="excludedSessionId"/>. Used by the password-change flow
    /// to invalidate other devices while keeping the user logged in on the
    /// device that issued the change.
    /// </summary>
    Task RevokeAllUserSessionsExceptAsync(
        Guid userId,
        Guid excludedSessionId,
        CancellationToken cancellationToken = default);
}
