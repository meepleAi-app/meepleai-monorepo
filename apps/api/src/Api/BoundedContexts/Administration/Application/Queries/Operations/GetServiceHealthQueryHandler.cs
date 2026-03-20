using Api.BoundedContexts.Administration.Application.Queries.Operations;
using MediatR;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.BoundedContexts.Administration.Application.Queries.Operations;

/// <summary>
/// Handler for GetServiceHealthQuery.
/// Issue #3696: Operations - Service Control Panel.
/// Aggregates health status from ASP.NET Core HealthCheckService.
/// </summary>
internal sealed class GetServiceHealthQueryHandler
    : IRequestHandler<GetServiceHealthQuery, Api.BoundedContexts.Administration.Application.DTOs.ServiceHealthResponseDto>
{
    private readonly HealthCheckService _healthCheckService;
    private readonly TimeProvider _timeProvider;

    public GetServiceHealthQueryHandler(
        HealthCheckService healthCheckService,
        TimeProvider? timeProvider = null)
    {
        _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Api.BoundedContexts.Administration.Application.DTOs.ServiceHealthResponseDto> Handle(
        GetServiceHealthQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Get health check report from ASP.NET Core HealthCheckService
        var healthReport = await _healthCheckService
            .CheckHealthAsync(cancellationToken)
            .ConfigureAwait(false);

        var timestamp = _timeProvider.GetUtcNow().UtcDateTime;

        // Map health check entries to DTOs
        var services = healthReport.Entries
            .Select(entry => new Api.BoundedContexts.Administration.Application.DTOs.ServiceHealthDto(
                ServiceName: entry.Key,
                Status: entry.Value.Status.ToString(),
                Description: entry.Value.Description,
                IsCritical: entry.Value.Tags.Contains("critical", StringComparer.OrdinalIgnoreCase),
                Timestamp: timestamp
            ))
            .ToList();

        // Determine overall status
        var overallStatus = DetermineOverallStatus(healthReport);

        return new Api.BoundedContexts.Administration.Application.DTOs.ServiceHealthResponseDto(
            OverallStatus: overallStatus,
            Services: services,
            Timestamp: timestamp
        );
    }

    private static string DetermineOverallStatus(HealthReport report)
    {
        // Check if any critical service is Unhealthy
        var hasCriticalFailure = report.Entries.Any(entry =>
            entry.Value.Status == HealthStatus.Unhealthy &&
            entry.Value.Tags.Contains("critical", StringComparer.OrdinalIgnoreCase));

        if (hasCriticalFailure)
        {
            return "Unhealthy";
        }

        // Check if all services are Healthy
        if (report.Status == HealthStatus.Healthy)
        {
            return "Healthy";
        }

        // Some non-critical services are Degraded or Unhealthy
        return "Degraded";
    }
}
