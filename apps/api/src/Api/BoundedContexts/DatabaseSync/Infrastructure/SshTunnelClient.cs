using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DatabaseSync.Infrastructure;

/// <summary>
/// HTTP client that communicates with the SSH tunnel sidecar container.
/// The sidecar exposes /status, /open, /close endpoints to manage the tunnel lifecycle.
/// </summary>
internal sealed class SshTunnelClient : ISshTunnelClient
{
    private readonly HttpClient _httpClient;
    private readonly string _authToken;
    private readonly ILogger<SshTunnelClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public SshTunnelClient(HttpClient httpClient, string authToken, ILogger<SshTunnelClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authToken = authToken ?? throw new ArgumentNullException(nameof(authToken));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TunnelStatusResult> GetStatusAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, "/status");
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var body = await response.Content
                .ReadFromJsonAsync<SidecarStatusResponse>(JsonOptions, ct)
                .ConfigureAwait(false);

            return MapStatus(body!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get tunnel status from sidecar");
            return new TunnelStatusResult(TunnelState.Error, 0, ex.Message);
        }
    }

    public async Task<TunnelStatusResult> OpenAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Post, "/open");
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var body = await response.Content
                .ReadFromJsonAsync<SidecarStatusResponse>(JsonOptions, ct)
                .ConfigureAwait(false);

            return MapStatus(body!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open tunnel via sidecar");
            return new TunnelStatusResult(TunnelState.Error, 0, ex.Message);
        }
    }

    public async Task<TunnelStatusResult> CloseAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Delete, "/close");
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var body = await response.Content
                .ReadFromJsonAsync<SidecarStatusResponse>(JsonOptions, ct)
                .ConfigureAwait(false);

            return MapStatus(body!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to close tunnel via sidecar");
            return new TunnelStatusResult(TunnelState.Error, 0, ex.Message);
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
        return request;
    }

    private static TunnelStatusResult MapStatus(SidecarStatusResponse response)
    {
        var state = response.Status switch
        {
            "closed" => TunnelState.Closed,
            "opening" => TunnelState.Opening,
            "open" => TunnelState.Open,
            _ => TunnelState.Error
        };

        return new TunnelStatusResult(state, response.UptimeSeconds, response.Message);
    }

    /// <summary>
    /// DTO matching the JSON shape returned by the SSH tunnel sidecar.
    /// </summary>
    private sealed record SidecarStatusResponse(
        string Status,
        [property: JsonPropertyName("uptime_seconds")] int UptimeSeconds,
        string? Message
    );
}
