using Api.BoundedContexts.KnowledgeBase.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository for <see cref="KbReindexJob"/> aggregates. Issue #941 / ADR-057.
/// Follows the UoW pattern per ADR-056: mutating methods stage changes; callers
/// invoke <see cref="Api.SharedKernel.Infrastructure.Persistence.IUnitOfWork.SaveChangesAsync"/>.
/// </summary>
public interface IKbReindexJobRepository
{
    /// <summary>Returns the job by ID, or null if not found.</summary>
    Task<KbReindexJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the existing active job (status = queued|running) for the given (GameId, UserId),
    /// or null. Used to enforce the concurrency invariant in AC-6 (at most one active per pair).
    /// </summary>
    Task<KbReindexJob?> GetActiveByGameAndUserAsync(Guid gameId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Stages an Add operation on the change-tracker.</summary>
    Task AddAsync(KbReindexJob job, CancellationToken cancellationToken = default);

    /// <summary>Stages an Update on the change-tracker (state machine transition already applied to the aggregate).</summary>
    Task UpdateAsync(KbReindexJob job, CancellationToken cancellationToken = default);
}
