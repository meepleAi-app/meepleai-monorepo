using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for Python orchestration (LangGraph) microservice.
/// </summary>
public class OrchestrationHealthCheck : IHealthCheck
{
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
            ?? Environment.GetEnvironmentVariable("ORCHESTRATION_SERVICE_URL");
        if (string.IsNullOrWhiteSpace(orchestratorUrl))
        {
            // Registration is gated on ORCHESTRATION_SERVICE_URL (see
            // HealthCheckServiceExtensions), so this branch is only hit under config
            // drift. Return Degraded (never Unhealthy) — orchestration is a
            // NonCritical/Optional service and must not 503 the aggregate /health (#3339).
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
            // #3339: orchestration is NonCritical/Optional (compose profile `tutor-agents`,
            // not the standard deploy). A connection failure must NOT 503 the aggregate
            // /health — return Degraded, consistent with the timeout branch above and the
            // Degraded failureStatus/NonCritical registration.
            _logger.LogWarning(ex, "Orchestration service health check failed - HTTP request error");
            return HealthCheckResult.Degraded("Orchestration service unavailable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Orchestration service health check failed - unexpected error");
            return HealthCheckResult.Degraded("Orchestration service check failed", ex);
        }
    }
}
