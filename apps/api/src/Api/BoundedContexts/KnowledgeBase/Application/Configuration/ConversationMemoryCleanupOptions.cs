namespace Api.BoundedContexts.KnowledgeBase.Application.Configuration;

/// <summary>
/// Configuration options for conversation memory cleanup background job.
/// Issue #3498: GDPR-compliant cleanup of conversation memories older than retention period.
/// </summary>
public sealed class ConversationMemoryCleanupOptions
{
    /// <summary>
    /// Configuration section key.
    /// </summary>
    public const string SectionKey = "BackgroundJobs:ConversationMemoryCleanup";

    /// <summary>
    /// Interval between cleanup job executions.
    /// Default: 24 hours (daily cleanup as per GDPR best practices)
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Maximum age for conversation memories before cleanup.
    /// Default: 90 days (as per issue requirements for GDPR compliance)
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Enable/disable the cleanup job.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Batch size for deletion operations to avoid long-running transactions.
    /// Default: 1000
    /// </summary>
    public int BatchSize { get; set; } = 1000;
}
