using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to submit user feedback on Decisore Agent move suggestion.
/// Issue #4335: Decisore Agent Beta Testing and User Feedback Iteration.
/// </summary>
public record SubmitDecisoreMoveFeedbackCommand : IRequest<Guid>
{
    public required Guid SuggestionId { get; init; }
    public required Guid GameSessionId { get; init; }
    public required Guid UserId { get; init; }
    public required int Rating { get; init; }
    public required string Quality { get; init; }  // Helpful/Neutral/Harmful
    public string? Comment { get; init; }
    public required string Outcome { get; init; }  // Win/Loss/Draw/InProgress
    public required bool SuggestionFollowed { get; init; }
    public required string TopSuggestedMove { get; init; }
    public required double PositionStrength { get; init; }
    public required string AnalysisDepth { get; init; }
}
