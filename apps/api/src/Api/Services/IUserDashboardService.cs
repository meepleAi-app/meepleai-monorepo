using Api.BoundedContexts.Administration.Application.DTOs;

namespace Api.Services;

/// <summary>
/// Service for retrieving user dashboard data (Issue #2854).
/// Aggregates data from multiple bounded contexts with HybridCache optimization.
/// </summary>
internal interface IUserDashboardService
{
    /// <summary>
    /// Get complete user dashboard data with caching.
    /// </summary>
    Task<UserDashboardDto> GetUserDashboardAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
