namespace Api.BoundedContexts.Administration.Application.Configuration;

/// <summary>
/// Configuration options for orphaned task cleanup background job.
/// ISSUE-2528: Configurable cleanup interval and retention period
/// </summary>
public sealed class OrphanedTaskCleanupOptions
{
    /// <summary>
    /// Configuration section key.
    /// </summary>
    public const string SectionKey = "BackgroundJobs:OrphanedTaskCleanup";

    /// <summary>
    /// Interval between cleanup job executions.
    /// Default: 1 hour (as per issue requirements)
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Maximum age for failed/cancelled tasks before cleanup.
    /// Default: 24 hours (as per issue requirements)
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Enable/disable the cleanup job.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;
}
