using System.Collections.Concurrent;

namespace Api.Services;

/// <summary>
/// Production implementation of background task execution using Task.Run
/// </summary>
internal class BackgroundTaskService : IBackgroundTaskService
{
    private readonly ILogger<BackgroundTaskService> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeTasks = new(StringComparer.Ordinal);

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
                await task().ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation in background task");
            }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
            // BACKGROUND SERVICE: Background service boundary - prevents task crash in fire-and-forget
            // Background service: Generic catch prevents task exception from crashing host process
            // Fire-and-forget tasks must not throw unhandled exceptions
            // Catch-all for unexpected errors in fire-and-forget tasks
#pragma warning restore S125
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in background task");
            }
#pragma warning restore CA1031
        }, CancellationToken.None);
    }

    public void ExecuteWithCancellation(string taskId, Func<CancellationToken, Task> taskFactory)
    {
        // S2930: CancellationTokenSource stored in dictionary for lifecycle management.
        // Disposed explicitly in finally block (line 81) or CancelTask() (line 92).
        // Cannot use 'using var' as disposal must occur when task completes or is cancelled.
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
                await taskFactory(cts.Token).ConfigureAwait(false);
                _logger.LogInformation("Background task {TaskId} completed successfully", taskId);
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogInformation(ex, "Background task {TaskId} was cancelled", taskId);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation in background task {TaskId}", taskId);
            }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
            // BACKGROUND SERVICE: Background service boundary - prevents task crash in fire-and-forget
            // Catch-all for unexpected errors in fire-and-forget tasks
#pragma warning restore S125
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in background task {TaskId}", taskId);
            }
#pragma warning restore CA1031
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
