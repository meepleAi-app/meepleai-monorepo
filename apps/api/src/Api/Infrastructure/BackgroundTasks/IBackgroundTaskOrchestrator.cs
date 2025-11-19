using System;
using System.Threading;
using System.Threading.Tasks;

namespace Api.Infrastructure.BackgroundTasks;

/// <summary>
/// Provides centralized orchestration for background tasks with distributed coordination via Redis.
/// Supports task scheduling, status tracking, cancellation, and distributed locking.
/// </summary>
public interface IBackgroundTaskOrchestrator
{
    /// <summary>
    /// Schedules a background task for immediate execution.
    /// </summary>
    /// <param name="taskId">Unique identifier for the task</param>
    /// <param name="taskName">Human-readable task name for logging</param>
    /// <param name="taskFactory">Factory function that produces the task to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the scheduling operation</returns>
    Task ScheduleAsync(
        string taskId,
        string taskName,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a background task for delayed execution.
    /// </summary>
    /// <param name="taskId">Unique identifier for the task</param>
    /// <param name="taskName">Human-readable task name for logging</param>
    /// <param name="delay">Delay before execution</param>
    /// <param name="taskFactory">Factory function that produces the task to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the scheduling operation</returns>
    Task ScheduleDelayedAsync(
        string taskId,
        string taskName,
        TimeSpan delay,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a recurring background task.
    /// </summary>
    /// <param name="taskId">Unique identifier for the task</param>
    /// <param name="taskName">Human-readable task name for logging</param>
    /// <param name="interval">Interval between executions</param>
    /// <param name="taskFactory">Factory function that produces the task to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the scheduling operation</returns>
    Task ScheduleRecurringAsync(
        string taskId,
        string taskName,
        TimeSpan interval,
        Func<CancellationToken, Task> taskFactory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a running or scheduled background task.
    /// </summary>
    /// <param name="taskId">Unique identifier for the task to cancel</param>
    /// <returns>True if task was cancelled, false if task was not found</returns>
    Task<bool> CancelAsync(string taskId);

    /// <summary>
    /// Gets the current status of a background task.
    /// </summary>
    /// <param name="taskId">Unique identifier for the task</param>
    /// <returns>Task status or null if task not found</returns>
    Task<BackgroundTaskStatus?> GetStatusAsync(string taskId);

    /// <summary>
    /// Executes a task with distributed lock coordination via Redis.
    /// Ensures only one instance of the task runs across all servers.
    /// </summary>
    /// <param name="lockKey">Unique lock identifier</param>
    /// <param name="taskFactory">Factory function that produces the task to execute</param>
    /// <param name="lockTimeout">Maximum duration to hold the lock</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if lock was acquired and task executed, false if lock was already held</returns>
    Task<bool> ExecuteWithDistributedLockAsync(
        string lockKey,
        Func<CancellationToken, Task> taskFactory,
        TimeSpan lockTimeout,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the status of a background task.
/// </summary>
public enum BackgroundTaskStatus
{
    /// <summary>
    /// Task is scheduled but not yet running.
    /// </summary>
    Scheduled,

    /// <summary>
    /// Task is currently executing.
    /// </summary>
    Running,

    /// <summary>
    /// Task completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Task failed with an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Task was cancelled.
    /// </summary>
    Cancelled
}
