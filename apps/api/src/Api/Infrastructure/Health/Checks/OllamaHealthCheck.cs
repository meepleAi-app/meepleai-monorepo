using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for Ollama LLM service.
/// </summary>
public class OllamaHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OllamaHealthCheck> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public OllamaHealthCheck(
        IConfiguration configuration,
        ILogger<OllamaHealthCheck> logger,
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
        // OLLAMA_URL gating is performed at registration time in HealthCheckServiceExtensions:
        // this check is registered only when OLLAMA_URL is configured.
        var ollamaUrl = _configuration["OLLAMA_URL"];
        if (string.IsNullOrWhiteSpace(ollamaUrl))
        {
            // Defensive: if registration somehow drifted from configuration, surface
            // Unhealthy so monitoring catches the misregistration rather than silently
            // returning Degraded.
            return HealthCheckResult.Unhealthy("Ollama health check registered without OLLAMA_URL — registration/config drift");
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(ollamaUrl);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await client.GetAsync("/", cts.Token).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Ollama service is running")
                : HealthCheckResult.Degraded($"Ollama service returned {response.StatusCode}");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Ollama service health check timeout (>5s)");
            return HealthCheckResult.Degraded("Timeout checking Ollama service");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Ollama service health check failed - HTTP request error");
            return HealthCheckResult.Unhealthy("Ollama service unavailable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama service health check failed - unexpected error");
            return HealthCheckResult.Unhealthy("Ollama service check failed", ex);
        }
    }
}
