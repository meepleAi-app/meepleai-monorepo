using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Analytics;

/// <summary>
/// Query to retrieve high-level overview analytics for the admin dashboard.
/// Returns total counts for users, games, documents, and chats plus today's new counts.
/// </summary>
internal record GetOverviewAnalyticsQuery : IQuery<OverviewAnalyticsDto>;
