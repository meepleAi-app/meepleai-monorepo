using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to submit user feedback on a dashboard AI insight.
/// Issue #4124: AI Insights Runtime Validation (Performance + Accuracy).
/// </summary>
public record SubmitInsightFeedbackCommand : IRequest<Guid>
{
    public required Guid UserId { get; init; }
    public required string InsightId { get; init; }
    public required string InsightType { get; init; }
    public required bool IsRelevant { get; init; }
    public string? Comment { get; init; }
}
