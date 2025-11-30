using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles get game by ID query.
/// </summary>
public class GetGameByIdQueryHandler : IQueryHandler<GetGameByIdQuery, GameDto?>
{
    private readonly IGameRepository _gameRepository;

    public GetGameByIdQueryHandler(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
    }

    public async Task<GameDto?> Handle(GetGameByIdQuery query, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(query.GameId, cancellationToken).ConfigureAwait(false);

        return game != null ? MapToDto(game) : null;
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
