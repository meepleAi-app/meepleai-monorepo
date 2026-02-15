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

    /// <summary>
    /// Gets count of emails enqueued for a user since a given time.
    /// Used for per-user rate limiting.
    /// Issue #4429: Email throttling.
    /// </summary>
    Task<int> GetRecentCountByUserIdAsync(Guid userId, DateTime since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a similar email was recently enqueued (same user, fileName in subject, template name in subject).
    /// Used for per-PDF deduplication throttling.
    /// Issue #4429: Email throttling.
    /// </summary>
    Task<bool> ExistsSimilarRecentAsync(Guid userId, string subject, DateTime since, CancellationToken cancellationToken = default);
}
