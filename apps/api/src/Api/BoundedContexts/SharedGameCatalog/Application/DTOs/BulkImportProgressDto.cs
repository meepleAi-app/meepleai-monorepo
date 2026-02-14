namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Progress data for bulk import SSE streaming.
/// Issue #4353: Backend - Bulk Import SSE Progress Endpoint
/// </summary>
public sealed record BulkImportProgressDto
{
    /// <summary>
    /// Total items in the queue (all statuses)
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Items waiting in queue
    /// </summary>
    public int Queued { get; init; }

    /// <summary>
    /// Items currently being processed
    /// </summary>
    public int Processing { get; init; }

    /// <summary>
    /// Successfully completed items
    /// </summary>
    public int Completed { get; init; }

    /// <summary>
    /// Failed items (after max retries)
    /// </summary>
    public int Failed { get; init; }

    /// <summary>
    /// Whether there are active (queued or processing) items
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Estimated seconds remaining (1 req/sec rate)
    /// </summary>
    public int EstimatedSecondsRemaining { get; init; }

    /// <summary>
    /// Currently processing item info (null if nothing processing)
    /// </summary>
    public BulkImportCurrentItemDto? CurrentItem { get; init; }

    /// <summary>
    /// UTC timestamp of this progress snapshot
    /// </summary>
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Info about the item currently being processed
/// </summary>
public sealed record BulkImportCurrentItemDto
{
    /// <summary>
    /// BGG ID being processed
    /// </summary>
    public int BggId { get; init; }

    /// <summary>
    /// Game name (if available)
    /// </summary>
    public string? GameName { get; init; }

    /// <summary>
    /// Current retry attempt
    /// </summary>
    public int RetryCount { get; init; }
}
