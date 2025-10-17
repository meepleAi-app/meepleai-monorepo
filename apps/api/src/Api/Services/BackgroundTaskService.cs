using System.Collections.Concurrent;

namespace Api.Services;

/// <summary>
/// Production implementation of background task execution using Task.Run
/// </summary>
public class BackgroundTaskService : IBackgroundTaskService
{
    private readonly ILogger<BackgroundTaskService> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeTasks = new();

    public BackgroundTaskService(ILogger<BackgroundTaskService> logger)
    {
        _logger = logger;
    }

    public void Execute(Func<Task> task)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await task();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background task failed");
            }
        }, CancellationToken.None);
    }

    public void ExecuteWithCancellation(string taskId, Func<CancellationToken, Task> taskFactory)
    {
        var cts = new CancellationTokenSource();

        if (!_activeTasks.TryAdd(taskId, cts))
        {
            _logger.LogWarning("Task with ID {TaskId} is already running", taskId);
            cts.Dispose();
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Starting background task {TaskId}", taskId);
                await taskFactory(cts.Token);
                _logger.LogInformation("Background task {TaskId} completed successfully", taskId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Background task {TaskId} was cancelled", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background task {TaskId} failed", taskId);
            }
            finally
            {
                _activeTasks.TryRemove(taskId, out _);
                cts.Dispose();
            }
        }, CancellationToken.None);
    }

    public bool CancelTask(string taskId)
    {
        if (_activeTasks.TryRemove(taskId, out var cts))
        {
            _logger.LogInformation("Cancelling background task {TaskId}", taskId);
            cts.Cancel();
            cts.Dispose();
            return true;
        }

        _logger.LogWarning("Task {TaskId} not found or already completed", taskId);
        return false;
    }
}
