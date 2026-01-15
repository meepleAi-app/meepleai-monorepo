using Api.Models;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to suggest player move using AI analysis.
/// Issue #2421: Player Mode UI Controls - Backend endpoint for AI move suggestions.
/// </summary>
internal sealed record SuggestPlayerMoveCommand : IRequest<PlayerModeSuggestionResponse>
{
    /// <summary>
    /// Game ID for context and knowledge base lookup.
    /// </summary>
    public required string GameId { get; init; }

    /// <summary>
    /// Current game state as flexible dictionary (players, resources, board, etc.).
    /// </summary>
    public required IReadOnlyDictionary<string, object> GameState { get; init; }

    /// <summary>
    /// Optional player query/question for context ("Should I focus on wood?").
    /// </summary>
    public string? Query { get; init; }

    /// <summary>
    /// Optional chat thread ID for conversation context.
    /// </summary>
    public Guid? ChatThreadId { get; init; }
}
