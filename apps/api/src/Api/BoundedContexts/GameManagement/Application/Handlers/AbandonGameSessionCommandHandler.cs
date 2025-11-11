using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles game session abandon command.
/// </summary>
public class AbandonGameSessionCommandHandler : ICommandHandler<AbandonGameSessionCommand, GameSessionDto>
{
    private readonly IGameSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AbandonGameSessionCommandHandler(
        IGameSessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<GameSessionDto> Handle(AbandonGameSessionCommand command, CancellationToken cancellationToken)
    {
        // Load session
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session with ID {command.SessionId} not found");

        // Abandon via domain method
        session.Abandon(command.Reason);

        // Persist
        await _sessionRepository.UpdateAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Map to DTO
        return MapToDto(session);
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
