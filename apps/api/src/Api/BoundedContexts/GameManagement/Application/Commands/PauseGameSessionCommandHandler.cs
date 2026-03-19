using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Handles pause game session command.
/// </summary>
internal class PauseGameSessionCommandHandler : ICommandHandler<PauseGameSessionCommand, GameSessionDto>
{
    private readonly IGameSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PauseGameSessionCommandHandler(
        IGameSessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameSessionDto> Handle(PauseGameSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Fetch session
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken).ConfigureAwait(false);
        if (session == null)
            throw new InvalidOperationException($"Session with ID {command.SessionId} not found");

        // Pause (domain method validates state transition)
        session.Pause();

        // Persist
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Map to DTO using shared mapper
        return session.ToDto();
    }
}
