using Api.BoundedContexts.GameManagement.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to apply a move suggestion to the game state.
/// Updates GameSessionState with the suggested move's state changes.
/// Issue #2404 - Player Mode apply suggestion
/// </summary>
/// <param name="SessionId">Game session ID to apply suggestion to</param>
/// <param name="SuggestionId">ID of the suggestion to apply</param>
/// <param name="StateChanges">State changes from the suggestion</param>
/// <param name="UserId">User applying the suggestion</param>
internal sealed record ApplySuggestionCommand(
    Guid SessionId,
    Guid SuggestionId,
    IReadOnlyDictionary<string, object> StateChanges,
    Guid UserId
) : IRequest<GameSessionStateDto>;
