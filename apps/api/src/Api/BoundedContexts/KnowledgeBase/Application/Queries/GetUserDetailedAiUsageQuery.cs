using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get detailed AI usage for a specific user.
/// Issue #3338: AI Token Usage Tracking per User
/// </summary>
/// <param name="UserId">Target user ID</param>
/// <param name="StartDate">Start date (inclusive)</param>
/// <param name="EndDate">End date (inclusive)</param>
internal record GetUserDetailedAiUsageQuery(
    Guid UserId,
    DateOnly StartDate,
    DateOnly EndDate
) : IQuery<UserAiUsageDto>;
