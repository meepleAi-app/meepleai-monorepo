using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to record user feedback for an agent response.
/// Used to track agent effectiveness and improve AI interactions.
/// </summary>
internal sealed record ProvideAgentFeedbackCommand : IRequest
{
    /// <summary>
    /// ID of the message being rated.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Endpoint that generated the response.
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// ID of the user providing feedback.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Feedback outcome (e.g., "helpful", "not-helpful", "incorrect").
    /// Null to remove existing feedback.
    /// </summary>
    public string? Outcome { get; init; }

    /// <summary>
    /// Optional game ID context.
    /// </summary>
    public string? GameId { get; init; }
}
