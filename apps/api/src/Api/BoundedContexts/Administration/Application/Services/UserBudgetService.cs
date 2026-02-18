using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Services;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// DTO for user budget status response
/// </summary>
public sealed record UserBudgetStatus(
    decimal CreditsRemaining,
    decimal DailyLimit,
    decimal WeeklyLimit,
    string DailyResetIn,
    string WeeklyResetIn,
    bool IsBlocked,
    decimal DailyCreditsUsed,
    decimal WeeklyCreditsUsed);

/// <summary>
/// Service for managing user credit budgets with caching
/// </summary>
public interface IUserBudgetService
{
    /// <summary>
    /// Get user's current budget status (cached for 5 minutes)
    /// </summary>
    Task<UserBudgetStatus> GetUserBudgetAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Check if user has sufficient budget for a query (non-blocking, fail-open)
    /// </summary>
    Task<bool> HasBudgetForQueryAsync(Guid userId, decimal estimatedCredits, CancellationToken ct = default);

    /// <summary>
    /// Record credit usage after LLM request completes
    /// </summary>
    Task RecordUsageAsync(Guid userId, decimal usdCost, int totalTokens, CancellationToken ct = default);
}

/// <summary>
/// Implementation with HybridCache for performance
/// </summary>
internal sealed class UserBudgetService : IUserBudgetService
{
    private readonly IUserTokenUsageRepository _usageRepository;
    private readonly ITokenTierRepository _tierRepository;
    private readonly IHybridCacheService _cache;
    private readonly ICreditConversionService _creditConversion;
    private readonly ILogger<UserBudgetService> _logger;

    public UserBudgetService(
        IUserTokenUsageRepository usageRepository,
        ITokenTierRepository tierRepository,
        IHybridCacheService cache,
        ICreditConversionService creditConversion,
        ILogger<UserBudgetService> logger)
    {
        _usageRepository = usageRepository ?? throw new ArgumentNullException(nameof(usageRepository));
        _tierRepository = tierRepository ?? throw new ArgumentNullException(nameof(tierRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _creditConversion = creditConversion ?? throw new ArgumentNullException(nameof(creditConversion));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<UserBudgetStatus> GetUserBudgetAsync(Guid userId, CancellationToken ct = default)
    {
        var cacheKey = $"user:budget:{userId}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancellationToken => await ComputeBudgetStatusAsync(userId, cancellationToken).ConfigureAwait(false),
            ["budget", $"user:{userId}"],
            TimeSpan.FromMinutes(5), // 5min cache
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> HasBudgetForQueryAsync(
        Guid userId,
        decimal estimatedCredits,
        CancellationToken ct = default)
    {
        try
        {
            var budget = await GetUserBudgetAsync(userId, ct).ConfigureAwait(false);

            // Fail-open: if blocked status set, deny budget
            if (budget.IsBlocked) return false;

            // Check if sufficient daily and weekly budget
            var hasDailyBudget = budget.CreditsRemaining >= estimatedCredits;
            var hasWeeklyBudget = (budget.WeeklyLimit - budget.WeeklyCreditsUsed) >= estimatedCredits;

            return hasDailyBudget && hasWeeklyBudget;
        }
        catch (Exception ex)
        {
            // Fail-open: on error, allow request (availability > strict enforcement)
            _logger.LogWarning(ex,
                "Budget check failed for user {UserId}, failing open (allowing request)",
                userId);
            return true;
        }
    }

    /// <inheritdoc />
    public async Task RecordUsageAsync(
        Guid userId,
        decimal usdCost,
        int totalTokens,
        CancellationToken ct = default)
    {
        var credits = _creditConversion.UsdToCredits(usdCost);

        // Get UserTokenUsage record
        var usage = await _usageRepository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);

        if (usage == null)
        {
            _logger.LogWarning(
                "UserTokenUsage not found for user {UserId}, cannot record credits. Skipping.",
                userId);
            return;
        }

        // Record credit usage with auto-reset
        usage.RecordCreditUsage(credits, timeProvider: null); // System time

        // Also update token usage (existing tracking)
        usage.RecordUsage(totalTokens, usdCost);

        // Get tier limits and check if user should be blocked
        var tier = await _tierRepository.GetByIdAsync(usage.TierId, ct).ConfigureAwait(false);
        if (tier != null)
        {
            usage.CheckLimits(tier.Limits);
        }

        await _usageRepository.UpdateAsync(usage, ct).ConfigureAwait(false);

        // Invalidate cache
        await _cache.RemoveByTagAsync($"user:{userId}", ct).ConfigureAwait(false);

        _logger.LogDebug(
            "Recorded {Credits} credits (${Cost:F6}) for user {UserId}",
            credits, usdCost, userId);
    }

    private async Task<UserBudgetStatus> ComputeBudgetStatusAsync(Guid userId, CancellationToken ct)
    {
        // Get usage
        var usage = await _usageRepository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);

        if (usage == null)
        {
            throw new InvalidOperationException($"UserTokenUsage not found for user {userId}");
        }

        // Get tier limits
        var tier = await _tierRepository.GetByIdAsync(usage.TierId, ct).ConfigureAwait(false);
        if (tier == null)
        {
            throw new InvalidOperationException($"TokenTier not found for tier ID {usage.TierId}");
        }

        var limits = tier.Limits;

        // Calculate remaining credits
        var dailyRemaining = Math.Max(0, limits.DailyCreditsLimit - usage.DailyCreditsUsed);

        // Calculate time until reset
        var now = DateTime.UtcNow;
        var nextDailyReset = usage.LastDailyReset.Date.AddDays(1);
        var dailyResetIn = FormatTimeSpan(nextDailyReset - now);

        var nextWeeklyReset = GetNextMonday(usage.LastWeeklyReset);
        var weeklyResetIn = FormatTimeSpan(nextWeeklyReset - now);

        return new UserBudgetStatus(
            CreditsRemaining: dailyRemaining,
            DailyLimit: limits.DailyCreditsLimit,
            WeeklyLimit: limits.WeeklyCreditsLimit,
            DailyResetIn: dailyResetIn,
            WeeklyResetIn: weeklyResetIn,
            IsBlocked: usage.IsBlocked,
            DailyCreditsUsed: usage.DailyCreditsUsed,
            WeeklyCreditsUsed: usage.WeeklyCreditsUsed);
    }

    private static DateTime GetNextMonday(DateTime from)
    {
        var daysSinceMonday = ((int)from.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var currentMonday = from.Date.AddDays(-daysSinceMonday);
        return currentMonday.AddDays(7); // Next Monday
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours}h";
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{ts.Minutes}m";
    }
}
