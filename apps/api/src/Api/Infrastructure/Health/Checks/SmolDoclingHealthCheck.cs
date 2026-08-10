using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for SmolDocling document intelligence service.
/// </summary>
public class SmolDoclingHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmolDoclingHealthCheck> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public SmolDoclingHealthCheck(
        IConfiguration configuration,
        ILogger<SmolDoclingHealthCheck> logger,
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
        // Provider gating is performed at registration time in HealthCheckServiceExtensions:
        // this check is registered only when PdfProcessing:Extractor:Provider routes to SmolDocling.
        var smoldoclingUrl = _configuration["PdfProcessing:Extractor:SmolDocling:ApiUrl"];
        if (string.IsNullOrWhiteSpace(smoldoclingUrl))
        {
            // Provider selected but URL missing — real misconfiguration. Surface Degraded,
            // not Unhealthy: this check is NonCritical/Optional and must not 503 the
            // aggregate /health (#3618). Monitoring still catches it — HealthStateMachine
            // treats any non-Healthy result as a failure (consistent with OllamaHealthCheck).
            return HealthCheckResult.Degraded("SmolDocling API URL missing — provider selected but PdfProcessing:Extractor:SmolDocling:ApiUrl unset");
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(smoldoclingUrl);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await client.GetAsync("/health", cts.Token).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("SmolDocling service is running")
                : HealthCheckResult.Degraded($"SmolDocling returned {response.StatusCode}");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "SmolDocling health check timeout (>5s)");
            return HealthCheckResult.Degraded("Timeout checking SmolDocling service");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "SmolDocling health check failed - HTTP request error");
            return HealthCheckResult.Degraded("SmolDocling service unavailable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmolDocling health check failed - unexpected error");
            return HealthCheckResult.Degraded("SmolDocling service check failed", ex);
        }
    }
}