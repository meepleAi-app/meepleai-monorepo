using System.Globalization;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
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
/// Date buckets: daily (yyyy-MM-dd), monthly (yyyy-MM), weekly (yyyy-Www)
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

    public TierEnforcementService(
        IConnectionMultiplexer redis,
        MeepleAiDbContext dbContext,
        IMemoryCache cache,
        ILogger<TierEnforcementService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TierLimits> GetLimitsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            .ConfigureAwait(false);

        if (user is null)
            return TierLimits.FreeTier;

        // Admin users get unlimited access
        if (string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
            return TierLimits.Unlimited;

        // Contributors are resolved as premium
        var tierName = user.IsContributor ? "premium" : user.Tier;

        var definition = await GetTierDefinitionAsync(tierName, ct).ConfigureAwait(false);
        return definition?.Limits ?? TierLimits.FreeTier;
    }

    public async Task<bool> CanPerformAsync(Guid userId, TierAction action, CancellationToken ct = default)
    {
        var limits = await GetLimitsAsync(userId, ct).ConfigureAwait(false);

        // Boolean-based actions
        if (action == TierAction.SaveSession)
            return limits.SessionSaveEnabled;

        // Rate-based actions — check Redis counter
        var currentUsage = await GetRedisCounterAsync(userId, action).ConfigureAwait(false);
        var maxAllowed = GetMaxForAction(limits, action);

        return currentUsage < maxAllowed;
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

        // Agent system removed (Task 10: Agent cleanup)
        var agents = 0;

        return new UsageSnapshot(
            PrivateGames: privateGames,
            PrivateGamesMax: limits.MaxPrivateGames,
            PdfThisMonth: pdfThisMonth,
            PdfThisMonthMax: limits.MaxPdfUploadsPerMonth,
            AgentQueriesToday: agentQueriesToday,
            AgentQueriesTodayMax: limits.MaxAgentQueriesPerDay,
            SessionQueries: sessionQueries,
            SessionQueriesMax: limits.MaxSessionQueries,
            Agents: agents,
            AgentsMax: limits.MaxAgents,
            PhotosThisSession: photosThisSession,
            PhotosThisSessionMax: limits.MaxPhotosPerSession,
            SessionSaveEnabled: limits.SessionSaveEnabled,
            CatalogProposalsThisWeek: catalogProposals,
            CatalogProposalsThisWeekMax: limits.MaxCatalogProposalsPerWeek
        );
    }

    // ─── Private helpers ──────────────────────────────────────────────────

    private async Task<TierDefinition?> GetTierDefinitionAsync(string tierName, CancellationToken ct)
    {
        var cacheKey = $"tier_def:{tierName}";

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

    private async Task<int> GetRedisCounterAsync(Guid userId, TierAction action)
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
            return 0;
        }
    }

    private static string BuildRedisKey(Guid userId, TierAction action)
    {
        var bucket = GetDateBucket(action);
        return $"{KeyPrefix}:{userId}:{action}:{bucket}";
    }

    private static string GetDateBucket(TierAction action) => action switch
    {
        TierAction.AgentQuery or TierAction.SessionAgentQuery
            => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),

        TierAction.UploadPdf
            => DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture),

        TierAction.ProposeToSharedCatalog
            => GetWeekBucket(),

        TierAction.UploadSessionPhoto
            => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),

        // Count-based actions don't use date buckets, but we still need a key
        _ => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    };

    private static string GetWeekBucket()
    {
        var now = DateTime.UtcNow;
        var cal = CultureInfo.InvariantCulture.Calendar;
        var week = cal.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return $"{now.Year}-W{week:D2}";
    }

    private static TimeSpan GetTtlForAction(TierAction action) => action switch
    {
        TierAction.AgentQuery or TierAction.SessionAgentQuery or TierAction.UploadSessionPhoto
            => DailyTtl,

        TierAction.UploadPdf
            => MonthlyTtl,

        TierAction.ProposeToSharedCatalog
            => WeeklyTtl,

        _ => DailyTtl
    };

    private static int GetMaxForAction(TierLimits limits, TierAction action) => action switch
    {
        TierAction.CreatePrivateGame => limits.MaxPrivateGames,
        TierAction.UploadPdf => limits.MaxPdfUploadsPerMonth,
        TierAction.CreateAgent => limits.MaxAgents,
        TierAction.AgentQuery => limits.MaxAgentQueriesPerDay,
        TierAction.SessionAgentQuery => limits.MaxSessionQueries,
        TierAction.UploadSessionPhoto => limits.MaxPhotosPerSession,
        TierAction.ProposeToSharedCatalog => limits.MaxCatalogProposalsPerWeek,
        _ => int.MaxValue
    };
}
