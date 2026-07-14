using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Domain.Repositories;

internal interface IAccessRequestRepository : IRepository<AccessRequest, Guid>
{
    Task<AccessRequest?> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccessRequest>> GetByStatusAsync(
        AccessRequestStatus? status, int page, int pageSize,
        CancellationToken cancellationToken = default);
    Task<int> CountByStatusAsync(AccessRequestStatus status, CancellationToken cancellationToken = default);
    Task<int> CountAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Partial update: persists ONLY <c>InvitationId</c> via a direct SQL UPDATE.
    /// Used by <c>AccessRequestApprovedEventHandler</c> during async outbox dispatch so it does
    /// NOT rewrite the whole aggregate — a full <see cref="IRepository{T,TKey}.UpdateAsync"/> would
    /// clobber a concurrently-committed status change (last-writer-wins, happy-path #B 2026-07-10).
    /// Executes immediately (no <c>SaveChangesAsync</c>), so it never reenters the event dispatcher.
    /// </summary>
    Task SetInvitationIdAsync(Guid id, Guid invitationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Partial update: persists ONLY <c>LastNotifiedEventId</c> (Slack-alert idempotency guard,
    /// #1940) via a direct SQL UPDATE. Same rationale as <see cref="SetInvitationIdAsync"/> — the
    /// Created-event handler must not rewrite the rest of the aggregate (which would revert an
    /// approval that landed between the handler's read and its write).
    /// </summary>
    Task MarkNotifiedAsync(Guid id, Guid eventId, CancellationToken cancellationToken = default);
}
