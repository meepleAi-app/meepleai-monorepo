using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Configuration;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.SystemConfiguration.Application.Services;

/// <summary>
/// Issue #5498: DB-first config provider with in-memory cache.
/// Reads LlmSystemConfig from database; falls back to AiProviderSettings (appsettings.json).
/// Cache TTL: 60 seconds — changes take effect within one minute without redeploy.
/// </summary>
internal sealed class LlmSystemConfigProvider : ILlmSystemConfigProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<AiProviderSettings> _aiSettings;
    private readonly ILogger<LlmSystemConfigProvider> _logger;

    private LlmSystemConfig? _cachedConfig;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly System.Threading.Lock _cacheLock = new();

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private const decimal DefaultDailyBudgetUsd = 10.00m;
    private const decimal DefaultMonthlyBudgetUsd = 100.00m;

    public LlmSystemConfigProvider(
        IServiceScopeFactory scopeFactory,
        IOptions<AiProviderSettings> aiSettings,
        ILogger<LlmSystemConfigProvider> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _aiSettings = aiSettings ?? throw new ArgumentNullException(nameof(aiSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> GetCircuitBreakerFailureThresholdAsync(CancellationToken ct = default)
    {
        var config = await GetCachedConfigAsync(ct).ConfigureAwait(false);
        return config?.CircuitBreakerFailureThreshold ?? _aiSettings.Value.CircuitBreaker.FailureThreshold;
    }

    public async Task<int> GetCircuitBreakerOpenDurationSecondsAsync(CancellationToken ct = default)
    {
        var config = await GetCachedConfigAsync(ct).ConfigureAwait(false);
        return config?.CircuitBreakerOpenDurationSeconds ?? _aiSettings.Value.CircuitBreaker.OpenDurationSeconds;
    }

    public async Task<int> GetCircuitBreakerSuccessThresholdAsync(CancellationToken ct = default)
    {
        var config = await GetCachedConfigAsync(ct).ConfigureAwait(false);
        return config?.CircuitBreakerSuccessThreshold ?? _aiSettings.Value.CircuitBreaker.SuccessThreshold;
    }

    public async Task<decimal> GetDailyBudgetUsdAsync(CancellationToken ct = default)
    {
        var config = await GetCachedConfigAsync(ct).ConfigureAwait(false);
        return config?.DailyBudgetUsd ?? DefaultDailyBudgetUsd;
    }

    public async Task<decimal> GetMonthlyBudgetUsdAsync(CancellationToken ct = default)
    {
        var config = await GetCachedConfigAsync(ct).ConfigureAwait(false);
        return config?.MonthlyBudgetUsd ?? DefaultMonthlyBudgetUsd;
    }

    public (int FailureThreshold, int OpenDurationSeconds, int SuccessThreshold) GetCircuitBreakerThresholdsSnapshot()
    {
        // Read from cache synchronously — no DB call, safe for non-async contexts
        lock (_cacheLock)
        {
            if (_cachedConfig != null && DateTime.UtcNow < _cacheExpiry)
            {
                return (
                    _cachedConfig.CircuitBreakerFailureThreshold,
                    _cachedConfig.CircuitBreakerOpenDurationSeconds,
                    _cachedConfig.CircuitBreakerSuccessThreshold);
            }
        }

        // Cache not populated yet — return appsettings defaults
        var cb = _aiSettings.Value.CircuitBreaker;
        return (cb.FailureThreshold, cb.OpenDurationSeconds, cb.SuccessThreshold);
    }

    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cachedConfig = null;
            _cacheExpiry = DateTime.MinValue;
        }

        _logger.LogInformation("LLM system config cache invalidated");
    }

    private async Task<LlmSystemConfig?> GetCachedConfigAsync(CancellationToken ct)
    {
        // Fast path: check cache under lock
        lock (_cacheLock)
        {
            if (DateTime.UtcNow < _cacheExpiry)
                return _cachedConfig;
        }

        // Cache miss or expired — fetch from DB
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ILlmSystemConfigRepository>();
            var config = await repo.GetCurrentAsync(ct).ConfigureAwait(false);

            lock (_cacheLock)
            {
                // Double-check: another thread may have already refreshed the cache
                if (DateTime.UtcNow >= _cacheExpiry)
                {
                    _cachedConfig = config;
                    _cacheExpiry = DateTime.UtcNow.Add(CacheTtl);
                }
                else
                {
                    return _cachedConfig; // Another thread won the race
                }
            }

            if (config != null)
            {
                _logger.LogDebug(
                    "LLM system config loaded from DB (CB: {Failure}/{Open}s/{Success}, Budget: ${Daily}/{Monthly})",
                    config.CircuitBreakerFailureThreshold, config.CircuitBreakerOpenDurationSeconds,
                    config.CircuitBreakerSuccessThreshold, config.DailyBudgetUsd, config.MonthlyBudgetUsd);
            }

            return config;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load LLM system config from DB, using appsettings defaults");

            // Set cache expiry to prevent hammering DB on repeated failures
            lock (_cacheLock)
            {
                if (DateTime.UtcNow >= _cacheExpiry)
                {
                    _cachedConfig = null;
                    _cacheExpiry = DateTime.UtcNow.Add(CacheTtl);
                }
            }

            return null;
        }
#pragma warning restore CA1031
    }
}
