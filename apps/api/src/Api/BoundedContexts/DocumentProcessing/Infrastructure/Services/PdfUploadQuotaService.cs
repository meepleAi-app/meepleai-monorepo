using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.Services;
using Api.SharedKernel.Utilities;
using StackExchange.Redis;
using System.Globalization;
using System.Text.Json;
using AuthRole = Api.SharedKernel.Domain.ValueObjects.Role;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Redis-based implementation of PDF upload quota service.
/// Tracks daily and weekly upload counts with automatic TTL-based reset.
/// </summary>
internal class PdfUploadQuotaService : IPdfUploadQuotaService
{
    /// <summary>
    /// Default upload quotas for each tier.
    /// </summary>
    private static class DefaultQuotas
    {
        // Free Tier
        public const int FreeDailyLimit = 5;
        public const int FreeWeeklyLimit = 20;
        public const int FreePerGameLimit = 1;

        // Normal Tier
        public const int NormalDailyLimit = 20;
        public const int NormalWeeklyLimit = 100;
        public const int NormalPerGameLimit = 3;

        // Premium Tier
        public const int PremiumDailyLimit = 100;
        public const int PremiumWeeklyLimit = 500;
        public const int PremiumPerGameLimit = 10;
    }
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
        ArgumentNullException.ThrowIfNull(redis);
        _redis = redis;
        ArgumentNullException.ThrowIfNull(configService);
        _configService = configService;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<PdfUploadQuotaResult> CheckQuotaAsync(
        Guid userId,
        UserTier userTier,
        AuthRole userRole,
        CancellationToken cancellationToken = default)
    {
        // SuperAdmin, Admin and Editor bypass quota checks (unlimited)
        if (userRole.IsSuperAdmin() || userRole.IsExactlyAdmin() || userRole.IsEditor())
        {
            return PdfUploadQuotaResult.Success(0, int.MaxValue, 0, int.MaxValue, DateTime.MaxValue, DateTime.MaxValue);
        }

        try
        {
            var (dailyLimit, weeklyLimit) = await GetLimitsForTierAsync(userTier).ConfigureAwait(false);
            var (dailyUsed, weeklyUsed) = await GetUsageAsync(userId).ConfigureAwait(false);

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
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // FAIL-OPEN PATTERN: Infrastructure resilience for quota enforcement
        // Catches all Redis/network failures during quota check. Fails open (allows upload)
        // to prioritize availability over quota enforcement. Without this, Redis downtime
        // would block all uploads. Logs error for monitoring.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking PDF upload quota for user {UserId}", userId);
            // Fail-open: allow upload if quota check fails (prioritize availability)
            return PdfUploadQuotaResult.Success(0, int.MaxValue, 0, int.MaxValue, DateTime.MaxValue, DateTime.MaxValue);
        }
#pragma warning restore CA1031
    }

    public Task IncrementUploadCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        IncrementUploadCountAsync(userId, _timeProvider.GetUtcNow().UtcDateTime);

    /// <summary>
    /// Incrementa i contatori del periodo di <paramref name="at"/> (UTC). La prenotazione passa
    /// l'istante che scrive in <c>ReservedAt</c>, cosi' il rilascio decrementa le stesse chiavi
    /// (#4547): con due letture dell'orologio, una mezzanotte caduta fra le due le separerebbe.
    /// </summary>
    private async Task IncrementUploadCountAsync(Guid userId, DateTime at)
    {
        try
        {
            var db = _redis.GetDatabase();

            var dailyKey = DailyCounterKey(userId, at);
            var weeklyKey = WeeklyCounterKey(userId, at);

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
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // NON-CRITICAL OPERATION: Usage tracking failure tolerance.
        // Catches all Redis failures during upload count increment.
        // This is pure tracking/analytics - failure doesn't impact upload functionality.
        // Logs warning for monitoring but swallows exception to avoid disrupting main upload flow.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to increment upload count for user {UserId}", userId);
        }
#pragma warning restore CA1031
    }

