using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to retrieve user activity audit logs with optional filtering.
/// Issue #911 - UserActivityTimeline component backend support.
/// </summary>
/// <param name="UserId">User ID to fetch activity for</param>
/// <param name="ActionFilter">Optional: Filter by specific action types (comma-separated)</param>
/// <param name="ResourceFilter">Optional: Filter by resource type</param>
/// <param name="StartDate">Optional: Filter logs from this date</param>
/// <param name="EndDate">Optional: Filter logs until this date</param>
/// <param name="Limit">Maximum number of logs to return (default: 100, max: 500)</param>
internal sealed record GetUserActivityQuery(
    Guid UserId,
    string? ActionFilter = null,
    string? ResourceFilter = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int Limit = 100
) : IRequest<GetUserActivityResult>;

internal sealed record UserActivityDto(
    Guid Id,
    string Action,
    string Resource,
    string? ResourceId,
    string Result,
    string? Details,
    DateTime CreatedAt,
    string? IpAddress
);

internal sealed record GetUserActivityResult(
    IReadOnlyList<UserActivityDto> Activities,
    int TotalCount
);
