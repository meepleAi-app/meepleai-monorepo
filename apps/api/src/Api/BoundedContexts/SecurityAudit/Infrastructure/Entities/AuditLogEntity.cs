namespace Api.BoundedContexts.SecurityAudit.Infrastructure.Entities;

/// <summary>
/// Persistence model for the SecurityAudit bounded context (I10 prep).
/// Stores immutable security-relevant events with actor/target attribution and correlation tracking.
/// Distinct from the legacy <c>Api.Infrastructure.Entities.AuditLogEntity</c> (table <c>audit_logs</c>),
/// which records administrative actions with action/resource/result semantics.
/// </summary>
public class AuditLogEntity
{
    public Guid Id { get; init; }

    /// <summary>
    /// User who initiated the action (null for system-driven events such as scheduled jobs).
    /// </summary>
    public Guid? ActorUserId { get; init; }

    /// <summary>
    /// User affected by the action (null when the event is not user-scoped).
    /// </summary>
    public Guid? TargetUserId { get; init; }

    /// <summary>
    /// Event type identifier (e.g., "auth.login.success", "auth.bootstrap.admin").
    /// Free-form string to allow extension by new auth flows without schema changes.
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }

    /// <summary>
    /// Event timestamp (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Free-form metadata (typically JSON) for event-specific context.
    /// Must not contain secrets, raw passwords, or session tokens.
    /// </summary>
    public string? Metadata { get; init; }

    /// <summary>
    /// Correlation identifier propagated across the request pipeline for distributed tracing.
    /// </summary>
    public string? CorrelationId { get; init; }
}
