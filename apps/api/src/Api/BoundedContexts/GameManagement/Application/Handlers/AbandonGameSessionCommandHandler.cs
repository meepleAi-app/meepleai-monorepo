using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles game session abandon command.
/// </summary>
public class AbandonGameSessionCommandHandler : ICommandHandler<AbandonGameSessionCommand, GameSessionDto>
{
    private readonly IGameSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AbandonGameSessionCommandHandler(
        IGameSessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<GameSessionDto> Handle(AbandonGameSessionCommand command, CancellationToken cancellationToken)
    {
        // Load session
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session with ID {command.SessionId} not found");

        // Abandon via domain method
        session.Abandon(command.Reason);

        // Persist
        await _sessionRepository.UpdateAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Map to DTO using shared mapper
        return session.ToDto();
    }
}