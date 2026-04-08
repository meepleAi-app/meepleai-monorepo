using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Health;

/// <summary>
/// BackgroundService that polls the AutoSave health tracker every 30 seconds and
/// emits a warning log when the last run is older than 120 seconds (2 missed
/// 60s intervals). Complements the Prometheus gauge with a textual signal.
/// </summary>
internal sealed class AutoSaveHealthLoggerService : BackgroundService
{
    private const int StaleThresholdSeconds = 120;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IAutoSaveHealthTracker _tracker;
    private readonly ILogger<AutoSaveHealthLoggerService> _logger;

    public AutoSaveHealthLoggerService(
        IAutoSaveHealthTracker tracker,
        ILogger<AutoSaveHealthLoggerService> logger)
    {
        _tracker = tracker;
        _logger = logger;
    }

    /// <summary>
    /// Single evaluation pass. Public for unit testing.
    /// </summary>
    public void EvaluateAndLog()
    {
        var ageSeconds = _tracker.GetLastRunAgeSeconds();
        if (ageSeconds is null)
        {
            return;
        }

        if (ageSeconds.Value > StaleThresholdSeconds)
        {
            _logger.LogWarning(
                "AutoSave job stale: last run {AgeSeconds} seconds ago (threshold {Threshold}s)",
                ageSeconds.Value,
                StaleThresholdSeconds);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                EvaluateAndLog();
            }
#pragma warning disable CA1031 // Background loop must not throw
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutoSaveHealthLoggerService evaluation failed");
            }
#pragma warning restore CA1031

            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
