using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to get paginated dead-lettered emails for admin review.
/// Issue #4430: Email queue dashboard monitoring.
/// </summary>
internal record GetDeadLetterEmailsQuery(
    int Skip = 0,
    int Take = 20
) : IQuery<DeadLetterEmailsResult>;

/// <summary>
/// Paginated result for dead letter emails.
/// </summary>
internal record DeadLetterEmailsResult(
    List<EmailQueueItemDto> Items,
    int TotalCount,
    int Skip,
    int Take
);
