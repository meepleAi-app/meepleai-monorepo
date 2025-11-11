using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles game creation command.
/// </summary>
public class CreateGameCommandHandler : ICommandHandler<CreateGameCommand, GameDto>
{
    private readonly IGameRepository _gameRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateGameCommandHandler(
        IGameRepository gameRepository,
        IUnitOfWork unitOfWork)
    {
        _gameRepository = gameRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<GameDto> Handle(CreateGameCommand command, CancellationToken cancellationToken)
    {
        // Create value objects
        var title = new GameTitle(command.Title);
        var publisher = command.Publisher != null ? new Publisher(command.Publisher) : null;
        var yearPublished = command.YearPublished.HasValue ? new YearPublished(command.YearPublished.Value) : null;

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

        // Create Game aggregate
        var game = new Game(
            id: Guid.NewGuid(),
            title: title,
            publisher: publisher,
            yearPublished: yearPublished,
            playerCount: playerCount,
            playTime: playTime
        );

        // Persist
        await _gameRepository.AddAsync(game, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Map to DTO
        return MapToDto(game);
    }

    private static GameDto MapToDto(Game game)
    {
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
