using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Commands;

public class RevokeSessionCommandHandler : ICommandHandler<RevokeSessionCommand, bool>
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ISessionCacheService? _sessionCache;
    private readonly ILogger<RevokeSessionCommandHandler> _logger;

    public RevokeSessionCommandHandler(
        MeepleAiDbContext db,
        ILogger<RevokeSessionCommandHandler> logger,
        ISessionCacheService? sessionCache = null,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionCache = sessionCache;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<bool> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
        if (session == null)
        {
            _logger.LogWarning("Attempted to revoke non-existent session {SessionId}", request.SessionId);
            return false;
        }

        if (session.RevokedAt != null)
        {
            _logger.LogInformation("Session {SessionId} was already revoked at {RevokedAt}", request.SessionId, session.RevokedAt);
            return false;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        session.RevokedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        // Invalidate cache if present (resilient to cache failures)
        if (_sessionCache != null)
        {
            try
            {
                await _sessionCache.InvalidateAsync(session.TokenHash, cancellationToken);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: Service boundary - cache failure resilience for session operations
            // RESILIENCE: Cache failures should not prevent session revocation
            // Database revocation already succeeded, so log warning and continue
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate cache for session {SessionId}, but database revocation succeeded", request.SessionId);
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation("Session {SessionId} for user {UserId} revoked successfully", request.SessionId, session.UserId);
        return true;
    }
}
