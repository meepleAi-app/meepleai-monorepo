using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handler for RevokeSessionCommand with authorization and audit tracking.
/// Verifies that requesting user owns the session OR has Admin role.
/// </summary>
public class RevokeSessionCommandHandler : ICommandHandler<RevokeSessionCommand, RevokeSessionResponse>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionCacheService? _sessionCache;
    private readonly ILogger<RevokeSessionCommandHandler> _logger;

    public RevokeSessionCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ILogger<RevokeSessionCommandHandler> logger,
        ISessionCacheService? sessionCache = null)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionCache = sessionCache;
    }

    public async Task<RevokeSessionResponse> Handle(RevokeSessionCommand command, CancellationToken cancellationToken)
    {
        // Retrieve session
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken).ConfigureAwait(false);

        if (session == null)
        {
            _logger.LogWarning("Session {SessionId} not found", command.SessionId);
            return new RevokeSessionResponse(false, "Session not found");
        }

        // Authorization check: User must own the session OR be an admin
        if (session.UserId != command.RequestingUserId && !command.IsRequestingUserAdmin)
        {
            _logger.LogWarning(
                "User {UserId} attempted to revoke session {SessionId} owned by {OwnerId} without admin privileges",
                command.RequestingUserId, command.SessionId, session.UserId);
            return new RevokeSessionResponse(false, "Unauthorized to revoke this session");
        }

        // Revoke session using domain logic
        try
        {
            var reason = command.Reason ??
                (command.IsRequestingUserAdmin && session.UserId != command.RequestingUserId
                    ? $"Revoked by admin {command.RequestingUserId}"
                    : "Revoked by user");

            session.Revoke(reason: reason);

            // Persist changes (domain events will be collected automatically)
            await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Invalidate cache to ensure immediate effect
            if (_sessionCache != null)
            {
                try
                {
                    await _sessionCache.InvalidateAsync(session.TokenHash, cancellationToken).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                // Justification: Service boundary - cache failure resilience
                // RESILIENCE: Cache failures should not prevent session revocation
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to invalidate cache for session {SessionId}, session revoked in database", command.SessionId);
                }
#pragma warning restore CA1031
            }

            _logger.LogInformation(
                "Session {SessionId} revoked by user {RequestingUserId}. Reason: {Reason}",
                command.SessionId, command.RequestingUserId, reason);

            return new RevokeSessionResponse(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke session {SessionId}", command.SessionId);
            return new RevokeSessionResponse(false, ex.Message);
        }
    }
}
