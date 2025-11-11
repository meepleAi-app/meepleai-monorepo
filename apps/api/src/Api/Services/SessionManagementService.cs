using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services;

public interface ISessionManagementService
{
    /// <summary>
    /// Gets all active sessions for a specific user.
    /// </summary>
    Task<List<SessionInfo>> GetUserSessionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets all sessions (optionally filtered by user ID) for admin purposes.
    /// </summary>
    Task<List<SessionInfo>> GetAllSessionsAsync(Guid? userId = null, int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Revokes a specific session by session ID.
    /// </summary>
    Task<bool> RevokeSessionAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Revokes all sessions for a specific user (e.g., on password change).
    /// </summary>
    Task<int> RevokeAllUserSessionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Auto-revokes sessions that have been inactive for longer than the configured timeout.
    /// Returns the number of sessions revoked.
    /// </summary>
    Task<int> RevokeInactiveSessionsAsync(CancellationToken ct = default);
}

public class SessionManagementService : ISessionManagementService
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ISessionCacheService? _sessionCache;
    private readonly ILogger<SessionManagementService> _logger;
    private readonly SessionManagementConfiguration _config;

    public SessionManagementService(
        MeepleAiDbContext db,
        IOptions<SessionManagementConfiguration> config,
        ILogger<SessionManagementService> logger,
        ISessionCacheService? sessionCache = null,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionCache = sessionCache;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<List<SessionInfo>> GetUserSessionsAsync(Guid userId, CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var sessions = await _db.UserSessions
            .AsNoTracking() // PERF-05: Read-only query for listing sessions
            .Include(s => s.User)
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastSeenAt ?? s.CreatedAt)
            .Select(s => new SessionInfo(
                s.Id.ToString(),
                s.UserId.ToString(),
                s.User.Email,
                s.CreatedAt,
                s.ExpiresAt,
                s.LastSeenAt,
                s.RevokedAt,
                s.IpAddress,
                s.UserAgent
            ))
            .ToListAsync(ct);

        return sessions;
    }

    public async Task<List<SessionInfo>> GetAllSessionsAsync(Guid? userId = null, int limit = 100, CancellationToken ct = default)
    {
        if (limit <= 0 || limit > 1000)
        {
            throw new ArgumentException("Limit must be between 1 and 1000", nameof(limit));
        }

        var query = _db.UserSessions
            .AsNoTracking() // PERF-05: Read-only query for admin listing
            .Include(s => s.User)
            .AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(s => s.UserId == userId.Value);
        }

        var sessions = await query
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .Select(s => new SessionInfo(
                s.Id.ToString(),
                s.UserId.ToString(),
                s.User.Email,
                s.CreatedAt,
                s.ExpiresAt,
                s.LastSeenAt,
                s.RevokedAt,
                s.IpAddress,
                s.UserAgent
            ))
            .ToListAsync(ct);

        return sessions;
    }

    public async Task<bool> RevokeSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session == null)
        {
            _logger.LogWarning("Attempted to revoke non-existent session {SessionId}", sessionId);
            return false;
        }

        if (session.RevokedAt != null)
        {
            _logger.LogInformation("Session {SessionId} was already revoked at {RevokedAt}", sessionId, session.RevokedAt);
            return false;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        session.RevokedAt = now;
        await _db.SaveChangesAsync(ct);

        // Invalidate cache if present (resilient to cache failures)
        if (_sessionCache != null)
        {
            try
            {
                await _sessionCache.InvalidateAsync(session.TokenHash, ct);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: Service boundary - cache failure resilience for session operations
            // RESILIENCE: Cache failures should not prevent session revocation
            // Database revocation already succeeded, so log warning and continue
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate cache for session {SessionId}, but database revocation succeeded", sessionId);
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation("Session {SessionId} for user {UserId} revoked successfully", sessionId, session.UserId);
        return true;
    }

    public async Task<int> RevokeAllUserSessionsAsync(Guid userId, CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var sessions = await _db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ToListAsync(ct);

        if (sessions.Count == 0)
        {
            _logger.LogInformation("No active sessions found for user {UserId}", userId);
            return 0;
        }

        foreach (var session in sessions)
        {
            session.RevokedAt = now;

            // Invalidate cache if present (resilient to cache failures)
            if (_sessionCache != null)
            {
                try
                {
                    await _sessionCache.InvalidateAsync(session.TokenHash, ct);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                // Justification: Service boundary - cache failure resilience for batch session operations
                // RESILIENCE: Cache failures should not prevent batch session revocation
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to invalidate cache for session {SessionId}, continuing batch revocation", session.Id);
                }
#pragma warning restore CA1031
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Revoked {Count} sessions for user {UserId}", sessions.Count, userId);
        return sessions.Count;
    }

    public async Task<int> RevokeInactiveSessionsAsync(CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var inactivityThreshold = now.AddDays(-_config.InactivityTimeoutDays);

        _logger.LogInformation(
            "Starting auto-revocation check. Inactivity threshold: {Threshold} ({Days} days)",
            inactivityThreshold,
            _config.InactivityTimeoutDays);

        // Find sessions that:
        // 1. Are not already revoked
        // 2. Have LastSeenAt older than threshold (or null and CreatedAt older than threshold)
        // 3. Are not yet expired (we only revoke based on inactivity, not expiration)
        var inactiveSessions = await _db.UserSessions
            .Where(s => s.RevokedAt == null &&
                        s.ExpiresAt > now &&
                        ((s.LastSeenAt != null && s.LastSeenAt < inactivityThreshold) ||
                         (s.LastSeenAt == null && s.CreatedAt < inactivityThreshold)))
            .ToListAsync(ct);

        if (inactiveSessions.Count == 0)
        {
            _logger.LogInformation("No inactive sessions found for auto-revocation");
            return 0;
        }

        foreach (var session in inactiveSessions)
        {
            session.RevokedAt = now;

            // Invalidate cache if present
            if (_sessionCache != null)
            {
                await _sessionCache.InvalidateAsync(session.TokenHash, ct);
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Auto-revoked {Count} inactive sessions (inactive for >{Days} days)",
            inactiveSessions.Count,
            _config.InactivityTimeoutDays);

        return inactiveSessions.Count;
    }
}
