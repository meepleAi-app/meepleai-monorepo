namespace Api.BoundedContexts.SecurityAudit.Application.Services;

/// <summary>
/// I10 (auth security fixes): logger for security-relevant events.
///
/// The interface intentionally accepts already-resolved primitives rather
/// than a domain object so it can be injected into both Authentication BC
/// handlers (which build the event from primitives) and infrastructure
/// middleware (which observes raw HTTP context). The default implementation
/// persists the row through the <c>security_audit_logs</c> table.
///
/// <para>
/// Failure handling: implementations MUST never throw out of
/// <see cref="LogAsync"/> — a failed audit write must not roll back the
/// caller's domain transaction or surface as a user-visible error. They
/// should log the underlying exception at <c>Error</c> severity instead.
/// </para>
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Persists an audit-log entry. Returns once the write has been
    /// acknowledged by the storage layer; failures are swallowed (see
    /// type-level remarks).
    /// </summary>
    /// <param name="eventType">
    /// Hierarchical event identifier (e.g. <c>auth.login.success</c>,
    /// <c>auth.bootstrap.admin</c>). See
    /// <see cref="AuditEventType"/> for the canonical names.
    /// </param>
    /// <param name="actorUserId">
    /// User who initiated the action. Null for system-driven events
    /// (background services, anonymous flows).
    /// </param>
    /// <param name="targetUserId">
    /// User affected by the action. Often equal to <paramref name="actorUserId"/>;
    /// null when the event is not user-scoped.
    /// </param>
    /// <param name="ipAddress">Client IP from the request, if available.</param>
    /// <param name="userAgent">Client User-Agent, if available.</param>
    /// <param name="metadata">
    /// Free-form metadata payload. Use a JSON string for structured data.
    /// MUST NOT contain secrets, raw passwords, or session tokens.
    /// </param>
    /// <param name="correlationId">Distributed-tracing correlation id.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    Task LogAsync(
        string eventType,
        Guid? actorUserId = null,
        Guid? targetUserId = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? metadata = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
}
