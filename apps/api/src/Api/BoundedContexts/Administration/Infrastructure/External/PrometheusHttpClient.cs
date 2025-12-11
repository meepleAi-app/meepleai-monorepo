using System.Text.Json;
using Api.BoundedContexts.Administration.Domain.Services;
using Microsoft.Extensions.Options;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.Administration.Infrastructure.External;

/// <summary>
/// HTTP client for querying Prometheus via HTTP API.
/// Issue #893: Implements PromQL range queries and instant queries.
/// </summary>
public class PrometheusHttpClient : IPrometheusQueryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PrometheusHttpClient> _logger;

    public PrometheusHttpClient(
        HttpClient httpClient,
        IOptions<PrometheusOptions> options,
        ILogger<PrometheusHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        var prometheusOptions = options.Value;

        // Configure base URL from options
        if (!string.IsNullOrEmpty(prometheusOptions.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(prometheusOptions.BaseUrl);
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(prometheusOptions.TimeoutSeconds);
    }

    public async Task<PrometheusQueryResult> QueryRangeAsync(
        string query,
        DateTime start,
        DateTime end,
        string step,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(step);
        ValidateStepFormat(step);

        if (end <= start)
        {
            throw new ArgumentException("End time must be after start time", nameof(end));
        }

        _logger.LogInformation(
            "Executing Prometheus range query: {Query}, Start: {Start}, End: {End}, Step: {Step}",
            query, start, end, step);

        try
        {
            var startUnix = new DateTimeOffset(start).ToUnixTimeSeconds();
            var endUnix = new DateTimeOffset(end).ToUnixTimeSeconds();

            var url = $"/api/v1/query_range?query={Uri.EscapeDataString(query)}&start={startUnix}&end={endUnix}&step={step}";

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var prometheusResponse = JsonSerializer.Deserialize<PrometheusApiResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (!string.Equals(prometheusResponse?.Status, "success", StringComparison.Ordinal))
            {
                var errorMsg = prometheusResponse?.Error ?? "Unknown error from Prometheus";
                _logger.LogError("Prometheus query failed: {Error}", errorMsg);
                throw new InvalidOperationException($"Prometheus query failed: {errorMsg}");
            }

            return ParseQueryResult(prometheusResponse.Data);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to Prometheus failed");
            throw new InvalidOperationException("Failed to connect to Prometheus", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Prometheus response");
            throw new InvalidOperationException("Failed to parse Prometheus response", ex);
        }
    }

    public async Task<PrometheusQueryResult> QueryInstantAsync(
        string query,
        DateTime? time = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var timestamp = time ?? DateTime.UtcNow;

        _logger.LogInformation(
            "Executing Prometheus instant query: {Query}, Time: {Time}",
            query, timestamp);

        try
        {
            var timeUnix = new DateTimeOffset(timestamp).ToUnixTimeSeconds();
            var url = $"/api/v1/query?query={Uri.EscapeDataString(query)}&time={timeUnix}";

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var prometheusResponse = JsonSerializer.Deserialize<PrometheusApiResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (!string.Equals(prometheusResponse?.Status, "success", StringComparison.Ordinal))
            {
                var errorMsg = prometheusResponse?.Error ?? "Unknown error from Prometheus";
                _logger.LogError("Prometheus query failed: {Error}", errorMsg);
                throw new InvalidOperationException($"Prometheus query failed: {errorMsg}");
            }

            return ParseQueryResult(prometheusResponse.Data);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to Prometheus failed");
            throw new InvalidOperationException("Failed to connect to Prometheus", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Prometheus response");
            throw new InvalidOperationException("Failed to parse Prometheus response", ex);
        }
    }

    private static void ValidateStepFormat(string step)
    {
        // Prometheus step format: number + unit (s=seconds, m=minutes, h=hours, d=days, w=weeks, y=years)
        // Examples: "5m", "1h", "30s", "1d"
        if (!System.Text.RegularExpressions.Regex.IsMatch(step, @"^\d+[smhdwy]$"))
        {
            throw new ArgumentException(
                "Step must be in Prometheus duration format (e.g., '5m', '1h', '30s', '1d')",
                nameof(step));
        }
    }

    private PrometheusQueryResult ParseQueryResult(PrometheusDataResponse? data)
    {
        if (data?.Result is null)
        {
            return new PrometheusQueryResult("matrix", Array.Empty<PrometheusTimeSeries>());
        }

        var timeSeries = data.Result.Select(result =>
        {
            var metric = result.Metric ?? new Dictionary<string, string>(StringComparer.Ordinal);

            // Parse values (for range queries) or value (for instant queries)
            var dataPoints = new List<PrometheusDataPoint>();

            if (result.Values is JsonElement valuesElement && valuesElement.ValueKind == JsonValueKind.Array)
            {
                // Range query response: array of [timestamp, value] arrays
                foreach (var valueArray in valuesElement.EnumerateArray())
                {
                    if (valueArray.ValueKind == JsonValueKind.Array)
                    {
                        var array = valueArray.EnumerateArray().ToArray();
                        if (array.Length >= 2)
                        {
                            var timestamp = DateTimeOffset.FromUnixTimeSeconds((long)array[0].GetDouble()).UtcDateTime;
                            var value = array[1].ValueKind == JsonValueKind.String
                                ? double.Parse(array[1].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture)
                                : array[1].GetDouble();
                            dataPoints.Add(new PrometheusDataPoint(timestamp, value));
                        }
                    }
                }
            }
            else if (result.Value is not null)
            {
                // Instant query response: single [timestamp, value] array
                if (result.Value is JsonElement element && element.ValueKind == JsonValueKind.Array)
                {
                    var array = element.EnumerateArray().ToArray();
                    if (array.Length >= 2)
                    {
                        var timestamp = DateTimeOffset.FromUnixTimeSeconds((long)array[0].GetDouble()).UtcDateTime;
                        var value = array[1].ValueKind == JsonValueKind.String
                            ? double.Parse(array[1].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture)
                            : array[1].GetDouble();
                        dataPoints.Add(new PrometheusDataPoint(timestamp, value));
                    }
                }
            }

            return new PrometheusTimeSeries(metric, dataPoints);
        }).ToList();

        return new PrometheusQueryResult(data.ResultType ?? "matrix", timeSeries);
    }
}

/// <summary>
/// Configuration options for Prometheus client.
/// </summary>
public class PrometheusOptions
{
    public const string SectionName = "Prometheus";

    /// <summary>
    /// Base URL of Prometheus server (e.g., "http://prometheus:9090")
    /// </summary>
    public string BaseUrl { get; set; } = "http://prometheus:9090";

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

// Internal DTOs for JSON deserialization from Prometheus API

internal record PrometheusApiResponse
{
    public string? Status { get; init; }
    public string? Error { get; init; }
    public PrometheusDataResponse? Data { get; init; }
}

internal record PrometheusDataResponse
{
    public string? ResultType { get; init; }
    public IList<PrometheusResultItem>? Result { get; init; }
}

internal record PrometheusResultItem
{
    public IDictionary<string, string>? Metric { get; init; }
    public object? Value { get; init; }  // For instant queries: [timestamp, value]
    public object? Values { get; init; } // For range queries: [[timestamp, value], ...]
}