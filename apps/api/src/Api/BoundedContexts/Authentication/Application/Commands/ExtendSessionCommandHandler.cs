using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handler for ExtendSessionCommand.
/// DDD: Uses ISessionRepository and Session domain entity to extend session.
/// AUTH-05: Session management
/// </summary>
public class ExtendSessionCommandHandler : ICommandHandler<ExtendSessionCommand, SessionStatusResponse?>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionCacheService? _sessionCache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ExtendSessionCommandHandler> _logger;

    public ExtendSessionCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ISessionCacheService? sessionCache,
        TimeProvider timeProvider,
        ILogger<ExtendSessionCommandHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _unitOfWork = unitOfWork;
        _sessionCache = sessionCache;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<SessionStatusResponse?> Handle(ExtendSessionCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _sessionRepository.GetByTokenHashAsync(command.TokenHash, cancellationToken);

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

            // Update LastSeenAt using domain method
            session.UpdateLastSeen();

            // Persist changes
            await _sessionRepository.UpdateAsync(session, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Invalidate cache to force refresh on next request
            if (_sessionCache != null)
            {
                await _sessionCache.InvalidateAsync(command.TokenHash, cancellationToken);
            }

            // Calculate new remaining minutes
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var expiryTime = now.AddDays(command.InactivityTimeoutDays);
            var remainingMinutes = (int)Math.Max(0, (expiryTime - now).TotalMinutes);

            return new SessionStatusResponse(
                ExpiresAt: session.ExpiresAt,
                LastSeenAt: now,
                RemainingMinutes: remainingMinutes
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending session for token hash");
            return null;
        }
    }
}
