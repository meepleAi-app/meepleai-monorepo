using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles get game details query with extended metadata and statistics.
/// </summary>
public class GetGameDetailsQueryHandler : IQueryHandler<GetGameDetailsQuery, GameDetailsDto?>
{
    private readonly IGameRepository _gameRepository;
    private readonly IGameSessionRepository _sessionRepository;

    public GetGameDetailsQueryHandler(
        IGameRepository gameRepository,
        IGameSessionRepository sessionRepository)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<GameDetailsDto?> Handle(GetGameDetailsQuery query, CancellationToken cancellationToken)
    {
        var game = await _gameRepository.GetByIdAsync(query.GameId, cancellationToken).ConfigureAwait(false);

        if (game == null)
            return null;

        // Get play statistics from sessions
        var sessions = await _sessionRepository.FindByGameIdAsync(query.GameId, cancellationToken).ConfigureAwait(false);
        var completedSessions = sessions
            .Where(s => s.Status == SessionStatus.Completed)
            .ToList();

        var totalSessionsPlayed = completedSessions.Count;
        var lastPlayedAt = completedSessions
            .OrderByDescending(s => s.CompletedAt)
            .Select(s => s.CompletedAt)
            .FirstOrDefault();

        return MapToDetailsDto(game, totalSessionsPlayed > 0 ? totalSessionsPlayed : null, lastPlayedAt);
    }

    private static GameDetailsDto MapToDetailsDto(Game game, int? totalSessions, DateTime? lastPlayed)
    {
        return new GameDetailsDto(
            Id: game.Id,
            Title: game.Title.Value,
            Publisher: game.Publisher?.Name,
            YearPublished: game.YearPublished?.Value,
            MinPlayers: game.PlayerCount?.Min,
            MaxPlayers: game.PlayerCount?.Max,
            MinPlayTimeMinutes: game.PlayTime?.MinMinutes,
            MaxPlayTimeMinutes: game.PlayTime?.MaxMinutes,
            BggId: game.BggId,
            BggMetadata: game.BggMetadata,
            CreatedAt: game.CreatedAt,
            SupportsSolo: game.SupportsSolo,
            TotalSessionsPlayed: totalSessions,
            LastPlayedAt: lastPlayed
        );
    }
}
