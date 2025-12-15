using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to end (complete) a game session with optional winner.
/// Business alias for CompleteGameSessionCommand.
/// </summary>
internal record EndGameSessionCommand(
    Guid SessionId,
    string? WinnerName = null
) : ICommand<GameSessionDto>;
