// OPS-02: User notification metrics
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    #region Notification Metrics

    /// <summary>
    /// Counter for total notifications created.
    /// Issue #2157: Tracks notification creation for operational visibility.
    /// </summary>
    public static readonly Counter<long> NotificationsCreatedTotal = Meter.CreateCounter<long>(
        name: "meepleai.notifications.created.total",
        unit: "notifications",
        description: "Total notifications created");

    /// <summary>
    /// Counter for notifications marked as read (single).
    /// Issue #2157: Tracks mark-as-read operations.
    /// </summary>
    public static readonly Counter<long> NotificationsReadTotal = Meter.CreateCounter<long>(
        name: "meepleai.notifications.read.total",
        unit: "notifications",
        description: "Total notifications marked as read");

    /// <summary>
    /// Counter for bulk mark-all-as-read operations.
    /// Issue #2157: Tracks batch read operations.
    /// </summary>
    public static readonly Counter<long> NotificationsMarkAllReadTotal = Meter.CreateCounter<long>(
        name: "meepleai.notifications.mark_all_read.total",
        unit: "operations",
        description: "Total mark-all-as-read operations");

    /// <summary>
    /// Histogram for mark-as-read operation duration.
    /// Issue #2157: Tracks latency for notification operations.
    /// </summary>
    public static readonly Histogram<double> NotificationMarkReadDuration = Meter.CreateHistogram<double>(
        name: "meepleai.notifications.mark_read.duration",
        unit: "ms",
        description: "Duration of mark-as-read operations");

    /// <summary>
    /// Histogram for mark-all-as-read operation duration.
    /// Issue #2157: Tracks latency for bulk notification operations.
    /// </summary>
    public static readonly Histogram<double> NotificationMarkAllReadDuration = Meter.CreateHistogram<double>(
        name: "meepleai.notifications.mark_all_read.duration",
        unit: "ms",
        description: "Duration of mark-all-as-read operations");

    /// <summary>
    /// Records a notification creation event.
    /// Issue #2157: Helper method for notification metrics.
    /// </summary>
    /// <param name="notificationType">Type of notification (e.g., system, game, user)</param>
    /// <param name="severity">Severity level (e.g., info, warning, error)</param>
    public static void RecordNotificationCreated(string notificationType, string severity)
    {
        var tags = new TagList
        {
            { "type", notificationType.ToLowerInvariant() },
            { "severity", severity.ToLowerInvariant() }
        };

        NotificationsCreatedTotal.Add(1, tags);
    }

    /// <summary>
    /// Records a mark-as-read operation.
    /// Issue #2157: Helper method for notification read metrics.
    /// </summary>
    /// <param name="durationMs">Duration of the operation in milliseconds</param>
    /// <param name="notificationType">Optional type of notification</param>
    public static void RecordNotificationRead(double durationMs, string? notificationType = null)
    {
        var tags = new TagList();

        if (!string.IsNullOrEmpty(notificationType))
        {
            tags.Add("type", notificationType.ToLowerInvariant());
        }

        NotificationsReadTotal.Add(1, tags);
        NotificationMarkReadDuration.Record(durationMs, tags);
    }

    /// <summary>
    /// Records a mark-all-as-read operation.
    /// Issue #2157: Helper method for bulk notification read metrics.
    /// </summary>
    /// <param name="durationMs">Duration of the operation in milliseconds</param>
    /// <param name="notificationCount">Number of notifications marked as read</param>
    public static void RecordNotificationMarkAllRead(double durationMs, int notificationCount)
    {
        var tags = new TagList
        {
            { "count_bucket", GetCountBucket(notificationCount) }
        };

        NotificationsMarkAllReadTotal.Add(1, tags);
        NotificationMarkAllReadDuration.Record(durationMs, tags);
    }

    /// <summary>
    /// Gets a bucket label for notification count ranges.
    /// </summary>
    private static string GetCountBucket(int count)
    {
        return count switch
        {
            0 => "zero",
            <= 5 => "1-5",
            <= 20 => "6-20",
            <= 50 => "21-50",
            _ => "50+"
        };
    }

    #endregion
}
