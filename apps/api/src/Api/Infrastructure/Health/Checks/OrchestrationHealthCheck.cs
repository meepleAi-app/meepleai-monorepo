using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for Python orchestration (LangGraph) microservice.
/// </summary>
public class OrchestrationHealthCheck : IHealthCheck
{
#pragma warning disable S1075 // URI should not be hardcoded — default for Docker internal service discovery
    private const string DefaultOrchestrationUrl = "http://orchestration-service:8004";
#pragma warning restore S1075

    private readonly IConfiguration _configuration;
    private readonly ILogger<OrchestrationHealthCheck> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public OrchestrationHealthCheck(
        IConfiguration configuration,
        ILogger<OrchestrationHealthCheck> logger,
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
        var orchestratorUrl = _configuration["ORCHESTRATION_SERVICE_URL"]
            ?? Environment.GetEnvironmentVariable("ORCHESTRATION_SERVICE_URL")
            ?? DefaultOrchestrationUrl;
        if (string.IsNullOrWhiteSpace(orchestratorUrl))
        {
            return HealthCheckResult.Degraded("Orchestration service not configured");
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(orchestratorUrl);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await client.GetAsync("/health", cts.Token).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Orchestration service is running")
                : HealthCheckResult.Degraded($"Orchestration service returned {response.StatusCode}");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Orchestration service health check timeout (>5s)");
            return HealthCheckResult.Degraded("Timeout checking orchestration service");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Orchestration service health check failed - HTTP request error");
            return HealthCheckResult.Unhealthy("Orchestration service unavailable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestration service health check failed - unexpected error");
            return HealthCheckResult.Unhealthy("Orchestration service check failed", ex);
        }
    }
}
