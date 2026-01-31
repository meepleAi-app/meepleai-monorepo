using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to add a player to an existing game session.
/// </summary>
internal record AddPlayerToSessionCommand(
    Guid SessionId,
    string PlayerName,
    int PlayerOrder,
    string? Color = null
) : ICommand<GameSessionDto>;
