using System.Globalization;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Api.SharedKernel.Services;

/// <summary>
/// Enforces tier-based resource limits using Redis atomic counters and EF Core.
/// D3: Game Night Flow - tier enforcement with Redis atomic counters.
///
/// Redis key pattern: tier:usage:{userId}:{action}:{date-bucket}
/// Date buckets: daily (yyyy-MM-dd), monthly (yyyy-MM), weekly ISO 8601 (yyyy-Www, see <see cref="IsoWeekKey"/>)
/// </summary>
internal sealed class TierEnforcementService : ITierEnforcementService
{
    private const string KeyPrefix = "tier:usage";
    private static readonly TimeSpan TierCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DailyTtl = TimeSpan.FromDays(2);
    private static readonly TimeSpan MonthlyTtl = TimeSpan.FromDays(35);
    private static readonly TimeSpan WeeklyTtl = TimeSpan.FromDays(8);

    private readonly IConnectionMultiplexer _redis;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TierEnforcementService> _logger;
    private readonly TimeProvider _timeProvider;

    public TierEnforcementService(
        IConnectionMultiplexer redis,
        MeepleAiDbContext dbContext,
        IMemoryCache cache,
        ILogger<TierEnforcementService> logger,
        TimeProvider? timeProvider = null)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<TierLimits> GetLimitsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            .ConfigureAwait(false);

        if (user is null)
            return TierLimits.FreeTier;

        // SuperAdmin, Admin and Editor users get unlimited access
        if (string.Equals(user.Role, "superadmin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Role, "editor", StringComparison.OrdinalIgnoreCase))
            return TierLimits.Unlimited;

        // Contributors are resolved as premium. "pro" is an alias of premium (UserTier), and no
        // plan carries that name: without this, a pro user got the free plan's limits (#4168).
        var tierName = user.IsContributor || string.Equals(user.Tier, "pro", StringComparison.OrdinalIgnoreCase)
            ? "premium"
            : user.Tier;

        var definition = await GetTierDefinitionAsync(tierName, ct).ConfigureAwait(false);
        if (definition is null)
        {
            // Not silent: since #4168 this fallback caps the user's questions at the free plan's.
            _logger.LogWarning(
                "No plan named {Tier} for user {UserId}: using the free plan's limits", tierName, userId);
            return TierLimits.FreeTier;
        }

        return definition.Limits;
    }

    public async Task<bool> CanPerformAsync(Guid userId, TierAction action, CancellationToken ct = default)
    {
        var limits = await GetLimitsAsync(userId, ct).ConfigureAwait(false);

        // Boolean-based actions
        if (action == TierAction.SaveSession)
            return limits.SessionSaveEnabled;

        // Boolean-based actions: RAPTOR rebuild is premium-only (blocked for free tier)
        if (action == TierAction.RaptorRebuild)
            return limits.RaptorRebuildEnabled;

        // Rate-based actions — check Redis counter
        var currentUsage = await GetRedisCounterAsync(userId, action).ConfigureAwait(false);
        var maxAllowed = GetMaxForAction(limits, action);

        return currentUsage < maxAllowed;
    }

    public async Task<TierUsageCheck> CheckUsageAsync(Guid userId, TierAction action, CancellationToken ct = default)
    {
        var limits = await GetLimitsAsync(userId, ct).ConfigureAwait(false);
        var max = GetMaxForAction(limits, action);

        // No limit, nothing to compare the counter with: it is not read.
        if (max == int.MaxValue)
            return new TierUsageCheck(0, max, CounterReadable: true);

        var current = await TryReadRedisCounterAsync(userId, action).ConfigureAwait(false);
        return new TierUsageCheck(current ?? 0, max, CounterReadable: current.HasValue);
    }

    public async Task RecordUsageAsync(Guid userId, TierAction action, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildRedisKey(userId, action);
            var ttl = GetTtlForAction(action);

            var newValue = await db.StringIncrementAsync(key, 1).ConfigureAwait(false);

            // Only set TTL on key creation (first increment = 1) to avoid resetting expiry on every usage
            if (newValue == 1)
            {
                await db.KeyExpireAsync(key, ttl).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record tier usage for user {UserId}, action {Action} (graceful degradation)",
                userId, action);
        }
    }

    public async Task<UsageSnapshot> GetUsageAsync(Guid userId, CancellationToken ct = default)
    {
        var limits = await GetLimitsAsync(userId, ct).ConfigureAwait(false);

        var pdfThisMonth = await GetRedisCounterAsync(userId, TierAction.UploadPdf).ConfigureAwait(false);
        var agentQueriesToday = await GetRedisCounterAsync(userId, TierAction.AgentQuery).ConfigureAwait(false);
        var sessionQueries = await GetRedisCounterAsync(userId, TierAction.SessionAgentQuery).ConfigureAwait(false);
        var photosThisSession = await GetRedisCounterAsync(userId, TierAction.UploadSessionPhoto).ConfigureAwait(false);
        var catalogProposals = await GetRedisCounterAsync(userId, TierAction.ProposeToSharedCatalog).ConfigureAwait(false);

        // E2-2: Count-based metrics from database
        var privateGames = await _dbContext.PrivateGames
            .CountAsync(g => g.OwnerId == userId, ct)
            .ConfigureAwait(false);

        return new UsageSnapshot(
            PrivateGames: privateGames,
            PrivateGamesMax: limits.MaxPrivateGames,
            PdfThisMonth: pdfThisMonth,
            PdfThisMonthMax: limits.MaxPdfUploadsPerMonth,
            AgentQueriesToday: agentQueriesToday,
            AgentQueriesTodayMax: limits.MaxAgentQueriesPerDay,
            SessionQueries: sessionQueries,
            SessionQueriesMax: limits.MaxSessionQueries,
            PhotosThisSession: photosThisSession,
            PhotosThisSessionMax: limits.MaxPhotosPerSession,
            SessionSaveEnabled: limits.SessionSaveEnabled,
            CatalogProposalsThisWeek: catalogProposals,
            CatalogProposalsThisWeekMax: limits.MaxCatalogProposalsPerWeek
        );
    }

