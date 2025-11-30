using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.BackgroundTasks;

/// <summary>
/// Background cleanup task for expired TOTP codes
/// SECURITY: Issue #1787 - Removes expired used_totp_codes entries hourly
/// Pattern: IHostedService with scheduled execution
/// </summary>
public class UsedTotpCodeCleanupTask : IHostedService, IDisposable
{
    private Timer? _timer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UsedTotpCodeCleanupTask> _logger;
    private readonly TimeProvider _timeProvider;

    public UsedTotpCodeCleanupTask(
        IServiceProvider serviceProvider,
        ILogger<UsedTotpCodeCleanupTask> logger,
        TimeProvider? timeProvider = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Start background task with 5-minute initial delay, then hourly execution
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("UsedTotpCodeCleanupTask: Starting background task");

        // Start after 5 minutes, then run every hour
        _timer = new Timer(
            CleanupExpiredCodes,
            null,
            TimeSpan.FromMinutes(5), // Initial delay
            TimeSpan.FromHours(1));  // Period

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop background task
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("UsedTotpCodeCleanupTask: Stopping background task");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleanup expired TOTP codes (ExecuteDelete for efficiency)
    /// </summary>
    private async void CleanupExpiredCodes(object? state)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // EF Core 7+: ExecuteDelete for bulk delete without loading entities
            var deleted = await dbContext.UsedTotpCodes
                .Where(u => u.ExpiresAt < now)
                .ExecuteDeleteAsync();

            if (deleted > 0)
            {
                _logger.LogInformation("UsedTotpCodeCleanupTask: Cleaned up {Count} expired TOTP codes", deleted);
            }
            else
            {
                _logger.LogDebug("UsedTotpCodeCleanupTask: No expired TOTP codes to clean up");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UsedTotpCodeCleanupTask: Error during cleanup");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer?.Dispose();
        }
    }
}
