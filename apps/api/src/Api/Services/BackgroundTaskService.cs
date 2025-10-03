namespace Api.Services;

/// <summary>
/// Production implementation of background task execution using Task.Run
/// </summary>
public class BackgroundTaskService : IBackgroundTaskService
{
    private readonly ILogger<BackgroundTaskService> _logger;

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
}
