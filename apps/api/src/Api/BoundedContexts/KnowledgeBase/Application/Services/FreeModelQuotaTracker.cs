using System.Globalization;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using StackExchange.Redis;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Redis-backed tracker for free OpenRouter model quota and rate-limit state.
///
/// Issue #5087: Free model quota tracking — RPM vs RPD distinction.
/// Issue #5088: Ollama fallback — RPD flag used by HybridLlmService to force Ollama.
///
/// Redis keys:
///   openrouter:rpd_exhausted:{modelId}:{date-utc} → "true"    TTL 25h
///   openrouter:rate_limit_type:last_error          → "rpd"|"rpm"|... TTL 24h
///   openrouter:rpd_reset_at                        → unix_ms_timestamp TTL 25h
/// </summary>
internal sealed class FreeModelQuotaTracker : IFreeModelQuotaTracker
{
    private static readonly TimeSpan RpdKeyTtl = TimeSpan.FromHours(25); // Outlasts midnight reset
    private static readonly TimeSpan LastErrorTtl = TimeSpan.FromHours(24);

    private const string LastErrorKey = "openrouter:rate_limit_type:last_error";
    private const string RpdResetAtKey = "openrouter:rpd_reset_at";

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<FreeModelQuotaTracker> _logger;

    public FreeModelQuotaTracker(
        IConnectionMultiplexer redis,
        ILogger<FreeModelQuotaTracker> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task RecordRateLimitErrorAsync(
        string modelId,
        RateLimitErrorType errorType,
        long? resetTimestampMs,
        CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var tasks = new List<Task>(3);

            if (errorType == RateLimitErrorType.Rpd)
            {
                // Mark this model as RPD-exhausted for today (25h TTL covers midnight reset)
                tasks.Add(db.StringSetAsync(
                    RpdExhaustedKey(modelId),
                    "true",
                    RpdKeyTtl));

                // Store reset timestamp so the dashboard can display a countdown
                if (resetTimestampMs.HasValue)
                {
                    tasks.Add(db.StringSetAsync(
                        RpdResetAtKey,
                        resetTimestampMs.Value.ToString(CultureInfo.InvariantCulture),
                        RpdKeyTtl));
                }
            }

            // Always record the last error type (RPM or RPD) for dashboard display
            tasks.Add(db.StringSetAsync(
                LastErrorKey,
                errorType.ToString().ToLowerInvariant(),
                LastErrorTtl));

            await Task.WhenAll(tasks).ConfigureAwait(false);

            _logger.LogWarning(
                "Free model quota event recorded: {ErrorType} for {Model} (resetMs={ResetMs})",
                errorType, modelId, resetTimestampMs);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis write failed for FreeModelQuotaTracker (graceful degradation)");
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsRpdExhaustedAsync(string modelId, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var val = await db.StringGetAsync(RpdExhaustedKey(modelId)).ConfigureAwait(false);
            return val == "true";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis read failed for RPD exhausted check (graceful degradation)");
            return false; // Assume not exhausted on Redis failure
        }
    }

    /// <inheritdoc/>
    public async Task<RateLimitErrorType?> GetLastErrorTypeAsync(CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var raw = await db.StringGetAsync(LastErrorKey).ConfigureAwait(false);
            if (!raw.HasValue)
                return null;

            return Enum.TryParse<RateLimitErrorType>(raw.ToString(), ignoreCase: true, out var result)
                ? result
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis read failed for last error type (graceful degradation)");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<DateTime?> GetRpdResetTimeAsync(CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var raw = await db.StringGetAsync(RpdResetAtKey).ConfigureAwait(false);
            if (!raw.HasValue)
                return null;

            if (long.TryParse(raw.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var ms))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis read failed for RPD reset time (graceful degradation)");
            return null;
        }
    }

    // ─── Private helpers ────────────────────────────────────────────────────

    private static string RpdExhaustedKey(string modelId) =>
        $"openrouter:rpd_exhausted:{modelId}:{DateTime.UtcNow:yyyyMMdd}";
}
