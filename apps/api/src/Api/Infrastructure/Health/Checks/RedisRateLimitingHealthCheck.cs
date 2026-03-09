using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Issue #5477: Health check for the Redis rate-limiting subsystem.
/// Reports Degraded when RedisRateLimitingHealthMonitor detects Redis is unavailable,
/// which means rate limiting is silently disabled.
/// </summary>
public sealed class RedisRateLimitingHealthCheck : IHealthCheck
{
    private readonly IEnumerable<IHostedService> _hostedServices;

    public RedisRateLimitingHealthCheck(IEnumerable<IHostedService> hostedServices)
    {
        _hostedServices = hostedServices;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var monitor = _hostedServices
            .OfType<RedisRateLimitingHealthMonitor>()
            .FirstOrDefault();

        if (monitor == null)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "RedisRateLimitingHealthMonitor not registered"));
        }

        if (monitor.IsRateLimitingDegraded)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Redis unavailable — rate limiting is disabled. " +
                $"Failure mode: {monitor.FailureMode}"));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "Redis rate-limiting subsystem operational"));
    }
}
