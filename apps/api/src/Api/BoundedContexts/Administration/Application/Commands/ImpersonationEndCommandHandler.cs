using Api.BoundedContexts.Authentication.Application.Commands;
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
/// </summary>
internal sealed class ImpersonationEndCommandHandler
    : ICommandHandler<ImpersonationEndCommand, bool>
{
    private readonly IMediator _mediator;
    private readonly ILogger<ImpersonationEndCommandHandler> _logger;

    public ImpersonationEndCommandHandler(
        IMediator mediator,
        ILogger<ImpersonationEndCommandHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(ImpersonationEndCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogWarning("SECURITY: admin {RequesterId} ending impersonation session {SessionId}",
            command.RequestingUserId, command.SessionId);

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
