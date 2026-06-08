using Api.BoundedContexts.UserLibrary.Domain.Entities;

namespace Api.BoundedContexts.UserLibrary.Domain.Repositories;

/// <summary>
/// Repository interface for ProposalMigration aggregate.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
public interface IProposalMigrationRepository
{
    /// <summary>
    /// Adds a new ProposalMigration to the repository.
    /// </summary>
    Task AddAsync(ProposalMigration migration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts the ProposalMigration and commits the change immediately. Returns
    /// <c>false</c> when the insert violates the <c>(source_event_id)</c> UNIQUE
    /// partial index — i.e. an outbox replay or a concurrent pod already wrote
    /// the row for this <c>IDomainEvent.EventId</c> (CF-2 / #1938). On <c>false</c>
    /// the tracked entity is detached so subsequent <c>SaveChanges</c> on the same
    /// scoped context don't retry it.
    /// </summary>
    /// <remarks>
    /// Inline-commit pattern mirroring <c>NotificationRepository.AddAndCommitAsync</c>
    /// (CF-1) and <c>ProcessingMetricsService.RecordStepDurationAsync</c> (CF-2 partial).
    /// Required for the proposal-migration handler because the rest of the BC saves
    /// downstream via the UoW pipeline — that pipeline turns a 23505 into an
    /// unhandled exception, so the dedup needs to land here before the row escapes.
    /// </remarks>
    Task<bool> AddAndCommitAsync(ProposalMigration migration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a ProposalMigration by its ID.
    /// </summary>
    Task<ProposalMigration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pending migrations for a specific user.
    /// </summary>
    Task<List<ProposalMigration>> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a migration by ShareRequest ID.
    /// </summary>
    Task<ProposalMigration?> GetByShareRequestIdAsync(Guid shareRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing ProposalMigration.
    /// </summary>
    Task UpdateAsync(ProposalMigration migration, CancellationToken cancellationToken = default);
}
