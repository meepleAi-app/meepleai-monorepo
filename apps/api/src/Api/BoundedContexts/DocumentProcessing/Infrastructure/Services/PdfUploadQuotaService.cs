using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.Services;
using StackExchange.Redis;
using AuthRole = Api.BoundedContexts.Authentication.Domain.ValueObjects.Role;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Redis-based implementation of PDF upload quota service.
/// Tracks daily and weekly upload counts with automatic TTL-based reset.
/// </summary>
public class PdfUploadQuotaService : IPdfUploadQuotaService
{
    #region Constants

    /// <summary>
    /// Default upload quotas for each tier.
    /// </summary>
    private static class DefaultQuotas
    {
        // Free Tier
        public const int FreeDailyLimit = 5;
        public const int FreeWeeklyLimit = 20;

        // Normal Tier
        public const int NormalDailyLimit = 20;
        public const int NormalWeeklyLimit = 100;

        // Premium Tier
        public const int PremiumDailyLimit = 100;
        public const int PremiumWeeklyLimit = 500;
    }

    #endregion
    private readonly IConnectionMultiplexer _redis;
    private readonly IConfigurationService _configService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PdfUploadQuotaService> _logger;

    public PdfUploadQuotaService(
        IConnectionMultiplexer redis,
        IConfigurationService configService,
        ILogger<PdfUploadQuotaService> logger,
        TimeProvider? timeProvider = null)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<PdfUploadQuotaResult> CheckQuotaAsync(
        Guid userId,
        UserTier userTier,
        AuthRole userRole,
        CancellationToken ct = default)
    {
        // Admin and Editor bypass quota checks (unlimited)
        if (userRole.IsAdmin() || userRole.IsEditor())
        {
            return PdfUploadQuotaResult.Success(0, int.MaxValue, 0, int.MaxValue, DateTime.MaxValue, DateTime.MaxValue);
        }

        try
        {
            var (dailyLimit, weeklyLimit) = await GetLimitsForTierAsync(userTier, ct).ConfigureAwait(false);
            var (dailyUsed, weeklyUsed) = await GetUsageAsync(userId, ct).ConfigureAwait(false);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var dailyReset = GetNextDailyReset(now);
            var weeklyReset = GetNextWeeklyReset(now);

            // Check daily limit
            if (dailyUsed >= dailyLimit)
            {
                var hoursUntilReset = (dailyReset - now).TotalHours;
                return PdfUploadQuotaResult.Denied(
                    $"Daily upload limit reached ({dailyLimit} PDF/day for {userTier} tier). Limit resets in {hoursUntilReset:F1} hours.",
                    dailyUsed,
                    dailyLimit,
                    weeklyUsed,
                    weeklyLimit,
                    dailyReset,
                    weeklyReset);
            }

            // Check weekly limit
            if (weeklyUsed >= weeklyLimit)
            {
                var daysUntilReset = (weeklyReset - now).TotalDays;
                return PdfUploadQuotaResult.Denied(
                    $"Weekly upload limit reached ({weeklyLimit} PDF/week for {userTier} tier). Limit resets in {daysUntilReset:F1} days.",
                    dailyUsed,
                    dailyLimit,
                    weeklyUsed,
                    weeklyLimit,
                    dailyReset,
                    weeklyReset);
            }

            return PdfUploadQuotaResult.Success(
                dailyUsed,
                dailyLimit,
                weeklyUsed,
                weeklyLimit,
                dailyReset,
                weeklyReset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking PDF upload quota for user {UserId}", userId);
            // Fail-open: allow upload if quota check fails (prioritize availability)
            return PdfUploadQuotaResult.Success(0, int.MaxValue, 0, int.MaxValue, DateTime.MaxValue, DateTime.MaxValue);
        }
    }

    public async Task IncrementUploadCountAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var dailyKey = $"pdf:upload:daily:{userId}:{GetDateKey(now)}";
            var weeklyKey = $"pdf:upload:weekly:{userId}:{GetWeekKey(now)}";

            var dailyTtl = TimeSpan.FromHours(25); // 24h + 1h buffer
            var weeklyTtl = TimeSpan.FromDays(8); // 7 days + 1 day buffer

            // Use Lua script for atomic increment + TTL operation
            // This prevents race condition where process could crash between INCR and EXPIRE
            var script = @"
                local key = KEYS[1]
                local ttl = tonumber(ARGV[1])
                local count = redis.call('INCR', key)
                redis.call('EXPIRE', key, ttl)
                return count
            ";

            var dailyKeys = new RedisKey[] { dailyKey };
            var dailyValues = new RedisValue[] { (int)dailyTtl.TotalSeconds };
            await db.ScriptEvaluateAsync(script, dailyKeys, dailyValues).ConfigureAwait(false);

            var weeklyKeys = new RedisKey[] { weeklyKey };
            var weeklyValues = new RedisValue[] { (int)weeklyTtl.TotalSeconds };
            await db.ScriptEvaluateAsync(script, weeklyKeys, weeklyValues).ConfigureAwait(false);

            _logger.LogDebug("Incremented PDF upload count for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to increment upload count for user {UserId}", userId);
            // Non-critical error, continue without throwing
        }
    }

    public async Task<PdfUploadQuotaInfo> GetQuotaInfoAsync(
        Guid userId,
        UserTier userTier,
        AuthRole userRole,
        CancellationToken ct = default)
    {
        // Admin and Editor have unlimited quota
        if (userRole.IsAdmin() || userRole.IsEditor())
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            return new PdfUploadQuotaInfo
            {
                DailyUploadsUsed = 0,
                DailyLimit = int.MaxValue,
                DailyRemaining = int.MaxValue,
                WeeklyUploadsUsed = 0,
                WeeklyLimit = int.MaxValue,
                WeeklyRemaining = int.MaxValue,
                DailyResetAt = DateTime.MaxValue,
                WeeklyResetAt = DateTime.MaxValue,
                IsUnlimited = true
            };
        }

        try
        {
            var (dailyLimit, weeklyLimit) = await GetLimitsForTierAsync(userTier, ct).ConfigureAwait(false);
            var (dailyUsed, weeklyUsed) = await GetUsageAsync(userId, ct).ConfigureAwait(false);

            var currentTime = _timeProvider.GetUtcNow().UtcDateTime;
            var dailyReset = GetNextDailyReset(currentTime);
            var weeklyReset = GetNextWeeklyReset(currentTime);

            return new PdfUploadQuotaInfo
            {
                DailyUploadsUsed = dailyUsed,
                DailyLimit = dailyLimit,
                DailyRemaining = Math.Max(0, dailyLimit - dailyUsed),
                WeeklyUploadsUsed = weeklyUsed,
                WeeklyLimit = weeklyLimit,
                WeeklyRemaining = Math.Max(0, weeklyLimit - weeklyUsed),
                DailyResetAt = dailyReset,
                WeeklyResetAt = weeklyReset,
                IsUnlimited = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quota info for user {UserId}", userId);
            throw;
        }
    }

    #region Private Helper Methods

    private async Task<(int dailyLimit, int weeklyLimit)> GetLimitsForTierAsync(UserTier tier, CancellationToken ct)
    {
        var tierValue = tier.Value;
        var dailyKey = $"UploadLimits:{tierValue}:DailyLimit";
        var weeklyKey = $"UploadLimits:{tierValue}:WeeklyLimit";

        // Get from configuration service (with fallback to defaults)
        var dailyLimit = await _configService.GetValueAsync<int?>(dailyKey, defaultValue: null).ConfigureAwait(false);
        var weeklyLimit = await _configService.GetValueAsync<int?>(weeklyKey, defaultValue: null).ConfigureAwait(false);

        // Default limits if not configured
        var defaultLimits = GetDefaultLimits(tier);

        return (
            dailyLimit ?? defaultLimits.dailyLimit,
            weeklyLimit ?? defaultLimits.weeklyLimit
        );
    }

    private async Task<(int dailyUsed, int weeklyUsed)> GetUsageAsync(Guid userId, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var dailyKey = $"pdf:upload:daily:{userId}:{GetDateKey(now)}";
        var weeklyKey = $"pdf:upload:weekly:{userId}:{GetWeekKey(now)}";

        var dailyValue = await db.StringGetAsync(dailyKey).ConfigureAwait(false);
        var weeklyValue = await db.StringGetAsync(weeklyKey).ConfigureAwait(false);

        var dailyUsed = dailyValue.HasValue ? (int)dailyValue : 0;
        var weeklyUsed = weeklyValue.HasValue ? (int)weeklyValue : 0;

        return (dailyUsed, weeklyUsed);
    }

    private static (int dailyLimit, int weeklyLimit) GetDefaultLimits(UserTier tier)
    {
        return tier.Value switch
        {
            "free" => (DefaultQuotas.FreeDailyLimit, DefaultQuotas.FreeWeeklyLimit),
            "normal" => (DefaultQuotas.NormalDailyLimit, DefaultQuotas.NormalWeeklyLimit),
            "premium" => (DefaultQuotas.PremiumDailyLimit, DefaultQuotas.PremiumWeeklyLimit),
            _ => (DefaultQuotas.FreeDailyLimit, DefaultQuotas.FreeWeeklyLimit) // Default to Free tier limits
        };
    }

    private static string GetDateKey(DateTime date)
    {
        // Format: yyyy-MM-dd (e.g., 2025-11-22)
        return date.ToString("yyyy-MM-dd");
    }

    private static string GetWeekKey(DateTime date)
    {
        // ISO 8601 week-numbering year calculation
        // See: https://en.wikipedia.org/wiki/ISO_week_date
        // Week 1 is the first week with at least 4 days in the new year (contains Thursday)

        var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        var weekRule = System.Globalization.CalendarWeekRule.FirstFourDayWeek;
        var firstDayOfWeek = DayOfWeek.Monday;

        var week = calendar.GetWeekOfYear(date, weekRule, firstDayOfWeek);
        int year = date.Year;

        // ISO 8601 year transitions:
        // 1. Early January (week >= 52): belongs to previous year's last week
        if (date.Month == 1 && week >= 52)
        {
            year = date.Year - 1;
        }
        // 2. Late December with week 1: GetWeekOfYear already calculated it as week 1 of next year
        else if (date.Month == 12 && week == 1)
        {
            year = date.Year + 1;
        }
        // 3. Late December (Dec 29-31) on Mon-Wed: might be week 1 of next year
        //    GetWeekOfYear returns week 53, but ISO 8601 says it's actually week 1 of next year
        else if (date.Month == 12 && date.Day >= 29)
        {
            // Check if this is actually week 1 of next year
            // If Dec 29-31 falls on Mon-Wed, it's week 1 of next year
            var dayOfWeek = date.DayOfWeek;
            if (dayOfWeek >= DayOfWeek.Monday && dayOfWeek <= DayOfWeek.Wednesday)
            {
                year = date.Year + 1;
                week = 1;
            }
        }

        return $"{year}-W{week:D2}";
    }

    private static DateTime GetNextDailyReset(DateTime now)
    {
        // Next midnight UTC
        return now.Date.AddDays(1);
    }

    private static DateTime GetNextWeeklyReset(DateTime now)
    {
        // Next Monday at midnight UTC
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0)
        {
            daysUntilMonday = 7; // If today is Monday, reset next Monday
        }
        return now.Date.AddDays(daysUntilMonday);
    }

    #endregion
}
