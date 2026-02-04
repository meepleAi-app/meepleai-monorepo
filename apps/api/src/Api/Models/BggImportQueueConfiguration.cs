namespace Api.Models;

/// <summary>
/// Configuration for BGG Import Queue Service.
/// Issue #3541: BGG Import Queue Service with rate limiting and retry logic.
/// </summary>
internal class BggImportQueueConfiguration
{
    /// <summary>
    /// Enable/disable the background queue processor (default: true).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Initial delay before processing starts in minutes (default: 1 minute).
    /// Allows application to fully start before queue processing begins.
    /// </summary>
    public double InitialDelayMinutes { get; set; } = 1;

    /// <summary>
    /// Processing interval in seconds (default: 1 second for BGG rate limit).
    /// BGG API allows 1 request per second max.
    /// </summary>
    public double ProcessingIntervalSeconds { get; set; } = 1.0;

    /// <summary>
    /// Maximum retry attempts for failed imports (default: 3).
    /// After max retries, job status changes to Failed.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Exponential backoff base delay in seconds (default: 2).
    /// Actual delay = BaseRetryDelaySeconds * (2 ^ RetryCount).
    /// Example: 2s, 4s, 8s for retries 1, 2, 3.
    /// </summary>
    public int BaseRetryDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Auto-cleanup completed/failed jobs older than N days (default: 7).
    /// 0 = disable auto-cleanup.
    /// </summary>
    public int AutoCleanupDays { get; set; } = 7;

    /// <summary>
    /// Maximum concurrent processing jobs (default: 1 for singleton queue).
    /// BGG rate limit requires sequential processing.
    /// </summary>
    public int MaxConcurrentJobs { get; set; } = 1;
}
