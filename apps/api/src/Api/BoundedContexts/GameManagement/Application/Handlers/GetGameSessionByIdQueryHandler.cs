using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles get game session by ID query.
/// </summary>
internal class GetGameSessionByIdQueryHandler : IQueryHandler<GetGameSessionByIdQuery, GameSessionDto?>
{
    private readonly IGameSessionRepository _sessionRepository;

    public GetGameSessionByIdQueryHandler(IGameSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<GameSessionDto?> Handle(GetGameSessionByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var session = await _sessionRepository.GetByIdAsync(query.SessionId, cancellationToken).ConfigureAwait(false);

        return session != null ? MapToDto(session) : null;
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
