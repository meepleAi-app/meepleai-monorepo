using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles game update command.
/// </summary>
public class UpdateGameCommandHandler : ICommandHandler<UpdateGameCommand, GameDto>
{
    private readonly IGameRepository _gameRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateGameCommandHandler(
        IGameRepository gameRepository,
        IUnitOfWork unitOfWork)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameDto> Handle(UpdateGameCommand command, CancellationToken cancellationToken)
    {
        // Load game
        var game = await _gameRepository.GetByIdAsync(command.GameId, cancellationToken)
.ConfigureAwait(false) ?? throw new InvalidOperationException($"Game with ID {command.GameId} not found");

        // Update value objects
        GameTitle? title = command.Title != null ? new GameTitle(command.Title) : null;
        Publisher? publisher = command.Publisher != null ? new Publisher(command.Publisher) : null;
        YearPublished? yearPublished = command.YearPublished.HasValue ? new YearPublished(command.YearPublished.Value) : null;

        PlayerCount? playerCount = null;
        if (command.MinPlayers.HasValue && command.MaxPlayers.HasValue)
        {
            playerCount = new PlayerCount(command.MinPlayers.Value, command.MaxPlayers.Value);
        }

        PlayTime? playTime = null;
        if (command.MinPlayTimeMinutes.HasValue && command.MaxPlayTimeMinutes.HasValue)
        {
            playTime = new PlayTime(command.MinPlayTimeMinutes.Value, command.MaxPlayTimeMinutes.Value);
        }

        // Call domain method
        game.UpdateDetails(
            title: title,
            publisher: publisher,
            yearPublished: yearPublished,
            playerCount: playerCount,
            playTime: playTime
        );

        // Persist
        await _gameRepository.UpdateAsync(game, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Map to DTO
        return new GameDto(
            Id: game.Id,
            Title: game.Title.Value,
            Publisher: game.Publisher?.Name,
            YearPublished: game.YearPublished?.Value,
            MinPlayers: game.PlayerCount?.Min,
            MaxPlayers: game.PlayerCount?.Max,
            MinPlayTimeMinutes: game.PlayTime?.MinMinutes,
            MaxPlayTimeMinutes: game.PlayTime?.MaxMinutes,
            BggId: game.BggId,
            CreatedAt: game.CreatedAt
        );
    }
}
