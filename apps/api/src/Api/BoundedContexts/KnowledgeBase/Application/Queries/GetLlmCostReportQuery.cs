using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get LLM cost report for a date range
/// ISSUE-960: BGAI-018 - Cost analytics and reporting
/// </summary>
public record GetLlmCostReportQuery : IRequest<LlmCostReportDto>
{
    /// <summary>
    /// Start date for report (inclusive)
    /// </summary>
    public required DateOnly StartDate { get; init; }

    /// <summary>
    /// End date for report (inclusive)
    /// </summary>
    public required DateOnly EndDate { get; init; }

    /// <summary>
    /// Optional user ID for user-specific report
    /// </summary>
    public Guid? UserId { get; init; }
}

/// <summary>
/// LLM cost report DTO
/// </summary>
public record LlmCostReportDto
{
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required decimal TotalCost { get; init; }
    public required Dictionary<string, decimal> CostsByProvider { get; init; }
    public required Dictionary<string, decimal> CostsByRole { get; init; }
    public required decimal DailyCost { get; init; }
    public required bool ExceedsThreshold { get; init; }
    public required decimal ThresholdAmount { get; init; }
}
