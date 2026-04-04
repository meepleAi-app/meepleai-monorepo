using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to get paginated email history for admin dashboard with search.
/// Issue #39: Admin email management real API.
/// </summary>
internal record GetAdminEmailHistoryQuery(
    int Skip = 0,
    int Take = 20,
    string? Search = null
) : IQuery<AdminEmailHistoryResult>;

internal record AdminEmailHistoryResult(
    List<EmailQueueItemDto> Items,
    int TotalCount,
    int Skip,
    int Take
);
