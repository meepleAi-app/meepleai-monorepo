using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to start a new game session.
/// </summary>
public record StartGameSessionCommand(
    Guid GameId,
    List<SessionPlayerRequest> Players
) : ICommand<GameSessionDto>;
