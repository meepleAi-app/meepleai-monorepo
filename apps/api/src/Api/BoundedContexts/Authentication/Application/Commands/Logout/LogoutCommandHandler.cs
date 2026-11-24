using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.BoundedContexts.SecurityAudit.Application.Services;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles user logout by revoking the session.
/// </summary>
internal class LogoutCommandHandler : ICommandHandler<LogoutCommand>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    public LogoutCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Parse and hash the token
        var sessionToken = SessionToken.FromStored(command.SessionToken);
        var tokenHash = sessionToken.ComputeHash();

        // Find session by token hash
        var session = await _sessionRepository.GetByTokenHashAsync(tokenHash, cancellationToken).ConfigureAwait(false);

        if (session == null)
            throw new DomainException("Invalid session token");

        // Revoke the session
        session.Revoke();

        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // #3838: an explicit logout is a security event; `revoked_at` alone does not tell it from an expiry.
        await _auditLogger.LogAsync(
            AuditEventType.Logout,
            actorUserId: session.UserId,
            targetUserId: session.UserId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
