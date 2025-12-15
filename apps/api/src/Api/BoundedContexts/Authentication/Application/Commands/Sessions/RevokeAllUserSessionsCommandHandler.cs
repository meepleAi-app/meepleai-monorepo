using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Commands;

internal class RevokeAllUserSessionsCommandHandler : ICommandHandler<RevokeAllUserSessionsCommand, int>
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ISessionCacheService? _sessionCache;
    private readonly ILogger<RevokeAllUserSessionsCommandHandler> _logger;

    public RevokeAllUserSessionsCommandHandler(
        MeepleAiDbContext db,
        ILogger<RevokeAllUserSessionsCommandHandler> logger,
        ISessionCacheService? sessionCache = null,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionCache = sessionCache;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<int> Handle(RevokeAllUserSessionsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var sessions = await _db.UserSessions
            .Where(s => s.UserId == request.UserId && s.RevokedAt == null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (sessions.Count == 0)
        {
            _logger.LogInformation("No active sessions found for user {UserId}", request.UserId);
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
                    await _sessionCache.InvalidateAsync(session.TokenHash, cancellationToken).ConfigureAwait(false);
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

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Revoked {Count} sessions for user {UserId}", sessions.Count, request.UserId);
        return sessions.Count;
    }
}
