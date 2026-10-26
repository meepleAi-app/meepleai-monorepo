namespace Api.BoundedContexts.KnowledgeBase.Application.Configuration;

/// <summary>
/// Configuration options for the job that blanks the texts of old <c>rag_executions</c> rows.
/// Issue #4634: the playground saves the question and previews of the retrieved passages with no expiry.
/// </summary>
public sealed class RagExecutionTextRetentionOptions
{
    /// <summary>
    /// Configuration section key.
    /// </summary>
    public const string SectionKey = "BackgroundJobs:RagExecutionTextRetention";

    /// <summary>
    /// Default age, in days, after which a row loses its texts. 90 is the window the admin event views already use
    /// and the one #4311 picked for <c>ai_request_logs</c>.
    /// </summary>
    public const int DefaultRetentionDays = 90;

    /// <summary>
    /// Rows blanked per statement, to avoid one long write on the table. Not a setting: the connection has a 30 second
    /// command timeout, and a batch large enough to exceed it would roll back as a whole every night, so the backlog
    /// would never shrink. 5,000 rows leaves a wide margin.
    /// </summary>
    public const int BatchSize = 5_000;

    /// <summary>
    /// Rows older than this many days lose their texts. Zero or less is a misconfiguration, not "blank everything":
    /// the job logs a warning and touches nothing.
    /// </summary>
    public int RetentionDays { get; set; } = DefaultRetentionDays;
}
