using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for Python embedding microservice.
/// </summary>
public class EmbeddingServiceHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EmbeddingServiceHealthCheck> _logger;

    public EmbeddingServiceHealthCheck(
        IHttpClientFactory httpClientFactory,
        ILogger<EmbeddingServiceHealthCheck> logger)
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
            var client = _httpClientFactory.CreateClient("EmbeddingService");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await client.GetAsync("/health", cts.Token).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Embedding service is running")
                : HealthCheckResult.Degraded($"Embedding service returned {response.StatusCode}");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Embedding service health check timeout (>5s)");
            return HealthCheckResult.Degraded("Timeout checking embedding service");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Embedding service health check failed - HTTP request error");
            return HealthCheckResult.Unhealthy("Embedding service unavailable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Embedding service health check failed - unexpected error");
            return HealthCheckResult.Unhealthy("Embedding service check failed", ex);
        }
    }
}