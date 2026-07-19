using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for Python reranker microservice.
/// </summary>
public class RerankerHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RerankerHealthCheck> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public RerankerHealthCheck(
        IConfiguration configuration,
        ILogger<RerankerHealthCheck> logger,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var rerankerUrl = _configuration["RERANKER_URL"];
        if (string.IsNullOrWhiteSpace(rerankerUrl))
        {
            return HealthCheckResult.Degraded("Reranker service not configured (RERANKER_URL missing)");
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(rerankerUrl);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            // Python ML microservice: model load / GC pauses / cold-start make 5s too
            // tight and trip Degraded on healthy-but-busy nodes. 10s absorbs those blips.
            // Registration timeout (HealthCheckServiceExtensions) is 12s, above this.
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var response = await client.GetAsync("/health", cts.Token).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Reranker service is running")
                : HealthCheckResult.Degraded($"Reranker service returned {response.StatusCode}");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Reranker service health check timeout (>10s)");
            return HealthCheckResult.Degraded("Timeout checking reranker service");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Reranker service health check failed - HTTP request error");
            return HealthCheckResult.Unhealthy("Reranker service unavailable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reranker service health check failed - unexpected error");
            return HealthCheckResult.Unhealthy("Reranker service check failed", ex);
        }
    }
}
