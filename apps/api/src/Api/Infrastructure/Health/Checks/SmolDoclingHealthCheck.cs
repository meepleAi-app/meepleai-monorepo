using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for SmolDocling document intelligence service.
/// </summary>
public class SmolDoclingHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmolDoclingHealthCheck> _logger;

    public SmolDoclingHealthCheck(
        IConfiguration configuration,
        ILogger<SmolDoclingHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var smoldoclingUrl = _configuration["PdfProcessing:SmolDoclingApiUrl"];
        if (string.IsNullOrWhiteSpace(smoldoclingUrl))
        {
            return HealthCheckResult.Degraded("SmolDocling API not configured");
        }

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(smoldoclingUrl) };
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
            return HealthCheckResult.Unhealthy("SmolDocling service unavailable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmolDocling health check failed - unexpected error");
            return HealthCheckResult.Unhealthy("SmolDocling service check failed", ex);
        }
    }
}