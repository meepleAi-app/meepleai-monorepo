using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.Services.Providers.Quota;

/// <summary>
/// Fetches DeepSeek balance via GET /user/balance.
/// Response: { is_available: bool, balance_infos: [{ currency, total_balance, granted_balance, topped_up_balance }] }.
/// total_balance = granted + topped_up. Currency normalized to USD (api always reports CNY+USD; we pick USD).
/// </summary>
internal sealed class DeepSeekQuotaProvider : IProviderQuotaProvider
{
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(5);

#pragma warning disable S1075 // URIs should not be hardcoded - Default fallback for DeepSeek public API
    private const string DefaultBalanceUrl = "https://api.deepseek.com/user/balance";
#pragma warning restore S1075

    private readonly HttpClient _httpClient;
    private readonly ILogger<DeepSeekQuotaProvider> _logger;
    private readonly string _balanceUrl;

    public string ProviderName => "deepseek";
    public string ApiKeyEnvVar => "DEEPSEEK_API_KEY";

    public DeepSeekQuotaProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<DeepSeekQuotaProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("provider-probe");
        _logger = logger;
        // DeepSeek balance endpoint is at /user/balance (not /v1/user/balance) on api.deepseek.com.
        // Allow override via Providers:DeepSeek:BalanceUrl for test/staging.
        _balanceUrl = configuration["Providers:DeepSeek:BalanceUrl"] ?? DefaultBalanceUrl;
    }

    public async Task<QuotaFetchResult> FetchAsync(string apiKey, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(FetchTimeout);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, _balanceUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var resp = await _httpClient.SendAsync(req, cts.Token).ConfigureAwait(false);

            if (resp.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                return new QuotaFetchResult(false, null, null, null, null, "unauthorized", "Provider rejected token");

            if (!resp.IsSuccessStatusCode)
                return new QuotaFetchResult(false, null, null, null, null, "http_" + (int)resp.StatusCode, $"HTTP {resp.StatusCode}");

            var payload = await resp.Content.ReadFromJsonAsync<BalanceResponse>(cancellationToken: cts.Token).ConfigureAwait(false);
            if (payload?.BalanceInfos is null || payload.BalanceInfos.Count == 0)
                return new QuotaFetchResult(false, null, null, null, null, "parse_error", "Empty payload");

            // Prefer USD balance; fallback to first entry if no USD found.
            var usd = payload.BalanceInfos.FirstOrDefault(b => string.Equals(b.Currency, "USD", StringComparison.OrdinalIgnoreCase))
                     ?? payload.BalanceInfos[0];

            return new QuotaFetchResult(
                Success: true,
                UsedUsd: null, // DeepSeek doesn't expose lifetime usage, only remaining balance
                LimitUsd: null, // No hard limit; pay-as-you-go
                RemainingUsd: usd.TotalBalance,
                ResetAt: null,
                ErrorCode: null,
                ErrorMessage: null);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new QuotaFetchResult(false, null, null, null, null, "timeout", "Quota fetch exceeded 5s");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "DeepSeek quota fetch network failure");
            return new QuotaFetchResult(false, null, null, null, null, "unreachable", "Network error");
        }
    }

    private sealed record BalanceResponse(
        [property: JsonPropertyName("is_available")] bool IsAvailable,
        [property: JsonPropertyName("balance_infos")] List<BalanceInfo>? BalanceInfos);

    private sealed record BalanceInfo(
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("total_balance")] decimal TotalBalance,
        [property: JsonPropertyName("granted_balance")] decimal GrantedBalance,
        [property: JsonPropertyName("topped_up_balance")] decimal ToppedUpBalance);
}
