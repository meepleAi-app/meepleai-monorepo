namespace Api.SharedKernel.Constants;

/// <summary>
/// Centralized Redis key constants for background analysis.
/// Issue #2454: Background Processing for Large Rulebooks
/// </summary>
public static class RedisKeyConstants
{
    /// <summary>
    /// Base prefix for all MeepleAI Redis keys.
    /// </summary>
    private const string BasePrefix = "meepleai";

    /// <summary>
    /// Prefix for background task status keys.
    /// Format: meepleai:tasks:analysis:{taskId}
    /// </summary>
    public static string TaskStatusPrefix => $"{BasePrefix}:tasks:analysis";

    /// <summary>
    /// Prefix for analysis progress tracking.
    /// Format: meepleai:analysis:progress:{taskId}
    /// </summary>
    public static string ProgressPrefix => $"{BasePrefix}:analysis:progress";

    /// <summary>
    /// Prefix for partial results during analysis.
    /// Format: meepleai:analysis:partial:{taskId}:phase_{N}
    /// </summary>
    public static string PartialResultsPrefix => $"{BasePrefix}:analysis:partial";

    /// <summary>
    /// Prefix for distributed locks.
    /// Format: meepleai:tasks:lock:rulebook:analysis:{gameId}:{pdfId}
    /// </summary>
    public static string DistributedLockPrefix => $"{BasePrefix}:tasks:lock:rulebook:analysis";

    /// <summary>
    /// Cache tag for game-specific invalidation.
    /// Format: game:{gameId}
    /// </summary>
    public static string GetGameTag(Guid gameId) => $"game:{gameId}";

    /// <summary>
    /// Cache tag for PDF-specific invalidation.
    /// Format: pdf:{pdfId}
    /// </summary>
    public static string GetPdfTag(Guid pdfId) => $"pdf:{pdfId}";

    /// <summary>
    /// Generates progress tracking key for a task.
    /// </summary>
    public static string GetProgressKey(string taskId) => $"{ProgressPrefix}:{taskId}";

    /// <summary>
    /// Generates task status key.
    /// </summary>
    public static string GetTaskStatusKey(string taskId) => $"{TaskStatusPrefix}:{taskId}";

    /// <summary>
    /// Generates distributed lock key for rulebook analysis.
    /// </summary>
    public static string GetAnalysisLockKey(Guid gameId, Guid pdfId) =>
        $"{DistributedLockPrefix}:{gameId}:{pdfId}";

    /// <summary>
    /// Generates partial result key for a specific phase.
    /// </summary>
    public static string GetPartialResultKey(string taskId, int phaseNumber) =>
        $"{PartialResultsPrefix}:{taskId}:phase_{phaseNumber}";
}
