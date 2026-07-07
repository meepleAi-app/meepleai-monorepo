using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to abandon an active game session.
/// </summary>
internal record AbandonGameSessionCommand(
    Guid SessionId,
    Guid RequesterId,
    string? Reason = null
) : ICommand<GameSessionDto>;
