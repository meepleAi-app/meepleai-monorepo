namespace Api.Infrastructure.Persistence;

/// <summary>
/// Marks an entity class for before/after snapshot capture by
/// <see cref="AuditingSaveChangesInterceptor"/>. Snapshots are forwarded to
/// <see cref="IAuditSnapshotSink"/> and consumed by the AuditLoggingBehavior to
/// write rows to the audit_outbox table.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class AuditableAttribute : Attribute { }
