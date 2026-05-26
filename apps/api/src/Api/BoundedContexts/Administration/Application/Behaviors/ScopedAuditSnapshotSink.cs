using Api.Infrastructure.Persistence;

namespace Api.BoundedContexts.Administration.Application.Behaviors;

/// <summary>
/// Per-request (scoped) snapshot sink. The <see cref="AuditingSaveChangesInterceptor"/> records
/// snapshots here during SaveChanges; <see cref="AuditLoggingBehavior{TRequest,TResponse}"/> drains
/// them after the handler completes and folds them into the audit_outbox payload.
///
/// Design note (T2 review, Option A): <see cref="IAuditSnapshotSink"/> remains write-only;
/// the behavior depends on the concrete type to access the read side (<see cref="Snapshots"/>),
/// keeping the interceptor decoupled from the payload shape.
/// </summary>
public sealed class ScopedAuditSnapshotSink : IAuditSnapshotSink
{
    private readonly List<AuditSnapshot> _snapshots = [];

    /// <summary>Snapshots captured during the current request's SaveChanges call(s).</summary>
    public IReadOnlyList<AuditSnapshot> Snapshots => _snapshots;

    /// <inheritdoc/>
    public void Record(AuditSnapshot snapshot) => _snapshots.Add(snapshot);

    /// <summary>Clears all snapshots. Called by the behavior after draining to avoid cross-command bleed.</summary>
    public void Clear() => _snapshots.Clear();
}
