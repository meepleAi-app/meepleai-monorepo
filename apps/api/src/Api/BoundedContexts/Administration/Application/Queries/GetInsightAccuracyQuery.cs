using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get insight accuracy metrics.
/// Issue #4124: AI Insights Runtime Validation (Performance + Accuracy).
/// </summary>
public record GetInsightAccuracyQuery : IRequest<InsightAccuracyResponseDto>;

/// <summary>
/// Response DTO for insight accuracy metrics.
/// </summary>
public record InsightAccuracyResponseDto(
    int TotalFeedback,
    int RelevantCount,
    double AccuracyPercentage,
    IReadOnlyList<InsightTypeAccuracyDto> ByType,
    DateTime CalculatedAt
);

/// <summary>
/// Accuracy breakdown per insight type.
/// </summary>
public record InsightTypeAccuracyDto(
    string Type,
    int Total,
    int Relevant,
    double AccuracyPercentage
);
