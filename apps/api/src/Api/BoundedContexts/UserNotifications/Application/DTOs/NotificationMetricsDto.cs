namespace Api.BoundedContexts.UserNotifications.Application.DTOs;

/// <summary>
/// DTO representing notification queue metrics for admin dashboard.
/// </summary>
internal record NotificationMetricsDto(
    int TotalPending,
    int TotalSent,
    int TotalFailed,
    int TotalDeadLetter,
    Dictionary<string, int> PendingByChannel);
