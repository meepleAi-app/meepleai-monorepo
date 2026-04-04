using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Analytics;

/// <summary>
/// Query to retrieve chat analytics: thread/message totals,
/// average messages per thread, and a 7-day message histogram.
/// </summary>
internal record GetChatAnalyticsQuery : IQuery<ChatAnalyticsDto>;
