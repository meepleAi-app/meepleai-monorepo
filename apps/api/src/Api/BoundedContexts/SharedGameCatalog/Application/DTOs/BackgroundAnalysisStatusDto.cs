using Api.Infrastructure.BackgroundTasks;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Public status enum for API contract.
/// </summary>
public enum AnalysisTaskStatus
{
    Scheduled,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Status DTO for background rulebook analysis tasks.
/// Issue #2454: Background Processing for Large Rulebooks
/// </summary>
public record BackgroundAnalysisStatusDto(
    string TaskId,
    AnalysisTaskStatus Status,
    int ProgressPercentage,
    string? CurrentPhase,
    string? StatusMessage,
    TimeSpan? EstimatedTimeRemaining,
    RulebookAnalysisDto? Result
)
{
    /// <summary>
    /// Creates status for scheduled/running task.
    /// </summary>
    public static BackgroundAnalysisStatusDto CreateRunning(
        string taskId,
        AnalysisTaskStatus status,
        int progressPercentage,
        string? currentPhase,
        string? statusMessage,
        TimeSpan? estimatedTime) =>
        new(taskId, status, progressPercentage, currentPhase, statusMessage, estimatedTime, null);

    /// <summary>
    /// Creates status for completed task with result.
    /// </summary>
    public static BackgroundAnalysisStatusDto CreateCompleted(
        string taskId,
        RulebookAnalysisDto result) =>
        new(taskId, AnalysisTaskStatus.Completed, 100, "Completed", "Analysis complete", null, result);

    /// <summary>
    /// Creates status for failed task.
    /// </summary>
    public static BackgroundAnalysisStatusDto CreateFailed(
        string taskId,
        string errorMessage) =>
        new(taskId, AnalysisTaskStatus.Failed, 0, "Failed", errorMessage, null, null);
};
