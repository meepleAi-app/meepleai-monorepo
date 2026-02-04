using Api.Infrastructure.Entities;

namespace Api.Infrastructure.Services;

/// <summary>
/// Service for managing the BGG import queue.
/// Issue #3541: BGG Import Queue Service
/// </summary>
public interface IBggImportQueueService
{
    /// <summary>
    /// Enqueue a single BGG game ID for import.
    /// </summary>
    /// <param name="bggId">BGG game ID to import</param>
    /// <param name="gameName">Optional game name for UI display</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created queue entity</returns>
    /// <exception cref="InvalidOperationException">If BGG ID is already queued or imported</exception>
    Task<BggImportQueueEntity> EnqueueAsync(
        int bggId,
        string? gameName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueue multiple BGG game IDs in batch.
    /// Duplicate detection ensures only new BGG IDs are enqueued.
    /// </summary>
    /// <param name="bggIds">Collection of BGG IDs to import</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of successfully enqueued entities</returns>
    Task<List<BggImportQueueEntity>> EnqueueBatchAsync(
        IEnumerable<int> bggIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current queue status with position information.
    /// Returns only Queued and Processing items.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of queued and processing items ordered by position</returns>
    Task<List<BggImportQueueEntity>> GetQueueStatusAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all queue items regardless of status (for SSE streaming).
    /// Issue #3543 - Fix #3: Support completed/failed counts in SSE
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all queue items</returns>
    Task<List<BggImportQueueEntity>> GetAllQueueItemsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get queue entry by BGG ID.
    /// </summary>
    /// <param name="bggId">BGG game ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Queue entity or null if not found</returns>
    Task<BggImportQueueEntity?> GetByBggIdAsync(
        int bggId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel a queued import job (Queued status only).
    /// </summary>
    /// <param name="id">Queue entry ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cancelled, false if not found or not in Queued status</returns>
    Task<bool> CancelAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retry a failed import job.
    /// Resets retry count and changes status back to Queued.
    /// </summary>
    /// <param name="id">Queue entry ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if retried, false if not found or not in Failed status</returns>
    Task<bool> RetryFailedAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clean up old completed/failed jobs based on retention policy.
    /// </summary>
    /// <param name="retentionDays">Jobs older than this will be deleted</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of jobs deleted</returns>
    Task<int> CleanupOldJobsAsync(
        int retentionDays,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get next queued item to process (for background worker).
    /// Returns lowest position item with Status=Queued.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Next queue entity to process or null if queue is empty</returns>
    Task<BggImportQueueEntity?> GetNextQueuedItemAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a queue item as processing.
    /// </summary>
    /// <param name="id">Queue entry ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkAsProcessingAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a queue item as completed successfully.
    /// </summary>
    /// <param name="id">Queue entry ID</param>
    /// <param name="createdGameId">ID of created SharedGame</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkAsCompletedAsync(
        Guid id,
        Guid createdGameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a queue item as failed.
    /// Increments retry count. If max retries reached, sets status to Failed permanently.
    /// If retries remain, sets back to Queued with exponential backoff.
    /// </summary>
    /// <param name="id">Queue entry ID</param>
    /// <param name="errorMessage">Error description</param>
    /// <param name="maxRetries">Maximum retry attempts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkAsFailedAsync(
        Guid id,
        string errorMessage,
        int maxRetries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recalculate queue positions after processing an item.
    /// Ensures position sequence is continuous (1, 2, 3, ...).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RecalculatePositionsAsync(
        CancellationToken cancellationToken = default);
}
