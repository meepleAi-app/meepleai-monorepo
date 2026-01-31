using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to abandon an active game session.
/// </summary>
internal record AbandonGameSessionCommand(
    Guid SessionId,
    string? Reason = null
) : ICommand<GameSessionDto>;
