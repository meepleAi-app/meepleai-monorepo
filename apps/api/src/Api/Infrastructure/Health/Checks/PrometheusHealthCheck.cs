using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for Prometheus metrics service connectivity.
/// </summary>
public class PrometheusHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PrometheusHealthCheck> _logger;

    public PrometheusHealthCheck(
        IConfiguration configuration,
        ILogger<PrometheusHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
        const string DefaultPrometheusUrl = "http://prometheus:9090";
#pragma warning restore S1075
        var prometheusUrl = _configuration["Monitoring:PrometheusUrl"] ?? DefaultPrometheusUrl;

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(prometheusUrl) };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await client.GetAsync("/-/healthy", cts.Token).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Prometheus is accessible")
                : HealthCheckResult.Degraded($"Prometheus returned {response.StatusCode}");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Prometheus health check timeout (>5s)");
            return HealthCheckResult.Degraded("Timeout checking Prometheus connectivity");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Prometheus health check failed - HTTP request error");
            return HealthCheckResult.Unhealthy("Prometheus unavailable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Prometheus health check failed - unexpected error");
            return HealthCheckResult.Unhealthy("Prometheus connectivity check failed", ex);
        }
    }
}