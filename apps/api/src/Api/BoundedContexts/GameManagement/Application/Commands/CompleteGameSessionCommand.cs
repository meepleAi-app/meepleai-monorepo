using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to complete an active game session.
/// </summary>
/// <param name="SessionId">Session to complete.</param>
/// <param name="RequesterId">Authenticated caller — must match the session creator (#2655 IDOR guard).</param>
/// <param name="WinnerName">Optional winner name.</param>
internal record CompleteGameSessionCommand(
    Guid SessionId,
    Guid RequesterId,
    string? WinnerName = null
) : ICommand<GameSessionDto>;