    public async Task<QuotaReservationResult> ReserveQuotaAsync(
        Guid userId,
        string pdfId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfId);
        try
        {
            var db = _redis.GetDatabase();
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var expiresAt = now.AddMinutes(30);

            var reservationKey = ReservationKey(userId, pdfId);
            // ReservedAt e' letto dal rilascio per decrementare i contatori di questo periodo (#4547).
            var reservationData = System.Text.Json.JsonSerializer.Serialize(new
            {
                UserId = userId,
                PdfId = pdfId,
                ReservedAt = now,
                ExpiresAt = expiresAt
            });

            var ttl = TimeSpan.FromMinutes(30);
            await db.StringSetAsync(reservationKey, reservationData, ttl).ConfigureAwait(false);
            await IncrementUploadCountAsync(userId, now).ConfigureAwait(false);

            _logger.LogInformation(
                "Quota reserved for user {UserId}, PDF {PdfId}, expires at {ExpiresAt}",
                userId, pdfId, expiresAt);

            return QuotaReservationResult.Success(expiresAt);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // INFRASTRUCTURE SERVICE PATTERN: Graceful degradation on Redis failure
        // Catches all Redis/network failures during quota reservation. Returns failure result
        // instead of throwing to allow caller to handle gracefully (e.g., retry, fallback).
        // Logs error for monitoring. Prevents infrastructure failures from propagating.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reserve quota for user {UserId}, PDF {PdfId}", userId, pdfId);
            return QuotaReservationResult.Failed($"Failed to reserve quota: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    public async Task ConfirmQuotaAsync(Guid userId, string pdfId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfId);
        try
        {
            var db = _redis.GetDatabase();
            var reservationKey = ReservationKey(userId, pdfId);

            var reservationExists = await db.KeyExistsAsync(reservationKey).ConfigureAwait(false);
            if (!reservationExists)
            {
                _logger.LogWarning(
                    "No reservation found for user {UserId}, PDF {PdfId} - may have expired",
                    userId, pdfId);
                return;
            }

            await db.KeyDeleteAsync(reservationKey).ConfigureAwait(false);

            _logger.LogInformation(
                "Quota confirmed for user {UserId}, PDF {PdfId}",
                userId, pdfId);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // NON-CRITICAL OPERATION PATTERN: Reservation cleanup failure tolerance
        // Catches all Redis failures during quota confirmation (reservation cleanup).
        // Cleanup failure is non-critical; logs warning but doesn't throw. Reservations
        // auto-expire via TTL anyway. Fail-safe for cleanup operations.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to confirm quota for user {UserId}, PDF {PdfId}", userId, pdfId);
        }
#pragma warning restore CA1031
    }

    public async Task ReleaseQuotaAsync(Guid userId, string pdfId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfId);
        try
        {
            var db = _redis.GetDatabase();
            var reservationKey = ReservationKey(userId, pdfId);

            // GETDEL legge e cancella la prenotazione in un solo comando: se due rilasci arrivano
            // insieme, uno solo trova il valore e decrementa. Prima erano EXISTS, i decrementi e poi
            // DEL: due rilasci concorrenti vedevano tutti e due la prenotazione (#4547).
            var reservation = await db.StringGetDeleteAsync(reservationKey).ConfigureAwait(false);
            if (reservation.IsNull)
            {
                _logger.LogWarning(
                    "No reservation found for user {UserId}, PDF {PdfId} - may have already expired or been released",
                    userId, pdfId);
                return;
            }

            // I contatori da decrementare sono quelli del periodo della prenotazione, non di adesso:
            // prenotata domenica alle 23:50 e rilasciata lunedi' alle 00:10, si decrementano domenica
            // e la sua settimana (#4547). Se ReservedAt non si legge, si ripiega sul periodo di adesso,
            // cioe' il comportamento di prima.
            var reservedAt = TryReadReservedAt(reservation);
            if (reservedAt is null)
            {
                _logger.LogWarning(
                    "Reservation for user {UserId}, PDF {PdfId} has no readable ReservedAt - releasing the counters of the current period",
                    userId, pdfId);
            }

            await DecrementUploadCountAsync(userId, reservedAt ?? _timeProvider.GetUtcNow().UtcDateTime).ConfigureAwait(false);

            _logger.LogInformation(
                "Quota released for user {UserId}, PDF {PdfId}",
                userId, pdfId);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // NON-CRITICAL OPERATION PATTERN: Quota release failure tolerance
        // Catches all Redis failures during quota release (decrement + cleanup).
        // Release failure is non-critical; logs warning but doesn't throw. Quotas reset
        // automatically via TTL. Fail-safe for cleanup operations.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to release quota for user {UserId}, PDF {PdfId}", userId, pdfId);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// L'istante della prenotazione, dal JSON scritto da <see cref="ReserveQuotaAsync"/>. Il formato
    /// e' lo stesso da prima di #4547, quindi vale anche per le prenotazioni scritte prima del
    /// deploy. <c>null</c> se il valore non e' un oggetto JSON con <c>ReservedAt</c> come data.
    /// </summary>
    private static DateTime? TryReadReservedAt(RedisValue reservation)
    {
        try
        {
            using var json = JsonDocument.Parse((string)reservation!);
            var root = json.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("ReservedAt", out var reservedAt)
                && reservedAt.ValueKind == JsonValueKind.String
                && reservedAt.TryGetDateTimeOffset(out var instant)
                    ? instant.UtcDateTime
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task DecrementUploadCountAsync(Guid userId, DateTime at)
    {
        try
        {
            var db = _redis.GetDatabase();

            var dailyKey = DailyCounterKey(userId, at);
            var weeklyKey = WeeklyCounterKey(userId, at);

            var script = @"
                local key = KEYS[1]
                local current = redis.call('GET', key)
                if current and tonumber(current) > 0 then
                    return redis.call('DECR', key)
                else
                    return 0
                end
            ";

            var dailyKeys = new RedisKey[] { dailyKey };
            await db.ScriptEvaluateAsync(script, dailyKeys).ConfigureAwait(false);

            var weeklyKeys = new RedisKey[] { weeklyKey };
            await db.ScriptEvaluateAsync(script, weeklyKeys).ConfigureAwait(false);

            _logger.LogDebug("Decremented PDF upload count for user {UserId}", userId);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // NON-CRITICAL OPERATION: Usage tracking decrement failure tolerance.
        // Catches all Redis failures during upload count decrement.
        // This is tracking/analytics only - failure doesn't impact functionality.
        // Logs warning but swallows exception. Fail-safe for non-critical operations.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrement upload count for user {UserId}", userId);
        }
#pragma warning restore CA1031
    }

    public async Task<PdfUploadQuotaInfo> GetQuotaInfoAsync(
        Guid userId,
        UserTier userTier,
        AuthRole userRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userTier);
        // SuperAdmin, Admin and Editor have unlimited quota
        if (userRole.IsSuperAdmin() || userRole.IsExactlyAdmin() || userRole.IsEditor())
        {
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
            var (dailyLimit, weeklyLimit) = await GetLimitsForTierAsync(userTier).ConfigureAwait(false);
            var (dailyUsed, weeklyUsed) = await GetUsageAsync(userId).ConfigureAwait(false);

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
#pragma warning disable S2139 // Exceptions should be either logged or rethrown but not both
        // INFRASTRUCTURE LOGGING PATTERN: Log exceptions at the infrastructure boundary for debugging.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quota info for user {UserId}", userId);
            throw;
        }
#pragma warning restore S2139
    }
    private async Task<(int dailyLimit, int weeklyLimit)> GetLimitsForTierAsync(UserTier tier)
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

    private async Task<(int dailyUsed, int weeklyUsed)> GetUsageAsync(Guid userId)
    {
        var db = _redis.GetDatabase();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var dailyKey = DailyCounterKey(userId, now);
        var weeklyKey = WeeklyCounterKey(userId, now);

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
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    // Le chiavi dei contatori si calcolano solo qui: incremento, decremento e lettura usano la
    // stessa formula (#4547).
    private static string DailyCounterKey(Guid userId, DateTime at) =>
        $"pdf:upload:daily:{userId}:{GetDateKey(at)}";

    private static string WeeklyCounterKey(Guid userId, DateTime at) =>
        $"pdf:upload:weekly:{userId}:{IsoWeekKey.For(at)}";

    private static string ReservationKey(Guid userId, string pdfId) =>
        $"pdf:quota:reservation:{userId}:{pdfId}";

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

    // ============================================================================
    // Issue #3653: Per-Game Quota Methods for Private PDF Uploads
    // ============================================================================

    public async Task<PerGameQuotaResult> CheckPerGameQuotaAsync(
        Guid userId,
        Guid gameId,
        UserTier userTier,
        AuthRole userRole,
        CancellationToken cancellationToken = default)
    {
        // SuperAdmin, Admin and Editor bypass quota checks (unlimited)
        if (userRole.IsSuperAdmin() || userRole.IsExactlyAdmin() || userRole.IsEditor())
        {
            return PerGameQuotaResult.Unlimited();
        }

        try
        {
            var perGameLimit = await GetPerGameLimitForTierAsync(userTier).ConfigureAwait(false);
            var perGameUsed = await GetPerGameUsageAsync(userId, gameId).ConfigureAwait(false);

            // Check per-game limit
            if (perGameUsed >= perGameLimit)
            {
                return PerGameQuotaResult.Denied(
                    $"Per-game private PDF limit reached ({perGameLimit} PDF/game for {userTier} tier). Delete existing PDFs to upload more.",
                    perGameUsed,
                    perGameLimit);
            }

            return PerGameQuotaResult.Success(perGameUsed, perGameLimit);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // FAIL-OPEN PATTERN: Infrastructure resilience for per-game quota enforcement
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking per-game PDF upload quota for user {UserId}, game {GameId}", userId, gameId);
            // Fail-open: allow upload if quota check fails
            return PerGameQuotaResult.Unlimited();
        }
#pragma warning restore CA1031
    }

    public async Task IncrementPerGameCountAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var perGameKey = GetPerGameKey(userId, gameId);

            // Per-game counts don't expire (permanent until game/PDF is deleted)
            await db.StringIncrementAsync(perGameKey).ConfigureAwait(false);

            _logger.LogDebug("Incremented per-game PDF count for user {UserId}, game {GameId}", userId, gameId);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to increment per-game count for user {UserId}, game {GameId}", userId, gameId);
        }
#pragma warning restore CA1031
    }

    public async Task DecrementPerGameCountAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var perGameKey = GetPerGameKey(userId, gameId);

            var script = @"
                local key = KEYS[1]
                local current = redis.call('GET', key)
                if current and tonumber(current) > 0 then
                    return redis.call('DECR', key)
                else
                    return 0
                end
            ";

            var keys = new RedisKey[] { perGameKey };
            await db.ScriptEvaluateAsync(script, keys).ConfigureAwait(false);

            _logger.LogDebug("Decremented per-game PDF count for user {UserId}, game {GameId}", userId, gameId);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrement per-game count for user {UserId}, game {GameId}", userId, gameId);
        }
#pragma warning restore CA1031
    }