    public async Task<GamebookQuotaSnapshot> GetGamebookQuotaSnapshotAsync(Guid userId, CancellationToken ct = default)
    {
        // Issue #2750 (C14): display-only monthly gamebook translation quota.
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            .ConfigureAwait(false);

        string tier;
        if (user is null)
        {
            tier = "free";
        }
        else if (string.Equals(user.Role, "superadmin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Role, "editor", StringComparison.OrdinalIgnoreCase))
        {
            // Unlimited tier surfaces as "premium" for the FE quota-widget enum.
            tier = "premium";
        }
        else
        {
            // FE QuotaInfo.tier is a binary enum ('free' | 'premium'); collapse any paid tier
            // (normal/pro/premium/enterprise) or contributor to "premium" so the Zod parse holds.
            var resolved = user.IsContributor ? "premium" : (user.Tier ?? "free");
            tier = string.Equals(resolved, "free", StringComparison.OrdinalIgnoreCase) ? "free" : "premium";
        }

        var limits = await GetLimitsAsync(userId, ct).ConfigureAwait(false);
        var used = await GetRedisCounterAsync(userId, TierAction.TranslateGamebookParagraph).ConfigureAwait(false);

        var now = UtcNow;
        var resetDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);

        return new GamebookQuotaSnapshot(
            TranslationsThisMonth: used,
            MaxTranslationsPerMonth: limits.MaxGamebookTranslationsPerMonth,
            ResetDate: resetDate,
            Tier: tier);
    }

    internal static string TierDefinitionCacheKey(string tierName) => $"tier_def:{tierName}";

    // ─── Private helpers ──────────────────────────────────────────────────

    private async Task<TierDefinition?> GetTierDefinitionAsync(string tierName, CancellationToken ct)
    {
        var cacheKey = TierDefinitionCacheKey(tierName);

        if (_cache.TryGetValue(cacheKey, out TierDefinition? cached))
            return cached;

        var definition = await _dbContext.Set<TierDefinition>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Name == tierName, ct)
            .ConfigureAwait(false);

        if (definition is not null)
        {
            _cache.Set(cacheKey, definition, TierCacheDuration);
        }

        return definition;
    }

    /// <summary>The counter, or zero when Redis could not be read (graceful degradation).</summary>
    private async Task<int> GetRedisCounterAsync(Guid userId, TierAction action) =>
        await TryReadRedisCounterAsync(userId, action).ConfigureAwait(false) ?? 0;

    /// <summary>
    /// The counter, or null when Redis could not be read. A counter never written is a zero that
    /// was read; a Redis that did not answer is a count nobody knows.
    /// </summary>
    private async Task<int?> TryReadRedisCounterAsync(Guid userId, TierAction action)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildRedisKey(userId, action);
            var raw = await db.StringGetAsync(key).ConfigureAwait(false);

            return raw.HasValue && int.TryParse(raw.ToString(), CultureInfo.InvariantCulture, out var val)
                ? val
                : 0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis read failed for tier counter (graceful degradation)");
            return null;
        }
    }

    /// <summary>The buckets are UTC days, months and weeks: one clock for all of them.</summary>
    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    private string BuildRedisKey(Guid userId, TierAction action)
    {
        var bucket = GetDateBucket(action);
        return $"{KeyPrefix}:{userId}:{action}:{bucket}";
    }

    private string GetDateBucket(TierAction action) => action switch
    {
        TierAction.AgentQuery or TierAction.SessionAgentQuery
            => UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),

        TierAction.UploadPdf or TierAction.TranslateGamebookParagraph
            => UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture),

        TierAction.ProposeToSharedCatalog
            => IsoWeekKey.For(UtcNow),

        TierAction.UploadSessionPhoto
            => UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),

        // Count-based actions don't use date buckets, but we still need a key
        _ => UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    };

    private static TimeSpan GetTtlForAction(TierAction action) => action switch
    {
        TierAction.AgentQuery or TierAction.SessionAgentQuery or TierAction.UploadSessionPhoto
            => DailyTtl,

        TierAction.UploadPdf or TierAction.TranslateGamebookParagraph
            => MonthlyTtl,

        TierAction.ProposeToSharedCatalog
            => WeeklyTtl,

        _ => DailyTtl
    };

    private static int GetMaxForAction(TierLimits limits, TierAction action) => action switch
    {
        TierAction.CreatePrivateGame => limits.MaxPrivateGames,
        TierAction.UploadPdf => limits.MaxPdfUploadsPerMonth,
        TierAction.AgentQuery => limits.MaxAgentQueriesPerDay,
        TierAction.SessionAgentQuery => limits.MaxSessionQueries,
        TierAction.UploadSessionPhoto => limits.MaxPhotosPerSession,
        TierAction.ProposeToSharedCatalog => limits.MaxCatalogProposalsPerWeek,
        // RaptorRebuild is handled separately as a boolean gate — this path is never reached.
        TierAction.RaptorRebuild => int.MaxValue,
        TierAction.TranslateGamebookParagraph => limits.MaxGamebookTranslationsPerMonth,
        _ => int.MaxValue
    };
}
