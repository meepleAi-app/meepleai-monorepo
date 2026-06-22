using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.Administration.Infrastructure.External;

/// <summary>
/// Default <see cref="IPrometheusLabelsClient"/> implementation backed by
/// <see cref="HttpClient"/>. Configured base URL is taken from the same
/// <see cref="PrometheusOptions"/> section used by <see cref="PrometheusHttpClient"/>.
/// </summary>
internal sealed class PrometheusLabelsClient : IPrometheusLabelsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<PrometheusLabelsClient> _logger;

    public PrometheusLabelsClient(
        HttpClient httpClient,
        IOptions<PrometheusOptions> options,
        ILogger<PrometheusLabelsClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _logger = logger;

        var promOptions = options.Value;
        if (!string.IsNullOrEmpty(promOptions.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(promOptions.BaseUrl);
        }
        _httpClient.Timeout = TimeSpan.FromSeconds(promOptions.TimeoutSeconds);
    }

    public async Task<IReadOnlyList<string>?> GetMetricNamesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient
                .GetAsync("/api/v1/label/__name__/values", cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Prometheus labels endpoint returned {Status}",
                    (int)response.StatusCode);
                return null;
            }

            var content = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            var parsed = JsonSerializer.Deserialize<PrometheusLabelsResponse>(content, JsonOptions);

            if (parsed is null
                || !string.Equals(parsed.Status, "success", StringComparison.Ordinal)
                || parsed.Data is null)
            {
                _logger.LogWarning(
                    "Prometheus labels endpoint returned unexpected payload (status={Status})",
                    parsed?.Status ?? "<null>");
                return null;
            }

            return parsed.Data;
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Prometheus labels endpoint unreachable");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Prometheus labels endpoint returned malformed JSON");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            // Timeout (not user cancellation)
            _logger.LogWarning(ex, "Prometheus labels endpoint request timed out");
            return null;
        }
    }

    private sealed record PrometheusLabelsResponse(string? Status, List<string>? Data);
}
