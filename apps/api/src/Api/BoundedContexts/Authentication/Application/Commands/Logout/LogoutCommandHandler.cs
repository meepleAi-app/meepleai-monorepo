using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles user logout by revoking the session.
/// </summary>
public class LogoutCommandHandler : ICommandHandler<LogoutCommand>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public LogoutCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        // Parse and hash the token
        var sessionToken = SessionToken.FromStored(command.SessionToken);
        var tokenHash = sessionToken.ComputeHash();

        // Find session by token hash
        var session = await _sessionRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (session == null)
            throw new DomainException("Invalid session token");

        // Revoke the session
        session.Revoke();

        await _sessionRepository.UpdateAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
