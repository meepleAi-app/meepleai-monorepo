using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handler for AddPlayerToSessionCommand.
/// Adds a new player to an existing game session.
/// </summary>
public class AddPlayerToSessionCommandHandler : ICommandHandler<AddPlayerToSessionCommand, GameSessionDto>
{
    private readonly IGameSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddPlayerToSessionCommandHandler(
        IGameSessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameSessionDto> Handle(AddPlayerToSessionCommand command, CancellationToken cancellationToken)
    {
        // Get existing session
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken).ConfigureAwait(false);
        if (session == null)
            throw new InvalidOperationException($"Session with ID {command.SessionId} not found");

        // Create SessionPlayer value object
        var player = new SessionPlayer(command.PlayerName, command.PlayerOrder, command.Color);

        // Add player to session (domain method handles validation)
        session.AddPlayer(player);

        // Persist
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Map to DTO using shared mapper
        return session.ToDto();
    }
}
