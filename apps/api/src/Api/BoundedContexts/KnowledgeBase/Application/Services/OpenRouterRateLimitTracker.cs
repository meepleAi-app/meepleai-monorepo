using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Issue #5075: Redis sliding-window tracker for OpenRouter RPM and TPM.
///
/// Redis keys per provider:
///   openrouter:rpm:{provider}  → Sorted Set (score=timestamp_ms, member=request_id)  TTL 120s
///   openrouter:tpm:{provider}  → Sorted Set (score=timestamp_ms, member="{req_id}:{tokens}") TTL 120s
///
/// Algorithm (ZADD + ZREMRANGEBYSCORE + ZCARD):
///   1. ZADD key score member        — add current request
///   2. ZREMRANGEBYSCORE key 0 now-60000  — evict records older than 60s
///   3. ZCARD key                    — count requests in window = current RPM
///
/// Graceful degradation: all Redis calls wrapped in try/catch, returns zeros on failure.
/// </summary>
internal sealed class OpenRouterRateLimitTracker : IOpenRouterRateLimitTracker
{
    private static readonly TimeSpan WindowSize = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan KeyTtl = TimeSpan.FromSeconds(120);

    private readonly IConnectionMultiplexer _redis;
    private readonly IOpenRouterUsageService _usageService;
    private readonly ILogger<OpenRouterRateLimitTracker> _logger;

    public OpenRouterRateLimitTracker(
        IConnectionMultiplexer redis,
        IOpenRouterUsageService usageService,
        ILogger<OpenRouterRateLimitTracker> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _usageService = usageService ?? throw new ArgumentNullException(nameof(usageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ─── IOpenRouterRateLimitTracker ────────────────────────────────────────

    public async Task RecordRequestAsync(string provider, string modelId, int totalTokens, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var requestId = Guid.NewGuid().ToString("N");
            var cutoff = nowMs - (long)WindowSize.TotalMilliseconds;

            var rpmKey = RpmKey(provider);
            var tpmKey = TpmKey(provider);

            var batch = db.CreateBatch();

            // RPM: add + evict old + refresh TTL
            var rpmAddTask = batch.SortedSetAddAsync(rpmKey, requestId, nowMs);
            var rpmEvictTask = batch.SortedSetRemoveRangeByScoreAsync(rpmKey, double.NegativeInfinity, cutoff);
            var rpmTtlTask = batch.KeyExpireAsync(rpmKey, KeyTtl);

            // TPM: add with token count encoded in member, evict old, refresh TTL
            if (totalTokens > 0)
            {
                var tpmMember = $"{requestId}:{totalTokens}";
                var tpmAddTask = batch.SortedSetAddAsync(tpmKey, tpmMember, nowMs);
                var tpmEvictTask = batch.SortedSetRemoveRangeByScoreAsync(tpmKey, double.NegativeInfinity, cutoff);
                var tpmTtlTask = batch.KeyExpireAsync(tpmKey, KeyTtl);

                batch.Execute();
                await Task.WhenAll(rpmAddTask, rpmEvictTask, rpmTtlTask, tpmAddTask, tpmEvictTask, tpmTtlTask)
                    .ConfigureAwait(false);
            }
            else
            {
                batch.Execute();
                await Task.WhenAll(rpmAddTask, rpmEvictTask, rpmTtlTask).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis write failed for RPM/TPM tracker (graceful degradation)");
        }
    }

    public async Task<RateLimitStatus> GetCurrentStatusAsync(string provider, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var cutoff = nowMs - (long)WindowSize.TotalMilliseconds;

            var rpmKey = RpmKey(provider);
            var tpmKey = TpmKey(provider);

            // Evict stale entries, then count
            await db.SortedSetRemoveRangeByScoreAsync(rpmKey, double.NegativeInfinity, cutoff).ConfigureAwait(false);
            await db.SortedSetRemoveRangeByScoreAsync(tpmKey, double.NegativeInfinity, cutoff).ConfigureAwait(false);

            var currentRpm = (int)await db.SortedSetLengthAsync(rpmKey).ConfigureAwait(false);
            var tpmEntries = await db.SortedSetRangeByScoreAsync(tpmKey, cutoff, double.PositiveInfinity).ConfigureAwait(false);

            var currentTpm = SumTokensFromEntries(tpmEntries);

            // Get limit from usage service cache
            var accountStatus = await _usageService.GetAccountStatusAsync(ct).ConfigureAwait(false);
            var limitRpm = accountStatus?.RateLimitRequests ?? 0;

            var utilization = limitRpm > 0 ? (double)currentRpm / limitRpm : 0.0;

            return new RateLimitStatus
            {
                CurrentRpm = currentRpm,
                LimitRpm = limitRpm,
                CurrentTpm = currentTpm,
                LimitTpm = 0, // TPM limit not exposed by OpenRouter /auth/key
                UtilizationPercent = utilization,
                IsThrottled = limitRpm > 0 && currentRpm >= limitRpm
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis read failed for RPM/TPM status (graceful degradation)");
            return new RateLimitStatus();
        }
    }

    public async Task<bool> IsApproachingLimitAsync(string provider, int thresholdPercent = 80, CancellationToken ct = default)
    {
        var status = await GetCurrentStatusAsync(provider, ct).ConfigureAwait(false);
        if (status.LimitRpm <= 0)
            return false;

        return status.UtilizationPercent * 100 >= thresholdPercent;
    }

    // ─── Private helpers ────────────────────────────────────────────────────

    private static string RpmKey(string provider) => $"openrouter:rpm:{provider}";
    private static string TpmKey(string provider) => $"openrouter:tpm:{provider}";

    private static int SumTokensFromEntries(RedisValue[] entries)
    {
        var total = 0;
        foreach (var entry in entries)
        {
            if (entry.HasValue)
            {
                var s = entry.ToString();
                var colonIdx = s.LastIndexOf(':');
                if (colonIdx >= 0 && int.TryParse(s.AsSpan(colonIdx + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var tokens))
                    total += tokens;
            }
        }

        return total;
    }
}
