namespace Api.BoundedContexts.SecurityAudit.Application.Services;

/// <summary>
/// #3495 H6 — appends an IMMUTABLE audit row to the AMBIENT scoped <c>MeepleAiDbContext</c> WITHOUT
/// calling SaveChanges, so the caller's <c>IUnitOfWork</c> commits the row ATOMICALLY with the domain
/// write (evidence-or-nothing).
/// <para>
/// This is the deliberate OPPOSITE of <see cref="IAuditLogger"/>, which resolves a fresh DbContext
/// from a CHILD scope and runs its own SaveChanges precisely so audit rows survive a caller rollback.
/// That is the wrong guarantee for copyright evidence, which must never be orphaned from — nor leave
/// orphaned — the row it attests. Use <see cref="IAuditLogger"/> for "audit on every attempt"; use
/// this for "the evidence and the write commit together or not at all".
/// </para>
/// </summary>
public interface ITransactionalAuditWriter
{
    /// <summary>
    /// Adds an audit row to the ambient scoped DbContext. Does NOT persist — the caller's
    /// <c>IUnitOfWork.SaveChangesAsync</c> is what commits it, in the same transaction as the write.
    /// </summary>
    void Append(
        string eventType,
        Guid? actorUserId = null,
        Guid? targetUserId = null,
        string? metadata = null,
        string? correlationId = null);
}
