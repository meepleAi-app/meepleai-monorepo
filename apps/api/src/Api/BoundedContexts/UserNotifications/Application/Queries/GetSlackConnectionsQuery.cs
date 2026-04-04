using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to retrieve paginated Slack connections for admin management.
/// </summary>
internal record GetSlackConnectionsQuery(int Page, int PageSize) : IQuery<PaginatedSlackConnectionsResult>;

internal record PaginatedSlackConnectionsResult(
    IReadOnlyList<SlackConnectionDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
