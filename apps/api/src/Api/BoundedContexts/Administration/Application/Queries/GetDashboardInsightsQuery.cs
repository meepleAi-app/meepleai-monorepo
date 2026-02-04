using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to fetch AI-powered dashboard insights (Issue #3319).
/// Returns personalized insights based on user's library, play history, and RAG recommendations.
/// </summary>
public record GetDashboardInsightsQuery(
    Guid UserId
) : IQuery<DashboardInsightsResponseDto>;
