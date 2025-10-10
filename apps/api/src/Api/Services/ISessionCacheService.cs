using Api.Models;

namespace Api.Services;

/// <summary>
/// Redis-based cache for session validation (Phase 2 optimization)
/// Reduces database queries for session validation by ~90%
/// </summary>
public interface ISessionCacheService
{
    /// <summary>
    /// Get cached session by token hash
    /// </summary>
    Task<ActiveSession?> GetAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>
    /// Cache a validated session
    /// </summary>
    Task SetAsync(string tokenHash, ActiveSession session, DateTime expiresAt, CancellationToken ct = default);

    /// <summary>
    /// Invalidate a specific session (e.g., on logout)
    /// </summary>
    Task InvalidateAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>
    /// Invalidate all sessions for a user (e.g., password change)
    /// </summary>
    Task InvalidateUserSessionsAsync(string userId, CancellationToken ct = default);
}
