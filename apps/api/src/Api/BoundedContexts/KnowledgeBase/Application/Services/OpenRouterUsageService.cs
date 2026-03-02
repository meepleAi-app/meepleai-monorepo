using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Issue #5074: Background service that polls OpenRouter /auth/key every 60 seconds
/// and caches account status and daily spend in Redis for fast reads.
///
/// Redis keys:
///   openrouter:account:status        → JSON OpenRouterAccountStatus (TTL 90s)
///   openrouter:daily_spend:{date}    → decimal string (TTL 25h, INCRBYFLOAT)
///
/// Graceful degradation: all Redis operations are wrapped in try/catch.
/// If Redis is unavailable, GetAccountStatusAsync returns null and GetDailySpendAsync returns 0.
/// </summary>
internal sealed class OpenRouterUsageService : BackgroundService, IOpenRouterUsageService
{
    private const string AccountStatusKey = "openrouter:account:status";
    private const string AuthKeyEndpoint = "auth/key";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan AccountStatusTtl = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan DailySpendTtl = TimeSpan.FromHours(25);

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<OpenRouterUsageService> _logger;
    private readonly string? _apiKey;

    public OpenRouterUsageService(
        IHttpClientFactory httpClientFactory,
        IConnectionMultiplexer redis,
        IConfiguration configuration,
        ILogger<OpenRouterUsageService> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiKey = SecretsHelper.GetSecretOrValue(configuration, "OPENROUTER_API_KEY", logger, required: false);
    }

    // ─── BackgroundService ──────────────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OpenRouterUsageService started — polling every {Interval}s", PollInterval.TotalSeconds);

        // Initial poll without waiting
        await PollAccountStatusAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(PollInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await PollAccountStatusAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    // ─── IOpenRouterUsageService ────────────────────────────────────────────

    public async Task<OpenRouterAccountStatus?> GetAccountStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var raw = await db.StringGetAsync(AccountStatusKey).ConfigureAwait(false);
            if (!raw.HasValue)
                return null;

            return JsonSerializer.Deserialize<OpenRouterAccountStatus>(raw.ToString(), s_jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis read failed for account status (graceful degradation)");
            return null;
        }
    }

    public async Task<decimal> GetDailySpendAsync(CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = DailySpendKey();
            var raw = await db.StringGetAsync(key).ConfigureAwait(false);
            return raw.HasValue && decimal.TryParse(raw.ToString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var val)
                ? val
                : 0m;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis read failed for daily spend (graceful degradation)");
            return 0m;
        }
    }

    public async Task RecordRequestCostAsync(decimal costUsd, CancellationToken ct = default)
    {
        if (costUsd <= 0m)
            return;

        try
        {
            var db = _redis.GetDatabase();
            var key = DailySpendKey();

            // INCRBYFLOAT: atomic increment, safe for concurrent calls
            await db.StringIncrementAsync(key, (double)costUsd).ConfigureAwait(false);

            // Refresh TTL on each write so the key expires 25h after the last request today
            await db.KeyExpireAsync(key, DailySpendTtl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis write failed for daily spend (graceful degradation)");
        }
    }

    // ─── Private helpers ────────────────────────────────────────────────────

    private async Task PollAccountStatusAsync(CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogDebug("OpenRouter API key not configured — skipping /auth/key poll");
                return;
            }

            using var client = _httpClientFactory.CreateClient("OpenRouter");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            using var response = await client.GetAsync(AuthKeyEndpoint, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OpenRouter /auth/key returned {StatusCode} — skipping cache update",
                    (int)response.StatusCode);
                return;
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var apiResponse = JsonSerializer.Deserialize<AuthKeyApiResponse>(json, s_jsonOptions);

            if (apiResponse?.Data is null)
            {
                _logger.LogWarning("OpenRouter /auth/key response missing 'data' field");
                return;
            }

            var status = new OpenRouterAccountStatus
            {
                BalanceUsd = apiResponse.Data.LimitUsd - apiResponse.Data.Usage,
                LimitUsd = apiResponse.Data.LimitUsd,
                UsageUsd = apiResponse.Data.Usage,
                IsFreeTier = apiResponse.Data.IsFreeTier,
                RateLimitRequests = apiResponse.Data.RateLimit?.Requests ?? 0,
                RateLimitInterval = apiResponse.Data.RateLimit?.Interval ?? string.Empty,
                LastUpdated = DateTime.UtcNow
            };

            var db = _redis.GetDatabase();
            var serialized = JsonSerializer.Serialize(status, s_jsonOptions);
            await db.StringSetAsync(AccountStatusKey, serialized, AccountStatusTtl).ConfigureAwait(false);

            _logger.LogDebug(
                "OpenRouter account status cached: Balance=${Balance}, Usage=${Usage}, FreeTier={FreeTier}",
                status.BalanceUsd, status.UsageUsd, status.IsFreeTier);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to poll OpenRouter /auth/key (will retry in {Interval}s)",
                PollInterval.TotalSeconds);
        }
    }

    private static string DailySpendKey()
        => $"openrouter:daily_spend:{DateTime.UtcNow:yyyy-MM-dd}";

    // ─── OpenRouter API response models (internal positional records) ──────────

    // Positional records: constructor assignment is recognized by SonarAnalyzer (avoids S3459/S1144).
    // System.Text.Json supports record deserialization via constructor parameter matching.

    private sealed record AuthKeyApiResponse(AuthKeyData? Data);

    private sealed record AuthKeyData(
        // OpenRouter returns "limit" (not "limit_usd")
        [property: JsonPropertyName("limit")] decimal LimitUsd,
        decimal Usage,
        [property: JsonPropertyName("is_free_tier")] bool IsFreeTier,
        [property: JsonPropertyName("rate_limit")] RateLimitInfo? RateLimit);

    private sealed record RateLimitInfo(int Requests, string Interval);
}
