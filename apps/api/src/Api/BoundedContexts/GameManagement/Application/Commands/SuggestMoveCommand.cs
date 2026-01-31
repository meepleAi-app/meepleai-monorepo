using Api.BoundedContexts.GameManagement.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to request move suggestions for a game session.
/// Uses Player Mode agent to analyze game state and suggest optimal moves.
/// Issue #2404 - Player Mode move suggestions
/// </summary>
/// <param name="SessionId">Game session ID to get suggestions for</param>
/// <param name="AgentId">Agent ID configured in Player Mode</param>
/// <param name="Query">Optional specific question about the move (e.g., "Cosa dovrei fare?")</param>
/// <param name="UserId">User making the request</param>
internal sealed record SuggestMoveCommand(
    Guid SessionId,
    Guid AgentId,
    string? Query,
    Guid UserId
) : IRequest<MoveSuggestionsDto>;
