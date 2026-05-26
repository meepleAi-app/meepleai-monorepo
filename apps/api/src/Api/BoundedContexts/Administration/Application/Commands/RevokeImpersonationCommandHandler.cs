using Api.BoundedContexts.Administration.Application;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for <see cref="RevokeImpersonationCommand"/> (SP5 Admin Security S2 — T5).
///
/// Flow:
///   1. Verify the requester is a superadmin (kill-switch is superadmin-only).
///   2. Load the target session; verify it is an ACTIVE impersonation (ImpersonatedByUserId set,
///      not already revoked).
///   3. Revoke it via the existing RevokeSessionCommand (sets RevokedAt).
///   4. Write a bespoke ImpersonationRevoked audit row: user_id = the impersonating admin
///      (session.ImpersonatedByUserId), impersonated_user_id = the superadmin caller — Scenario S2-4.
///
/// Audit is best-effort (EnqueueAuditAsync): the revoke is the security-critical action and must
/// succeed even if audit enqueue fails (revoke-first). Propagation to other in-flight requests is
/// handled by invalidate-on-read in ValidateSessionQuery (reads RevokedAt every request; no cache),
/// so the revoke takes effect on the next request — well within the ≤5s D-S2-5 SLA.
/// </summary>
internal sealed class RevokeImpersonationCommandHandler
    : ICommandHandler<RevokeImpersonationCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IMediator _mediator;
    private readonly AuditService _auditService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RevokeImpersonationCommandHandler> _logger;

    public RevokeImpersonationCommandHandler(
        IUserRepository userRepository,
        ISessionRepository sessionRepository,
        IMediator mediator,
        AuditService auditService,
        TimeProvider timeProvider,
        ILogger<RevokeImpersonationCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(RevokeImpersonationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogWarning("SECURITY: superadmin {RequesterId} revoking impersonation session {SessionId}",
            command.RequestingUserId, command.SessionId);

        // 1. Requester must be a superadmin.
        var requester = await _userRepository.GetByIdAsync(command.RequestingUserId, cancellationToken)
            .ConfigureAwait(false);
        if (requester is null)
        {
            throw new NotFoundException($"Requesting user with ID '{command.RequestingUserId}' not found");
        }
        if (!string.Equals(requester.Role.Value, "superadmin", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Only superadmins can revoke an impersonation session");
        }

        // 2. Load + validate the target session is an active impersonation.
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false);
        if (session is null)
        {
            _logger.LogWarning("Session {SessionId} not found for impersonation revoke", command.SessionId);
            return false;
        }
        if (!session.IsImpersonation)
        {
            throw new ConflictException($"Session '{command.SessionId}' is not an impersonation session");
        }
        if (session.IsRevoked())
        {
            // Idempotent: already revoked — nothing to do, treat as success.
            _logger.LogInformation("Impersonation session {SessionId} already revoked", command.SessionId);
            return true;
        }

        var impersonatingAdminId = session.ImpersonatedByUserId; // the actor whose impersonation we kill

        // 3. Revoke via the existing generic session-revoke command (sets RevokedAt).
        var revokeResult = await _mediator.Send(
            new RevokeSessionCommand(
                SessionId: command.SessionId,
                RequestingUserId: command.RequestingUserId,
                IsRequestingUserAdmin: true,
                Reason: "Impersonation revoked by superadmin (kill-switch)"),
            cancellationToken).ConfigureAwait(false);

        if (!revokeResult.Success)
        {
            _logger.LogWarning("Failed to revoke impersonation session {SessionId}: {Error}",
                command.SessionId, revokeResult.ErrorMessage);
            return false;
        }

        // 4. Bespoke audit (Scenario S2-4): user_id = impersonating admin (subject of the revoke),
        // impersonated_user_id = superadmin caller (actor). Best-effort — revoke already committed.
        await _auditService.EnqueueAuditAsync(new AuditOutboxPayload
        {
            Action = "ImpersonationRevoked",
            Resource = "Session",
            UserId = impersonatingAdminId?.ToString(),
            ResourceId = command.SessionId.ToString(),
            Result = "Success",
            IpAddress = null,
            UserAgent = null,
            RequestType = nameof(RevokeImpersonationCommand),
            Details = "{}",
            ImpersonatedUserId = command.RequestingUserId,
            StepUpTokenId = null,
            Timestamp = _timeProvider.GetUtcNow(),
            Oversize = false,
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogWarning(
            "SECURITY: impersonation revoked by superadmin {RequesterId} — killed session {SessionId} of admin {AdminId}",
            command.RequestingUserId, command.SessionId, impersonatingAdminId);

        return true;
    }
}
