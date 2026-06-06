namespace Api.Infrastructure.Entities.Administration;

/// <summary>
/// Per-service dedup record for HealthStatusChangedEvent alert dispatch
/// (issue #1941 / iso-2 Fix 2). The handler must short-circuit when the
/// incoming event id matches <see cref="LastEventId"/>, avoiding duplicate
/// Slack alerts on a rolled-back / retried health-status transition.
///
/// <para>Service name is the primary key — at most one row per service.
/// On each successful alert send, the handler upserts this row with the
/// latest event id and timestamp. Re-dispatch sees the match and skips.</para>
/// </summary>
public class HealthStatusAlertSentEntity
{
    /// <summary>Service name (e.g., "postgres", "ollama", "oauth"). Primary key.</summary>
    public required string ServiceName { get; set; }

    /// <summary>The <c>HealthStatusChangedEvent.EventId</c> of the last successfully dispatched alert.</summary>
    public Guid LastEventId { get; set; }

    /// <summary>When the last alert was successfully sent.</summary>
    public DateTime LastSentAt { get; set; }
}
