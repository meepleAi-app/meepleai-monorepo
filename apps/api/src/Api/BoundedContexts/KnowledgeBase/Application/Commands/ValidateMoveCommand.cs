using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to validate a player move using Arbitro Agent.
/// Issue #3760: Arbitro Agent Move Validation Logic with Game State Analysis.
/// </summary>
public record ValidateMoveCommand : IRequest<MoveValidationResultDto>
{
    /// <summary>
    /// Game session identifier
    /// </summary>
    public required Guid GameSessionId { get; init; }

    /// <summary>
    /// Player making the move
    /// </summary>
    public required string PlayerName { get; init; }

    /// <summary>
    /// Action being performed (e.g., "roll dice", "move piece", "draw card")
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Optional position or coordinates (e.g., "A5", "3,4")
    /// </summary>
    public string? Position { get; init; }

    /// <summary>
    /// Optional additional context for complex moves
    /// </summary>
    public Dictionary<string, string>? AdditionalContext { get; init; }
}
