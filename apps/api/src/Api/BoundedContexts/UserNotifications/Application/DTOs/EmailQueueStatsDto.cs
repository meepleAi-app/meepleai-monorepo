namespace Api.BoundedContexts.UserNotifications.Application.DTOs;

/// <summary>
/// Admin dashboard statistics for email queue health.
/// Issue #4430: Email queue dashboard monitoring.
/// </summary>
internal record EmailQueueStatsDto(
    int PendingCount,
    int ProcessingCount,
    int SentCount,
    int FailedCount,
    int DeadLetterCount,
    int SentLastHour,
    int SentLast24Hours
);
