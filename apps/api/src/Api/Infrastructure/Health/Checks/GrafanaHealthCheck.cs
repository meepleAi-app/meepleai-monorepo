using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for Grafana monitoring service connectivity.
/// </summary>
public class GrafanaHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GrafanaHealthCheck> _logger;

    public GrafanaHealthCheck(
        IConfiguration configuration,
        ILogger<GrafanaHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
        const string DefaultGrafanaUrl = "http://grafana:3000";
#pragma warning restore S1075
        var grafanaUrl = _configuration["Monitoring:GrafanaUrl"] ?? DefaultGrafanaUrl;

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(grafanaUrl) };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await client.GetAsync("/api/health", cts.Token).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Grafana is accessible")
                : HealthCheckResult.Degraded($"Grafana returned {response.StatusCode}");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Grafana health check timeout (>5s)");
            return HealthCheckResult.Degraded("Timeout checking Grafana connectivity");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Grafana health check failed - HTTP request error");
            return HealthCheckResult.Unhealthy("Grafana unavailable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Grafana health check failed - unexpected error");
            return HealthCheckResult.Unhealthy("Grafana connectivity check failed", ex);
        }
    }
}