using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.BackgroundTasks;

/// <summary>
/// Background cleanup task for expired TOTP codes
/// SECURITY: Issue #1787 - Removes expired used_totp_codes entries hourly
/// ARCH-01: Uses BackgroundService with PeriodicTimer instead of async void Timer callback
/// </summary>
internal class UsedTotpCodeCleanupTask : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UsedTotpCodeCleanupTask> _logger;
    private readonly TimeProvider _timeProvider;

    public UsedTotpCodeCleanupTask(
        IServiceProvider serviceProvider,
        ILogger<UsedTotpCodeCleanupTask> logger,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("UsedTotpCodeCleanupTask: Starting background task");

        // Initial delay of 5 minutes
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);

        // ARCH-01: Use PeriodicTimer for proper async execution (no async void)
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        // Run once immediately after initial delay, then on each tick
        await CleanupExpiredCodesAsync(stoppingToken).ConfigureAwait(false);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await CleanupExpiredCodesAsync(stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("UsedTotpCodeCleanupTask: Stopping background task");
    }

    /// <summary>
    /// Cleanup expired TOTP codes (ExecuteDelete for efficiency)
    /// </summary>
    private async Task CleanupExpiredCodesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // EF Core 7+: ExecuteDelete for bulk delete without loading entities
            var deleted = await dbContext.UsedTotpCodes
                .Where(u => u.ExpiresAt < now)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

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
}
