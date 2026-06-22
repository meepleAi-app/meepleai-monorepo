using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.Services.Providers.Quota;

/// <summary>
/// Fetches OpenRouter credit usage via GET /api/v1/auth/key.
/// Response: { data: { usage: number, limit: number|null, is_free_tier: bool } }.
/// All values in USD.
/// </summary>
internal sealed class OpenRouterQuotaProvider : IProviderQuotaProvider
{
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(5);

#pragma warning disable S1075 // URIs should not be hardcoded - Default fallback for OpenRouter public API
    private const string DefaultAuthKeyUrl = "https://openrouter.ai/api/v1/auth/key";
#pragma warning restore S1075

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouterQuotaProvider> _logger;
    private readonly string _authKeyUrl;

    public string ProviderName => "openrouter";
    public string ApiKeyEnvVar => "OPENROUTER_API_KEY";

    public OpenRouterQuotaProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenRouterQuotaProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("provider-probe");
        _logger = logger;
        var baseUrl = configuration["Providers:OpenRouter:BaseUrl"]?.TrimEnd('/');
        _authKeyUrl = baseUrl is not null ? $"{baseUrl}/auth/key" : DefaultAuthKeyUrl;
    }

    public async Task<QuotaFetchResult> FetchAsync(string apiKey, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(FetchTimeout);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, _authKeyUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var resp = await _httpClient.SendAsync(req, cts.Token).ConfigureAwait(false);

            if (resp.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                return new QuotaFetchResult(false, null, null, null, null, "unauthorized", "Provider rejected token");

            if (!resp.IsSuccessStatusCode)
                return new QuotaFetchResult(false, null, null, null, null, "http_" + (int)resp.StatusCode, $"HTTP {resp.StatusCode}");

            var payload = await resp.Content.ReadFromJsonAsync<AuthKeyResponse>(cancellationToken: cts.Token).ConfigureAwait(false);
            if (payload?.Data is null)
                return new QuotaFetchResult(false, null, null, null, null, "parse_error", "Empty payload");

            var used = payload.Data.Usage;
            var limit = payload.Data.Limit;
            var remaining = limit.HasValue ? Math.Max(0m, limit.Value - used) : (decimal?)null;

            return new QuotaFetchResult(
                Success: true,
                UsedUsd: used,
                LimitUsd: limit,
                RemainingUsd: remaining,
                ResetAt: null, // OpenRouter does not expose a reset time
                ErrorCode: null,
                ErrorMessage: null);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new QuotaFetchResult(false, null, null, null, null, "timeout", "Quota fetch exceeded 5s");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "OpenRouter quota fetch network failure");
            return new QuotaFetchResult(false, null, null, null, null, "unreachable", "Network error");
        }
    }

    private sealed record AuthKeyResponse([property: JsonPropertyName("data")] AuthKeyData? Data);
    private sealed record AuthKeyData(
        [property: JsonPropertyName("usage")] decimal Usage,
        [property: JsonPropertyName("limit")] decimal? Limit);
}
