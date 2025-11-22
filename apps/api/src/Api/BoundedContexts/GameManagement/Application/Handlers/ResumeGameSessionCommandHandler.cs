using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles resume game session command.
/// </summary>
public class ResumeGameSessionCommandHandler : ICommandHandler<ResumeGameSessionCommand, GameSessionDto>
{
    private readonly IGameSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ResumeGameSessionCommandHandler(
        IGameSessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameSessionDto> Handle(ResumeGameSessionCommand command, CancellationToken cancellationToken)
    {
        // Fetch session
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken);
        if (session == null)
            throw new InvalidOperationException($"Session with ID {command.SessionId} not found");

        // Resume (domain method validates state transition)
        session.Resume();

        // Persist
        await _sessionRepository.UpdateAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Map to DTO using shared mapper
        return session.ToDto();
    }
}