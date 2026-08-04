using Api.BoundedContexts.SecurityAudit.Application.Services;
using Api.BoundedContexts.SecurityAudit.Infrastructure.Entities;
using Api.Infrastructure;

namespace Api.BoundedContexts.SecurityAudit.Infrastructure.Services;

/// <inheritdoc cref="ITransactionalAuditWriter"/>
/// <remarks>
/// Registered SCOPED so it resolves the SAME per-request <see cref="MeepleAiDbContext"/> instance as
/// the caller's <c>IUnitOfWork</c> — the row added here is tracked by that context and committed by
/// the caller's SaveChanges. There is intentionally NO SaveChanges and NO try/catch here: a failure
/// to persist the evidence MUST bubble and roll back the domain write.
/// </remarks>
internal sealed class TransactionalAuditWriter : ITransactionalAuditWriter
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;

    public TransactionalAuditWriter(MeepleAiDbContext db, TimeProvider timeProvider)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public void Append(
        string eventType,
        Guid? actorUserId = null,
        Guid? targetUserId = null,
        string? metadata = null,
        string? correlationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        _db.Set<AuditLogEntity>().Add(new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            ActorUserId = actorUserId,
            TargetUserId = targetUserId,
            Metadata = metadata,
            CorrelationId = correlationId,
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
        });
    }
}
