using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to get chat analytics for admin dashboard.
/// Issue #3714: System-wide chat analytics with time range filtering.
/// </summary>
/// <param name="TimeRangeDays">Number of days to analyze (7, 30, 90, 365)</param>
public record GetChatAnalyticsQuery(
    int TimeRangeDays
) : IRequest<ChatAnalyticsDto>;
