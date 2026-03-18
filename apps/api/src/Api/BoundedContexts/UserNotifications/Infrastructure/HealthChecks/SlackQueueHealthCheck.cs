using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.HealthChecks;

/// <summary>
/// Health check that monitors the Slack notification queue depth.
/// Returns Unhealthy if the pending count exceeds the threshold (100),
/// indicating a potential processing bottleneck or Slack API issues.
/// </summary>
internal class SlackQueueHealthCheck : IHealthCheck
{
    private const int UnhealthyThreshold = 100;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SlackQueueHealthCheck> _logger;

    public SlackQueueHealthCheck(
        IServiceScopeFactory scopeFactory,
        ILogger<SlackQueueHealthCheck> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<INotificationQueueRepository>();
            var pendingCount = await repository.GetPendingCountAsync(cancellationToken).ConfigureAwait(false);

            var data = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["pending_count"] = pendingCount,
                ["threshold"] = UnhealthyThreshold
            };

            if (pendingCount > UnhealthyThreshold)
            {
                _logger.LogWarning(
                    "Slack queue health check: {PendingCount} pending items exceeds threshold of {Threshold}",
                    pendingCount, UnhealthyThreshold);

                return HealthCheckResult.Unhealthy(
                    $"Slack notification queue backlog: {pendingCount} pending (threshold: {UnhealthyThreshold})",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"Slack notification queue healthy: {pendingCount} pending",
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Slack queue health check failed");
            return HealthCheckResult.Unhealthy("Failed to check Slack queue health", ex);
        }
    }
}
