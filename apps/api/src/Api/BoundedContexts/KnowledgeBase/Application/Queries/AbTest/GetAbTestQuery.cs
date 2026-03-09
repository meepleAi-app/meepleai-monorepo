using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.AbTest;

/// <summary>
/// Query to get an A/B test session in blind mode (no model info).
/// Issue #5494: A/B Test CQRS commands and queries.
/// </summary>
internal sealed record GetAbTestQuery(Guid SessionId) : IQuery<AbTestSessionDto?>;

/// <summary>
/// Query to get an A/B test session in revealed mode (with model info, only after evaluation).
/// </summary>
internal sealed record RevealAbTestQuery(Guid SessionId) : IQuery<AbTestSessionRevealedDto?>;

/// <summary>
/// Query to get paginated list of A/B test sessions for a user.
/// </summary>
internal sealed record GetAbTestsQuery(
    Guid UserId,
    int Page = 1,
    int PageSize = 20,
    string? Status = null
) : IQuery<AbTestSessionListDto>;

/// <summary>
/// Query to get aggregated A/B test analytics.
/// </summary>
internal sealed record GetAbTestAnalyticsQuery(
    DateTime? DateFrom = null,
    DateTime? DateTo = null
) : IQuery<AbTestAnalyticsDto>;
