using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to submit user feedback on Arbitro Agent move validation.
/// Issue #4328: Arbitro Agent Beta Testing and User Feedback Iteration.
/// </summary>
public record SubmitValidationFeedbackCommand : IRequest<Guid>
{
    /// <summary>
    /// Correlation ID of the validation that was performed.
    /// </summary>
    public required Guid ValidationId { get; init; }

    /// <summary>
    /// Game session where the validation occurred.
    /// </summary>
    public required Guid GameSessionId { get; init; }

    /// <summary>
    /// User submitting the feedback (from session context).
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// User rating (1-5 stars).
    /// </summary>
    public required int Rating { get; init; }

    /// <summary>
    /// User's accuracy assessment: "Correct", "Incorrect", or "Uncertain".
    /// </summary>
    public required string Accuracy { get; init; }

    /// <summary>
    /// Optional text comment.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// AI's original decision for correlation (captured from validation result).
    /// </summary>
    public required string AiDecision { get; init; }

    /// <summary>
    /// AI's original confidence score (0.0-1.0).
    /// </summary>
    public required double AiConfidence { get; init; }

    /// <summary>
    /// Whether conflicts were detected during validation.
    /// </summary>
    public required bool HadConflicts { get; init; }
}
