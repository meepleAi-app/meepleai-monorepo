using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles get all games query.
/// </summary>
internal class GetAllGamesQueryHandler : IQueryHandler<GetAllGamesQuery, PaginatedGamesResponse>
{
    private readonly IGameRepository _gameRepository;

    public GetAllGamesQueryHandler(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
    }

    public async Task<PaginatedGamesResponse> Handle(GetAllGamesQuery query, CancellationToken cancellationToken)
    {
        // Validate and sanitize pagination parameters
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize < 1 ? 20 : query.PageSize, 1, 100);

        // Get paginated games from repository
        var (games, total) = await _gameRepository.GetPaginatedAsync(
            query.Search,
            page,
            pageSize,
            cancellationToken
        ).ConfigureAwait(false);

        // Map to DTOs
        var gameDtos = games.Select(MapToDto).ToList();

        // Calculate total pages
        var totalPages = total > 0 ? (int)Math.Ceiling((double)total / pageSize) : 0;

        return new PaginatedGamesResponse(
            Games: gameDtos,
            Total: total,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages
        );
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
