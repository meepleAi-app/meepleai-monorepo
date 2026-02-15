using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to get paginated email history for a specific user.
/// Issue #4417: Email notification queue.
/// </summary>
internal record GetEmailHistoryQuery(
    Guid UserId,
    int Skip = 0,
    int Take = 20
) : IQuery<EmailHistoryResult>;

/// <summary>
/// Paginated result for email history.
/// </summary>
internal record EmailHistoryResult(
    List<EmailQueueItemDto> Items,
    int TotalCount,
    int Skip,
    int Take
);
