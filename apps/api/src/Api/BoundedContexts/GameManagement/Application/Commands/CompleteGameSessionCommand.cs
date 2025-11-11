using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to complete an active game session.
/// </summary>
public record CompleteGameSessionCommand(
    Guid SessionId,
    string? WinnerName = null
) : ICommand<GameSessionDto>;
