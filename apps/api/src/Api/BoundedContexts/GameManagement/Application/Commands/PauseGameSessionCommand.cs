using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to pause an active game session.
/// </summary>
internal record PauseGameSessionCommand(
    Guid SessionId
) : ICommand<GameSessionDto>;
