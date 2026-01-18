using System.Text.Json;
using Api.Infrastructure.Health.Models;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Routing;

/// <summary>
/// Endpoints for comprehensive health check monitoring.
/// </summary>
public static class HealthCheckEndpoints
{
    private static readonly JsonSerializerOptions HealthCheckJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Maps comprehensive health check endpoint at /api/v1/health.
    /// Returns detailed status for all services with criticality flags.
    /// </summary>
    /// <param name="app">The web application.</param>
    public static void MapHealthCheckEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/api/v1/health", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";

                var checks = report.Entries.Select(entry => new
                {
                    serviceName = entry.Key,
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description ?? string.Empty,
                    isCritical = entry.Value.Tags.Contains(HealthCheckTags.Critical, StringComparer.OrdinalIgnoreCase),
                    timestamp = DateTime.UtcNow
                }).ToList();

                // Determine overall status:
                // - Healthy: All checks are Healthy
                // - Unhealthy: At least one critical service is Unhealthy
                // - Degraded: At least one non-critical service is Degraded/Unhealthy
                var overallStatus = DetermineOverallStatus(report);

                var response = new
                {
                    overallStatus,
                    checks,
                    timestamp = DateTime.UtcNow
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(response, HealthCheckJsonOptions)).ConfigureAwait(false);
            }
        })
        .WithName("ComprehensiveHealthCheck")
        .WithTags("Health")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Comprehensive health check for all services";
            operation.Description = "Returns detailed health status for Core Infrastructure, AI Services, External APIs, and Monitoring services with criticality flags.";
            return operation;
        })
        .AllowAnonymous(); // Public endpoint, no authentication required
    }

    private static string DetermineOverallStatus(HealthReport report)
    {
        // Check if any critical service is Unhealthy
        var hasCriticalFailure = report.Entries.Any(entry =>
            entry.Value.Status == HealthStatus.Unhealthy &&
            entry.Value.Tags.Contains(HealthCheckTags.Critical, StringComparer.OrdinalIgnoreCase));

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
