using Api.Models;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to invoke the chess agent for question answering.
/// CHESS-04: Specialized chess conversational agent.
/// </summary>
internal sealed record InvokeChessAgentCommand : IRequest<ChessAgentResponse>
{
    /// <summary>
    /// Chess-related question from the user.
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// Optional FEN position for position-specific analysis.
    /// </summary>
    public string? FenPosition { get; init; }

    /// <summary>
    /// Optional chat ID for conversation context.
    /// </summary>
    public Guid? ChatId { get; init; }
}
