using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.Authentication.Application.Commands;

internal class RevokeInactiveSessionsCommandHandler : ICommandHandler<RevokeInactiveSessionsCommand, int>
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ISessionCacheService? _sessionCache;
    private readonly ILogger<RevokeInactiveSessionsCommandHandler> _logger;
    private readonly SessionManagementConfiguration _config;

    public RevokeInactiveSessionsCommandHandler(
        MeepleAiDbContext db,
        IOptions<SessionManagementConfiguration> config,
        ILogger<RevokeInactiveSessionsCommandHandler> logger,
        ISessionCacheService? sessionCache = null,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionCache = sessionCache;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<int> Handle(RevokeInactiveSessionsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
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
            .ToListAsync(cancellationToken).ConfigureAwait(false);

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
                await _sessionCache.InvalidateAsync(session.TokenHash, cancellationToken).ConfigureAwait(false);
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Auto-revoked {Count} inactive sessions (inactive for >{Days} days)",
            inactiveSessions.Count,
            _config.InactivityTimeoutDays);

        return inactiveSessions.Count;
    }
}
