using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for Unstructured PDF processing API.
/// </summary>
public class UnstructuredHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<UnstructuredHealthCheck> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public UnstructuredHealthCheck(
        IConfiguration configuration,
        ILogger<UnstructuredHealthCheck> logger,
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
        var unstructuredUrl = _configuration["PdfProcessing:Extractor:Unstructured:ApiUrl"];
        if (string.IsNullOrWhiteSpace(unstructuredUrl))
        {
            return HealthCheckResult.Degraded("Unstructured API not configured");
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(unstructuredUrl);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            // Unstructured service health endpoint
            var response = await client.GetAsync("/health", cts.Token).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Unstructured API is accessible")
                : HealthCheckResult.Degraded($"Unstructured API returned {response.StatusCode}");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Unstructured API health check timeout (>5s)");
            return HealthCheckResult.Degraded("Timeout checking Unstructured API");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Unstructured API health check failed - HTTP request error");
            return HealthCheckResult.Unhealthy("Unstructured API unavailable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unstructured API health check failed - unexpected error");
            return HealthCheckResult.Unhealthy("Unstructured API check failed", ex);
        }
    }
}