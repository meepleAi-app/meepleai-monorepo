using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get aggregated statistics for agent feedback.
/// Used by analytics dashboard to track agent effectiveness and user satisfaction.
/// </summary>
/// <param name="StartDate">Optional start date filter (inclusive).</param>
/// <param name="EndDate">Optional end date filter (inclusive).</param>
/// <param name="Endpoint">Optional endpoint filter (e.g., "/api/v1/chat").</param>
internal record GetFeedbackStatsQuery(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? Endpoint = null
) : IQuery<FeedbackStatsDto>;