    public async Task<PerGameQuotaInfo> GetPerGameQuotaInfoAsync(
        Guid userId,
        Guid gameId,
        UserTier userTier,
        AuthRole userRole,
        CancellationToken cancellationToken = default)
    {
        // SuperAdmin, Admin and Editor have unlimited quota
        if (userRole.IsSuperAdmin() || userRole.IsExactlyAdmin() || userRole.IsEditor())
        {
            return new PerGameQuotaInfo
            {
                GameId = gameId,
                PerGameUsed = 0,
                PerGameLimit = int.MaxValue,
                PerGameRemaining = int.MaxValue,
                IsUnlimited = true
            };
        }

        try
        {
            var perGameLimit = await GetPerGameLimitForTierAsync(userTier).ConfigureAwait(false);
            var perGameUsed = await GetPerGameUsageAsync(userId, gameId).ConfigureAwait(false);

            return new PerGameQuotaInfo
            {
                GameId = gameId,
                PerGameUsed = perGameUsed,
                PerGameLimit = perGameLimit,
                PerGameRemaining = Math.Max(0, perGameLimit - perGameUsed),
                IsUnlimited = false
            };
        }
#pragma warning disable S2139 // Exceptions should be either logged or rethrown but not both
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting per-game quota info for user {UserId}, game {GameId}", userId, gameId);
            throw;
        }
#pragma warning restore S2139
    }

    private async Task<int> GetPerGameLimitForTierAsync(UserTier tier)
    {
        var tierValue = tier.Value;
        var perGameKey = $"UploadLimits:{tierValue}:PerGameLimit";

        // Get from configuration service (with fallback to defaults)
        var perGameLimit = await _configService.GetValueAsync<int?>(perGameKey, defaultValue: null).ConfigureAwait(false);

        return perGameLimit ?? GetDefaultPerGameLimit(tier);
    }

    private async Task<int> GetPerGameUsageAsync(Guid userId, Guid gameId)
    {
        var db = _redis.GetDatabase();
        var perGameKey = GetPerGameKey(userId, gameId);

        var value = await db.StringGetAsync(perGameKey).ConfigureAwait(false);
        return value.HasValue ? (int)value : 0;
    }

    private static int GetDefaultPerGameLimit(UserTier tier)
    {
        return tier.Value switch
        {
            "free" => DefaultQuotas.FreePerGameLimit,
            "normal" => DefaultQuotas.NormalPerGameLimit,
            "premium" => DefaultQuotas.PremiumPerGameLimit,
            _ => DefaultQuotas.FreePerGameLimit // Default to Free tier limits
        };
    }

    private static string GetPerGameKey(Guid userId, Guid gameId) => $"pdf:upload:game:{userId}:{gameId}";
}

