using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for EndImpersonationCommand (Issue #3349).
/// Terminates an impersonation session and creates audit trail.
/// </summary>
internal sealed class EndImpersonationCommandHandler
    : ICommandHandler<EndImpersonationCommand, bool>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EndImpersonationCommandHandler> _logger;

    public EndImpersonationCommandHandler(
        ISessionRepository sessionRepository,
        IAuditLogRepository auditLogRepository,
        IMediator mediator,
        IUnitOfWork unitOfWork,
        ILogger<EndImpersonationCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(EndImpersonationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogWarning("⚠️ SECURITY: Admin {AdminId} ending impersonation session {SessionId}",
            command.AdminUserId, command.SessionId);

        // Get session to find impersonated user
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            _logger.LogWarning("Session {SessionId} not found for ending impersonation", command.SessionId);
            return false;
        }

        var impersonatedUserId = session.UserId;

        // Revoke the session using existing command
        var revokeCommand = new RevokeSessionCommand(
            SessionId: command.SessionId,
            RequestingUserId: command.AdminUserId,
            IsRequestingUserAdmin: true,
            Reason: "Impersonation ended by admin"
        );

        var revokeResult = await _mediator.Send(revokeCommand, cancellationToken).ConfigureAwait(false);

        if (!revokeResult.Success)
        {
            _logger.LogWarning("Failed to revoke impersonation session {SessionId}: {Error}",
                command.SessionId, revokeResult.ErrorMessage);
            return false;
        }

        // Create audit log for ending impersonation
        var auditLog = new Api.BoundedContexts.Administration.Domain.Entities.AuditLog(
            id: Guid.NewGuid(),
            userId: command.AdminUserId,
            action: "impersonate_user_ended",
            resource: "User",
            result: "success",
            resourceId: impersonatedUserId.ToString(),
            details: System.Text.Json.JsonSerializer.Serialize(new
            {
                sessionId = command.SessionId,
                impersonatedUserId,
                endedByAdminId = command.AdminUserId
            }),
            ipAddress: "admin-action"
        );

        await _auditLogRepository.AddAsync(auditLog, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogWarning("⚠️ SECURITY: Impersonation ended - Admin {AdminId} stopped impersonating User {UserId}",
            command.AdminUserId, impersonatedUserId);

        return true;
    }
}
