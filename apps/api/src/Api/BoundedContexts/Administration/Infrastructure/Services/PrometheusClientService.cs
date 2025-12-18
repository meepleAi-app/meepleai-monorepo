using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Service for querying Prometheus metrics API (Issue #2139)
/// </summary>
internal class PrometheusClientService : IPrometheusClientService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PrometheusClientService> _logger;
    private readonly string _prometheusUrl;

    public PrometheusClientService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PrometheusClientService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Default to localhost:9090 if not configured
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
        _prometheusUrl = configuration["Prometheus:Url"] ?? "http://localhost:9090";
#pragma warning restore S1075

        _logger.LogInformation("PrometheusClientService initialized with URL: {PrometheusUrl}", _prometheusUrl);
    }

    public async Task<object?> QueryAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be empty", nameof(query));
        }

        try
        {
            var encodedQuery = Uri.EscapeDataString(query);
            var url = $"{_prometheusUrl}/api/v1/query?query={encodedQuery}";

            _logger.LogDebug("Executing Prometheus query: {Query}", query);

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Prometheus query failed with status {StatusCode}: {Query}",
                    response.StatusCode,
                    query);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<object>(content);

            _logger.LogDebug("Prometheus query successful: {Query}", query);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error querying Prometheus: {Query}", query);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Prometheus query timeout: {Query}", query);
            return null;
        }
#pragma warning disable CA1031
        // Justification: INFRASTRUCTURE SERVICE PATTERN - Graceful degradation
        // Catches all Prometheus API failures. Returns null instead of throwing
        // to allow caller to handle gracefully. Prevents infrastructure failures from propagating.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error querying Prometheus: {Query}", query);
            return null;
        }
#pragma warning restore CA1031
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_prometheusUrl}/-/healthy";
            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

            var isHealthy = response.IsSuccessStatusCode;

            _logger.LogDebug("Prometheus health check: {IsHealthy}", isHealthy);

            return isHealthy;
        }
#pragma warning disable CA1031
        // Justification: INFRASTRUCTURE SERVICE PATTERN - Graceful degradation
        // Catches all health check failures. Returns false instead of throwing
        // to allow monitoring to continue. Non-critical infrastructure check.
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Prometheus health check failed");
            return false;
        }
#pragma warning restore CA1031
    }
}
