using Api.BoundedContexts.Administration.Domain.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.BoundedContexts.Administration.Infrastructure.External;

/// <summary>
/// Infrastructure implementation for health monitoring service.
/// Issue #891: Aggregates health checks from ASP.NET Core HealthCheckService.
/// </summary>
internal class InfrastructureHealthService : IInfrastructureHealthService
{
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger<InfrastructureHealthService> _logger;

    // Issue #892: Monitored services including n8n
    private static readonly string[] MonitoredServices = { "postgres", "redis", "qdrant", "qdrant-collection", "embedding", "n8n" };

    public InfrastructureHealthService(
        HealthCheckService healthCheckService,
        ILogger<InfrastructureHealthService> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    public async Task<ServiceHealthStatus> GetServiceHealthAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Execute health check for specific service
            var healthReport = await _healthCheckService.CheckHealthAsync(
                check => check.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase),
                cancellationToken).ConfigureAwait(false);

            var entryPair = healthReport.Entries.FirstOrDefault(e =>
                e.Key.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

            if (entryPair.Key is null)
            {
                _logger.LogWarning("Service {ServiceName} not found in health checks", serviceName);
                return new ServiceHealthStatus(
                    serviceName,
                    HealthState.Unhealthy,
                    $"Service '{serviceName}' not configured for health checks",
                    DateTime.UtcNow,
                    DateTime.UtcNow - startTime);
            }

            var entry = entryPair.Value;

            var state = MapHealthStatus(entry.Status);
            var errorMessage = entry.Exception?.Message ?? entry.Description;

            return new ServiceHealthStatus(
                serviceName,
                state,
                state == HealthState.Healthy ? null : errorMessage,
                DateTime.UtcNow,
                entry.Duration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check health for service {ServiceName}", serviceName);
            return new ServiceHealthStatus(
                serviceName,
                HealthState.Unhealthy,
                ex.Message,
                DateTime.UtcNow,
                DateTime.UtcNow - startTime);
        }
    }

    public async Task<IReadOnlyCollection<ServiceHealthStatus>> GetAllServicesHealthAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting health status for all infrastructure services");

        try
        {
            // Execute all health checks
            var healthReport = await _healthCheckService.CheckHealthAsync(cancellationToken).ConfigureAwait(false);

            var serviceStatuses = healthReport.Entries
                .Where(e => MonitoredServices.Contains(e.Key, StringComparer.OrdinalIgnoreCase))
                .Select(e =>
                {
                    var state = MapHealthStatus(e.Value.Status);
                    var errorMessage = e.Value.Exception?.Message ?? e.Value.Description;

                    return new ServiceHealthStatus(
                        e.Key,
                        state,
                        state == HealthState.Healthy ? null : errorMessage,
                        DateTime.UtcNow,
                        e.Value.Duration);
                })
                .ToList();

            _logger.LogInformation("Health check completed. {HealthyCount}/{TotalCount} services healthy",
                serviceStatuses.Count(s => s.State == HealthState.Healthy),
                serviceStatuses.Count);

            return serviceStatuses;
        }
#pragma warning disable S2139 // Exceptions should be either logged or rethrown but not both
        // INFRASTRUCTURE LOGGING PATTERN: Log exceptions at the infrastructure boundary for debugging.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get health status for all services");
            throw;
        }
#pragma warning restore S2139
    }

    public async Task<OverallHealthStatus> GetOverallHealthAsync(CancellationToken cancellationToken = default)
    {
        var services = await GetAllServicesHealthAsync(cancellationToken).ConfigureAwait(false);

        var healthyCount = services.Count(s => s.State == HealthState.Healthy);
        var degradedCount = services.Count(s => s.State == HealthState.Degraded);
        var unhealthyCount = services.Count(s => s.State == HealthState.Unhealthy);

        // Overall state: Unhealthy if any service is unhealthy, Degraded if any degraded, else Healthy
        HealthState overallState;
        if (unhealthyCount > 0)
        {
            overallState = HealthState.Unhealthy;
        }
        else if (degradedCount > 0)
        {
            overallState = HealthState.Degraded;
        }
        else
        {
            overallState = HealthState.Healthy;
        }

        return new OverallHealthStatus(
            overallState,
            services.Count,
            healthyCount,
            degradedCount,
            unhealthyCount,
            DateTime.UtcNow);
    }

    private static HealthState MapHealthStatus(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => HealthState.Healthy,
        HealthStatus.Degraded => HealthState.Degraded,
        HealthStatus.Unhealthy => HealthState.Unhealthy,
        _ => HealthState.Unhealthy
    };
}
