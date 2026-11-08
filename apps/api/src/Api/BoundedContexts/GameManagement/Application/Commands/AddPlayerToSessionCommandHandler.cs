using Api.Middleware.Exceptions;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Handler for AddPlayerToSessionCommand.
/// Adds a new player to an existing game session.
/// </summary>
internal class AddPlayerToSessionCommandHandler : ICommandHandler<AddPlayerToSessionCommand, GameSessionDto>
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
        ArgumentNullException.ThrowIfNull(command);
        // Get existing session
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken).ConfigureAwait(false);
        if (session == null)
            // #3662: era InvalidOperationException. Il codice HTTP usciva giusto per caso (il
            // middleware mappa quel tipo a 404), ma la convenzione #2568 vuole
            // NotFoundException, che dichiara l'intento invece di ottenerlo per effetto
            // collaterale del mapping.
            throw new NotFoundException("GameSession", command.SessionId.ToString());

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
