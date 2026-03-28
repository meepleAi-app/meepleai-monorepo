using System.Text.Json;
using Api.BoundedContexts.Administration.Application.DTOs;

namespace Api.BoundedContexts.Administration.Infrastructure.External;

public interface ISeqQueryClient
{
    Task<(IReadOnlyList<ApplicationLogDto> Items, int? RemainingCount)> QueryEventsAsync(
        string? filter, string? level, DateTime? fromUtc, DateTime? toUtc,
        int count, string? afterId, CancellationToken ct);
}

public sealed class SeqQueryClient : ISeqQueryClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SeqQueryClient> _logger;

    public SeqQueryClient(HttpClient httpClient, ILogger<SeqQueryClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(IReadOnlyList<ApplicationLogDto> Items, int? RemainingCount)> QueryEventsAsync(
        string? filter, string? level, DateTime? fromUtc, DateTime? toUtc,
        int count, string? afterId, CancellationToken ct)
    {
        var queryParams = new List<string> { $"count={count}" };

        var filterParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(level))
            filterParts.Add($"@Level = '{level}'");
        if (!string.IsNullOrWhiteSpace(filter))
            filterParts.Add($"@Message like '%{EscapeSeqFilter(filter)}%' or @Exception like '%{EscapeSeqFilter(filter)}%'");

        if (filterParts.Count > 0)
            queryParams.Add($"filter={Uri.EscapeDataString(string.Join(" and ", filterParts))}");
        if (fromUtc.HasValue)
            queryParams.Add($"fromDateUtc={fromUtc.Value:O}");
        if (toUtc.HasValue)
            queryParams.Add($"toDateUtc={toUtc.Value:O}");
        if (!string.IsNullOrWhiteSpace(afterId))
            queryParams.Add($"afterId={afterId}");

        var url = $"/api/events?{string.Join("&", queryParams)}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var items = new List<ApplicationLogDto>();
            foreach (var evt in doc.RootElement.EnumerateArray())
            {
                items.Add(ParseEvent(evt));
            }

            int? remaining = null;
            if (response.Headers.TryGetValues("X-Seq-RemainingCount", out var vals)
                && int.TryParse(vals.FirstOrDefault(), System.Globalization.CultureInfo.InvariantCulture, out var rem))
            {
                remaining = rem;
            }

            return (items, remaining);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Seq events at {Url}", url);
            return (Array.Empty<ApplicationLogDto>(), null);
        }
    }

    private static ApplicationLogDto ParseEvent(JsonElement evt)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (evt.TryGetProperty("Properties", out var props))
        {
            foreach (var prop in props.EnumerateObject())
            {
                properties[prop.Name] = prop.Value.ToString();
            }
        }

        return new ApplicationLogDto(
            Id: evt.GetProperty("Id").GetString() ?? "",
            Timestamp: evt.GetProperty("Timestamp").GetDateTime(),
            Level: evt.TryGetProperty("Level", out var lvl) ? lvl.GetString() ?? "Information" : "Information",
            Message: evt.TryGetProperty("RenderedMessage", out var msg) ? msg.GetString() ?? "" : "",
            Source: properties.GetValueOrDefault("SourceContext"),
            CorrelationId: properties.GetValueOrDefault("CorrelationId"),
            Exception: evt.TryGetProperty("Exception", out var ex) ? ex.GetString() : null,
            Properties: properties.Count > 0 ? properties : null);
    }

    private static string EscapeSeqFilter(string input) => input.Replace("'", "''");
}
