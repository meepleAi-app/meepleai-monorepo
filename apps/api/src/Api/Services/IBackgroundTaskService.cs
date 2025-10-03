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
}
