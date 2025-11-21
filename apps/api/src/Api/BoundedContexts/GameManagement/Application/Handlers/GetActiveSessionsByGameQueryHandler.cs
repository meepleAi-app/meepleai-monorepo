using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles get active sessions by game query.
/// </summary>
public class GetActiveSessionsByGameQueryHandler : IQueryHandler<GetActiveSessionsByGameQuery, IReadOnlyList<GameSessionDto>>
{
    private readonly IGameSessionRepository _sessionRepository;

    public GetActiveSessionsByGameQueryHandler(IGameSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<IReadOnlyList<GameSessionDto>> Handle(GetActiveSessionsByGameQuery query, CancellationToken cancellationToken)
    {
        var sessions = await _sessionRepository.FindActiveByGameIdAsync(query.GameId, cancellationToken);

        return sessions.Select(MapToDto).ToList();
    }

    private static GameSessionDto MapToDto(GameSession session)
    {
        var playerDtos = session.Players.Select(p => new SessionPlayerDto(
            PlayerName: p.PlayerName,
            PlayerOrder: p.PlayerOrder,
            Color: p.Color
        )).ToList();

        return new GameSessionDto(
            Id: session.Id,
            GameId: session.GameId,
            Status: session.Status.Value,
            StartedAt: session.StartedAt,
            CompletedAt: session.CompletedAt,
            PlayerCount: session.PlayerCount,
            Players: playerDtos,
            WinnerName: session.WinnerName,
            Notes: session.Notes,
            DurationMinutes: (int)session.Duration.TotalMinutes
        );
    }
}
