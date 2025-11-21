using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles get all games query.
/// </summary>
public class GetAllGamesQueryHandler : IQueryHandler<GetAllGamesQuery, IReadOnlyList<GameDto>>
{
    private readonly IGameRepository _gameRepository;

    public GetAllGamesQueryHandler(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
    }

    public async Task<IReadOnlyList<GameDto>> Handle(GetAllGamesQuery query, CancellationToken cancellationToken)
    {
        var games = await _gameRepository.GetAllAsync(cancellationToken);

        return games.Select(MapToDto).ToList();
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
