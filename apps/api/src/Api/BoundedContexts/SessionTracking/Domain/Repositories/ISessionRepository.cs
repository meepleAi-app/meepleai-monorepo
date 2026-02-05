using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

/// <summary>
/// Repository interface for Session aggregate.
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// Gets a session by its unique identifier.
    /// </summary>
    /// <param name="id">Session ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Session if found, null otherwise.</returns>
    Task<Session?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Gets a session by its unique session code.
    /// </summary>
    /// <param name="code">6-character session code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Session if found, null otherwise.</returns>
    Task<Session?> GetByCodeAsync(string code, CancellationToken ct);

    /// <summary>
    /// Gets a session by its invite token (Issue #3354).
    /// </summary>
    /// <param name="inviteToken">Invite token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Session if found, null otherwise.</returns>
    Task<Session?> GetByInviteTokenAsync(string inviteToken, CancellationToken ct);

    /// <summary>
    /// Gets all active sessions for a user.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Collection of active sessions.</returns>
    Task<IEnumerable<Session>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Adds a new session.
    /// </summary>
    /// <param name="session">Session to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(Session session, CancellationToken ct);

    /// <summary>
    /// Updates an existing session.
    /// </summary>
    /// <param name="session">Session to update.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateAsync(Session session, CancellationToken ct);

    /// <summary>
    /// Soft deletes a session.
    /// </summary>
    /// <param name="id">Session ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(Guid id, CancellationToken ct);
}