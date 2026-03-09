using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Redis-backed budget isolation and rate limiting for A/B testing playground.
/// Issue #5505: Separate daily budget ($5/day default), per-role rate limits, 24h response caching.
/// </summary>
internal sealed class AbTestBudgetService : IAbTestBudgetService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<AbTestBudgetService> _logger;
    private readonly decimal _dailyBudgetUsd;
    private readonly int _editorDailyLimit;
    private readonly int _adminDailyLimit;
    private static readonly TimeSpan ResponseCacheTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan DailyKeyTtl = TimeSpan.FromHours(25); // 25h to cover timezone edges

    private const string BudgetKeyPrefix = "abtest:daily_budget";
    private const string RateLimitKeyPrefix = "abtest:rate_limit";
    private const string ResponseCacheKeyPrefix = "abtest:response_cache";

    public AbTestBudgetService(
        IConnectionMultiplexer redis,
        IConfiguration configuration,
        ILogger<AbTestBudgetService> logger)
    {
        _redis = redis;
        _logger = logger;

        _dailyBudgetUsd = decimal.TryParse(
            configuration["AbTesting:DailyBudgetUsd"], NumberStyles.Number, CultureInfo.InvariantCulture, out var budget) ? budget : 5.0m;
        _editorDailyLimit = int.TryParse(
            configuration["AbTesting:EditorDailyLimit"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var editorLimit) ? editorLimit : 50;
        _adminDailyLimit = int.TryParse(
            configuration["AbTesting:AdminDailyLimit"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var adminLimit) ? adminLimit : 200;
    }

    public async Task<bool> HasBudgetRemainingAsync(CancellationToken ct = default)
    {
        try
        {
            var spend = await GetDailySpendAsync(ct).ConfigureAwait(false);
            return spend < _dailyBudgetUsd;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check A/B test budget, allowing request (fail-open)");
            return true;
        }
    }

    public async Task RecordTestCostAsync(decimal costUsd, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetDailyBudgetKey();
            await db.StringIncrementAsync(key, (double)costUsd).ConfigureAwait(false);
            await db.KeyExpireAsync(key, DailyKeyTtl, CommandFlags.FireAndForget).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record A/B test cost of {Cost:F4} USD", costUsd);
        }
    }

    public async Task<decimal> GetDailySpendAsync(CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(GetDailyBudgetKey()).ConfigureAwait(false);
            return value.HasValue && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var spend) ? spend : 0m;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get A/B test daily spend");
            return 0m;
        }
    }

    public async Task<bool> HasRateLimitRemainingAsync(Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        try
        {
            var count = await GetUserTestCountTodayAsync(userId, ct).ConfigureAwait(false);
            var limit = isAdmin ? _adminDailyLimit : _editorDailyLimit;
            return count < limit;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check A/B test rate limit for user {UserId}, allowing (fail-open)", userId);
            return true;
        }
    }

    public async Task RecordTestExecutionAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetRateLimitKey(userId);
            await db.StringIncrementAsync(key).ConfigureAwait(false);
            await db.KeyExpireAsync(key, DailyKeyTtl, CommandFlags.FireAndForget).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record A/B test execution for user {UserId}", userId);
        }
    }

    public async Task<int> GetUserTestCountTodayAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(GetRateLimitKey(userId)).ConfigureAwait(false);
            return value.HasValue && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get A/B test count for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<string?> GetCachedResponseAsync(string query, string modelId, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetResponseCacheKey(query, modelId);
            var value = await db.StringGetAsync(key).ConfigureAwait(false);
            return value.HasValue ? value.ToString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get cached A/B test response");
            return null;
        }
    }

    public async Task CacheResponseAsync(string query, string modelId, string response, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetResponseCacheKey(query, modelId);
            await db.StringSetAsync(key, response, ResponseCacheTtl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache A/B test response");
        }
    }

    private static string GetDailyBudgetKey()
        => $"{BudgetKeyPrefix}:{DateTime.UtcNow:yyyy-MM-dd}";

    private static string GetRateLimitKey(Guid userId)
        => $"{RateLimitKeyPrefix}:{userId}:{DateTime.UtcNow:yyyy-MM-dd}";

    private static string GetResponseCacheKey(string query, string modelId)
    {
        var input = $"{query.Trim().ToLowerInvariant()}:{modelId.Trim().ToLowerInvariant()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"{ResponseCacheKeyPrefix}:{Convert.ToHexString(hash)[..16]}";
    }
}
