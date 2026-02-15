using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserNotifications.Domain.Repositories;

/// <summary>
/// Repository interface for EmailQueueItem aggregate.
/// Issue #4417: Email notification queue with retry policy.
/// </summary>
internal interface IEmailQueueRepository : IRepository<EmailQueueItem, Guid>
{
    /// <summary>
    /// Gets pending emails ready for processing.
    /// Returns items with status Pending or Failed where NextRetryAt has passed.
    /// </summary>
    Task<IReadOnlyList<EmailQueueItem>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets email history for a specific user with pagination.
    /// </summary>
    Task<IReadOnlyList<EmailQueueItem>> GetByUserIdAsync(Guid userId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets total email count for a specific user.
    /// </summary>
    Task<int> GetCountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
