using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Handler for GetSessionStatusQuery.
/// DDD: Uses ISessionRepository to fetch session status.
/// AUTH-05: Session management
/// </summary>
public class GetSessionStatusQueryHandler : IQueryHandler<GetSessionStatusQuery, SessionStatusResponse?>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GetSessionStatusQueryHandler> _logger;

    public GetSessionStatusQueryHandler(
        ISessionRepository sessionRepository,
        TimeProvider timeProvider,
        ILogger<GetSessionStatusQueryHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<SessionStatusResponse?> Handle(GetSessionStatusQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _sessionRepository.GetByTokenHashAsync(query.TokenHash, cancellationToken);

            if (session == null)
            {
                _logger.LogWarning("Session not found for token hash");
                return null;
            }

            if (session.IsRevoked())
            {
                _logger.LogWarning("Session {SessionId} is revoked", session.Id);
                return null;
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Calculate remaining minutes until session expires from inactivity
            var lastActivity = session.LastSeenAt ?? session.CreatedAt;
            var expiryTime = lastActivity.AddDays(query.InactivityTimeoutDays);
            var remainingMinutes = (int)Math.Max(0, (expiryTime - now).TotalMinutes);

            return new SessionStatusResponse(
                ExpiresAt: session.ExpiresAt,
                LastSeenAt: session.LastSeenAt,
                RemainingMinutes: remainingMinutes
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session status for token hash");
            return null;
        }
    }
}
