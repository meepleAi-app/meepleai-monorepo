namespace Api.Services;

/// <summary>
/// Service for executing background tasks
/// </summary>
public interface IBackgroundTaskService
{
    /// <summary>
    /// Executes a task in the background without blocking
    /// </summary>
    void Execute(Func<Task> task);

    /// <summary>
    /// Executes a task in the background with cancellation support
    /// </summary>
    /// <param name="taskId">Unique identifier for the task</param>
    /// <param name="taskFactory">Factory function that creates the task with a cancellation token</param>
    void ExecuteWithCancellation(string taskId, Func<CancellationToken, Task> taskFactory);

    /// <summary>
    /// Cancels a running background task by ID
    /// </summary>
    /// <param name="taskId">Unique identifier for the task</param>
    /// <returns>True if the task was found and cancelled, false otherwise</returns>
    bool CancelTask(string taskId);
}
