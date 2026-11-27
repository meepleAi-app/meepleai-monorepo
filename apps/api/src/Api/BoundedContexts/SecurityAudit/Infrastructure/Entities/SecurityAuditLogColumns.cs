namespace Api.BoundedContexts.SecurityAudit.Infrastructure.Entities;

/// <summary>
/// The lengths of the bounded text columns of <c>security_audit_logs</c>, read by its EF configuration (#5156). The
/// User-Agent is the client's header: <c>AuditLogger</c> cuts it to <see cref="UserAgentLength"/>, or Postgres refuses
/// the row and the event — a failed login, for one — leaves no trace.
/// </summary>
internal static class SecurityAuditLogColumns
{
    public const int EventTypeLength = 128;
    public const int IpAddressLength = 64;
    public const int UserAgentLength = 512;
    public const int CorrelationIdLength = 128;
}
