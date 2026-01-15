using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Guards;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles game session start command.
/// </summary>
internal class StartGameSessionCommandHandler : ICommandHandler<StartGameSessionCommand, GameSessionDto>
{
    private readonly IGameSessionRepository _sessionRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IUnitOfWork _unitOfWork;

    public StartGameSessionCommandHandler(
        IGameSessionRepository sessionRepository,
        IGameRepository gameRepository,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameSessionDto> Handle(StartGameSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validate input before repository operations
        Guard.AgainstEmptyGuid(command.GameId, nameof(command.GameId));
        Guard.AgainstEmptyCollection(command.Players, nameof(command.Players));

        // Get game to validate existence and player count
        var game = await _gameRepository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false);
        if (game == null)
            throw new InvalidOperationException($"Game with ID {command.GameId} not found");

        // Validate player count against game's limits
        var playerCount = command.Players.Count;
        if (game.PlayerCount != null)
        {
            if (playerCount < game.PlayerCount.Min || playerCount > game.PlayerCount.Max)
            {
                throw new SharedKernel.Domain.Exceptions.ValidationException(
                    nameof(command.Players),
                    $"Player count ({playerCount}) must be between {game.PlayerCount.Min} and {game.PlayerCount.Max} for this game");
            }
        }

        // Create SessionPlayer value objects
        var players = command.Players.Select(p =>
            new SessionPlayer(p.PlayerName, p.PlayerOrder, p.Color)).ToList();

        // Create GameSession aggregate
        var session = new GameSession(
            id: Guid.NewGuid(),
            gameId: command.GameId,
            players: players
        );

        // Start immediately (moves from Setup to InProgress)
        session.Start();

        // Persist
        await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Map to DTO using shared mapper
        return session.ToDto();
    }
}
