using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for BoardGameGeek (BGG) XML API connectivity.
/// </summary>
public class BggApiHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BggApiHealthCheck> _logger;

    public BggApiHealthCheck(
        IHttpClientFactory httpClientFactory,
        ILogger<BggApiHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("bggapi");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            // Simple connectivity test with BGG API
            var response = await client.GetAsync("/xmlapi2/search?query=test&type=boardgame", cts.Token).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("BGG API is accessible")
                : HealthCheckResult.Degraded($"BGG API returned {response.StatusCode}");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "BGG API health check timeout (>5s)");
            return HealthCheckResult.Degraded("Timeout checking BGG API connectivity");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "BGG API health check failed - HTTP request error");
            return HealthCheckResult.Unhealthy("BGG API unavailable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BGG API health check failed - unexpected error");
            return HealthCheckResult.Unhealthy("BGG API connectivity check failed", ex);
        }
    }
}