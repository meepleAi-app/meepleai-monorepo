using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for HyperDX OpenTelemetry endpoint configuration.
/// Validates that OTLP endpoint is configured for tracing/metrics export.
/// </summary>
public class HyperDxHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<HyperDxHealthCheck> _logger;

    public HyperDxHealthCheck(
        IConfiguration configuration,
        ILogger<HyperDxHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var otlpEndpoint = _configuration["HYPERDX_OTLP_ENDPOINT"];
        var hyperdxApiKey = _configuration["HYPERDX_API_KEY"];

        if (string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            _logger.LogWarning("HyperDX OTLP endpoint not configured");
            return Task.FromResult(HealthCheckResult.Degraded("HyperDX OTLP endpoint not configured"));
        }

        if (string.IsNullOrWhiteSpace(hyperdxApiKey))
        {
            _logger.LogWarning("HyperDX API key not configured");
            return Task.FromResult(HealthCheckResult.Degraded("HyperDX API key not configured"));
        }

        return Task.FromResult(HealthCheckResult.Healthy($"HyperDX OTLP configured: {otlpEndpoint}"));
    }
}
