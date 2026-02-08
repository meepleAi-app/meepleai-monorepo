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
