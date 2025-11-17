using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
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
    private readonly ILogger<RevokeSessionCommandHandler> _logger;

    public RevokeSessionCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ILogger<RevokeSessionCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RevokeSessionResponse> Handle(RevokeSessionCommand command, CancellationToken cancellationToken)
    {
        // Retrieve session
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken);

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

            session.Revoke(reason);

            // Persist changes (domain events will be collected automatically)
            await _sessionRepository.UpdateAsync(session, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

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
