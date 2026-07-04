using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Handles <see cref="OpenSessionLiveModeCommand"/> (WS1 DEC-1/6). Loads the Session,
/// opens live mode (unless already live — DEC-6 idempotency), and saves. The
/// <c>SessionStartedDomainEvent</c> raised by <c>Session.OpenLiveMode()</c> is
/// collected by <c>ISessionRepository.UpdateAsync</c> and dispatched post-commit
/// (ADR-060), driving the GameNight Published → InProgress promotion.
/// </summary>
internal sealed class OpenSessionLiveModeCommandHandler : ICommandHandler<OpenSessionLiveModeCommand>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OpenSessionLiveModeCommandHandler(ISessionRepository sessionRepository, IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(OpenSessionLiveModeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {command.SessionId} not found");

        // DEC-6: idempotent — a retry across the multi-SaveChanges use case can reach an
        // already-live session; treat as success, do NOT surface the max-1-live 409.
        if (session.IsLive)
            return;

        session.OpenLiveMode();
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
