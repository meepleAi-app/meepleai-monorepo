namespace Api.Infrastructure.Entities;

/// <summary>
/// Entity representing a BGG import job in the global rate-limited queue.
/// Supports singleton background worker processing at 1 request/second.
/// Issue #3541 - BGG Import Queue Service
/// </summary>
public sealed class BggImportQueueEntity
{
    /// <summary>
    /// Primary key
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// BGG game ID to import
    /// </summary>
    public required int BggId { get; set; }

    /// <summary>
    /// Optional game name for UI display before import completes
    /// </summary>
    public string? GameName { get; set; }

    /// <summary>
    /// Current status in the queue
    /// </summary>
    public required BggImportStatus Status { get; set; }

    /// <summary>
    /// Position in queue (1 = next to process)
    /// Updated automatically as queue progresses
    /// </summary>
    public required int Position { get; set; }

    /// <summary>
    /// Number of retry attempts made (max 3)
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Error message if status is Failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When the import was processed (completed or failed)
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// ID of the created SharedGame entity (if successful)
    /// </summary>
    public Guid? CreatedGameId { get; set; }

    /// <summary>
    /// User who requested the import (for audit trail and command attribution)
    /// </summary>
    public Guid? RequestedByUserId { get; set; }

    /// <summary>
    /// When the queue entry was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the queue entry was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// BGG import job status
/// </summary>
public enum BggImportStatus
{
    /// <summary>
    /// Waiting in queue to be processed
    /// </summary>
    Queued = 0,

    /// <summary>
    /// Currently being processed by background worker
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Successfully completed - game created
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Failed after max retries (3 attempts)
    /// </summary>
    Failed = 3
}
