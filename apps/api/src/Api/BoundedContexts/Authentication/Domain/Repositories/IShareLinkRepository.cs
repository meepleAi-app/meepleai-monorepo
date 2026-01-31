using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Domain.Repositories;

/// <summary>
/// Repository interface for ShareLink aggregate.
/// </summary>
internal interface IShareLinkRepository : IRepository<ShareLink, Guid>
{
    /// <summary>
    /// Finds all share links for a specific thread.
    /// </summary>
    Task<IReadOnlyList<ShareLink>> FindByThreadIdAsync(Guid threadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all share links created by a specific user.
    /// </summary>
    Task<IReadOnlyList<ShareLink>> FindByCreatorIdAsync(Guid creatorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a share link by thread ID and creator ID (for ownership verification).
    /// </summary>
    Task<ShareLink?> FindByThreadIdAndCreatorIdAsync(Guid threadId, Guid creatorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all active (not revoked, not expired) share links for a thread.
    /// </summary>
    Task<IReadOnlyList<ShareLink>> FindActiveByThreadIdAsync(Guid threadId, CancellationToken cancellationToken = default);
}
