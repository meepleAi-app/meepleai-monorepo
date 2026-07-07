using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for <see cref="ImpersonationEndCommand"/> (SP5 Admin Security S2 — T4).
/// Revokes the impersonation session via the existing <see cref="RevokeSessionCommand"/>.
///
/// Audit is written by <c>AuditLoggingBehavior</c> (the command is <c>[AuditableAction]</c>),
/// replacing the legacy manual <c>IAuditLogRepository.AddAsync</c> call (issue #1534). The
/// handler therefore no longer takes <c>IAuditLogRepository</c> or <c>IUnitOfWork</c>.
///
/// <para>Q3 Security (#2655, finding #9): this is a SELF-END endpoint — the acting admin ends
/// their OWN impersonation. Before delegating to the (admin-privileged) generic revoke, the
/// handler verifies ownership: the target session must (1) exist, (2) be an active impersonation
/// and (3) have been started by the requesting admin (<c>ImpersonatedByUserId == RequestingUserId</c>).
/// Without these guards any admin could revoke an arbitrary session id — force-logging-out any
/// user, or killing another admin's impersonation (that is the superadmin-only <c>/revoke</c>
/// kill-switch, <see cref="RevokeImpersonationCommandHandler"/>).</para>
/// </summary>
internal sealed class ImpersonationEndCommandHandler
    : ICommandHandler<ImpersonationEndCommand, bool>
{
    private readonly IMediator _mediator;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<ImpersonationEndCommandHandler> _logger;

    public ImpersonationEndCommandHandler(
        IMediator mediator,
        ISessionRepository sessionRepository,
        ILogger<ImpersonationEndCommandHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(ImpersonationEndCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogWarning("SECURITY: admin {RequesterId} ending impersonation session {SessionId}",
            command.RequestingUserId, command.SessionId);

        // Ownership guards (finding #9) — run BEFORE any state-changing delegation. Ordering keeps
        // us from leaking the revoke-state of sessions the caller does not own: not-found → 404,
        // non-impersonation or another admin's impersonation → 403, then idempotent already-revoked.
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false);
        if (session is null)
        {
            _logger.LogWarning("Impersonation end: session {SessionId} not found", command.SessionId);
            throw new NotFoundException($"Session '{command.SessionId}' not found");
        }
        if (!session.IsImpersonation)
        {
            _logger.LogWarning(
                "SECURITY: admin {RequesterId} attempted to end non-impersonation session {SessionId}",
                command.RequestingUserId, command.SessionId);
            throw new ForbiddenException("Session is not an impersonation session");
        }
        if (session.ImpersonatedByUserId != command.RequestingUserId)
        {
            _logger.LogWarning(
                "SECURITY: admin {RequesterId} attempted to end impersonation session {SessionId} started by {OwnerId}",
                command.RequestingUserId, command.SessionId, session.ImpersonatedByUserId);
            throw new ForbiddenException("You can only end an impersonation session you started");
        }
        if (session.IsRevoked())
        {
            // Idempotent: the caller's own impersonation is already revoked — nothing to do.
            _logger.LogInformation("Impersonation session {SessionId} already revoked", command.SessionId);
            return true;
        }

        // Revoke the session using the existing generic session-revoke command. RevokeSessionCommand
        // handles the not-found / already-revoked cases and returns a structured result.
        var revokeCommand = new RevokeSessionCommand(
            SessionId: command.SessionId,
            RequestingUserId: command.RequestingUserId,
            IsRequestingUserAdmin: true,
            Reason: "Impersonation ended by admin"
        );

        var revokeResult = await _mediator.Send(revokeCommand, cancellationToken).ConfigureAwait(false);

        if (!revokeResult.Success)
        {
            _logger.LogWarning("Failed to end impersonation session {SessionId}: {Error}",
                command.SessionId, revokeResult.ErrorMessage);
            return false;
        }

        // NB: no manual audit write — AuditLoggingBehavior records action="ImpersonationEnded".
        _logger.LogWarning("SECURITY: impersonation ended by admin {RequesterId} (session {SessionId})",
            command.RequestingUserId, command.SessionId);

        return true;
    }
}
