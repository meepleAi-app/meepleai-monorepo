using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get model performance analytics for admin dashboard.
/// Issue #3716: Model comparison dashboard with time range filtering.
/// </summary>
/// <param name="TimeRangeDays">Number of days to analyze (7, 30, 90, 365)</param>
public record GetModelPerformanceQuery(
    int TimeRangeDays
) : IRequest<ModelPerformanceDto>;
