namespace Api.BoundedContexts.Administration.Domain.Services;

/// <summary>
/// Service for cleaning up orphaned analysis tasks in Redis.
/// ISSUE-2528: Background job for automatic cleanup of stale tasks
/// </summary>
public interface IOrphanedTaskCleanupService
{
    /// <summary>
    /// Scans Redis for orphaned analysis tasks and removes them if they meet cleanup criteria.
    /// </summary>
    /// <param name="retentionPeriod">Maximum age for failed/cancelled tasks before cleanup (default 24 hours)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tasks cleaned up</returns>
    Task<int> CleanupOrphanedTasksAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);
}
