using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for OpenRouter AI API connectivity and authentication.
/// </summary>
public class OpenRouterHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenRouterHealthCheck> _logger;

    public OpenRouterHealthCheck(
        IHttpClientFactory httpClientFactory,
        ILogger<OpenRouterHealthCheck> logger)
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
            var client = _httpClientFactory.CreateClient("OpenRouter");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            // Simple connectivity test to OpenRouter API
            var response = await client.GetAsync("/api/v1/models", cts.Token).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("OpenRouter API is accessible")
                : HealthCheckResult.Degraded($"OpenRouter API returned {response.StatusCode}");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "OpenRouter health check timeout (>5s)");
            return HealthCheckResult.Degraded("Timeout checking OpenRouter connectivity");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "OpenRouter health check failed - HTTP request error");
            return HealthCheckResult.Unhealthy("OpenRouter API unavailable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenRouter health check failed - unexpected error");
            return HealthCheckResult.Unhealthy("OpenRouter connectivity check failed", ex);
        }
    }
}