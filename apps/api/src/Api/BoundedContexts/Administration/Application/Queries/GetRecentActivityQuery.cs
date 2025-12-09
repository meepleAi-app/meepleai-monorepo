using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Issue #874: Query to get recent activity feed for admin dashboard.
/// Returns last N system events (user registrations, uploads, errors, alerts, etc.)
/// </summary>
public record GetRecentActivityQuery(
    int Limit = 10,
    DateTime? Since = null
) : IQuery<RecentActivityDto>;
