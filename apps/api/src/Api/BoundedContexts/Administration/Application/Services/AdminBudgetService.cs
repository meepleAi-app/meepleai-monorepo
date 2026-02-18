using Api.BoundedContexts.Administration.Application.Interfaces;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Services;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// DTO for admin budget overview (OpenRouter + App budgets)
/// </summary>
public sealed record AdminBudgetOverview(
    decimal OpenRouterBalanceEuros,
    decimal DailySpendUsd,
    decimal WeeklySpendUsd,
    decimal DailyBudgetUsd,
    decimal WeeklyBudgetUsd,
    int DailyUsagePercent,
    int WeeklyUsagePercent);

/// <summary>
/// Wrapper for caching decimal balance (HybridCache requires reference types)
/// </summary>
internal sealed record CachedBalance(decimal Value);

/// <summary>
/// Service for admin budget monitoring and OpenRouter balance
/// </summary>
public interface IAdminBudgetService
{
    /// <summary>
    /// Get comprehensive budget overview for admins (cached 15 minutes)
    /// </summary>
    Task<AdminBudgetOverview> GetBudgetOverviewAsync(CancellationToken ct = default);

    /// <summary>
    /// Get OpenRouter balance (cached 15 minutes)
    /// </summary>
    Task<decimal> GetOpenRouterBalanceAsync(CancellationToken ct = default);
}

/// <summary>
/// Implementation with HybridCache and OpenRouter integration
/// </summary>
internal sealed class AdminBudgetService : IAdminBudgetService
{
    private readonly IOpenRouterService _openRouterService;
    private readonly ILlmCostLogRepository _costLogRepository;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<AdminBudgetService> _logger;

    // Admin budget configuration
    // Note: These default values should be moved to appsettings or database for configurability
    private const decimal DAILY_BUDGET_USD = 50.00m;
    private const decimal WEEKLY_BUDGET_USD = 200.00m;

    public AdminBudgetService(
        IOpenRouterService openRouterService,
        ILlmCostLogRepository costLogRepository,
        IHybridCacheService cache,
        ILogger<AdminBudgetService> logger)
    {
        _openRouterService = openRouterService ?? throw new ArgumentNullException(nameof(openRouterService));
        _costLogRepository = costLogRepository ?? throw new ArgumentNullException(nameof(costLogRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<decimal> GetOpenRouterBalanceAsync(CancellationToken ct = default)
    {
        const string cacheKey = "admin:openrouter:balance";

        var cached = await _cache.GetOrCreateAsync(
            cacheKey,
            async cancellationToken =>
            {
                try
                {
                    var response = await _openRouterService.GetBalanceAsync(cancellationToken).ConfigureAwait(false);

                    // Convert USD to Euros (rough estimate: 1 USD ≈ 0.92 EUR as of 2026)
                    const decimal USD_TO_EUR = 0.92m;
                    var balanceEuros = response.CurrentBalance * USD_TO_EUR;

                    _logger.LogInformation(
                        "OpenRouter balance: ${Usd:F2} USD (€{Euro:F2} EUR)",
                        response.CurrentBalance, balanceEuros);

                    return new CachedBalance(balanceEuros);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch OpenRouter balance, returning 0");
                    return new CachedBalance(0m); // Fail-open with zero balance
                }
            },
            ["budget", "openrouter"],
            TimeSpan.FromMinutes(15), // Cache for 15 minutes
            ct).ConfigureAwait(false);

        return cached.Value;
    }

    /// <inheritdoc />
    public async Task<AdminBudgetOverview> GetBudgetOverviewAsync(CancellationToken ct = default)
    {
        const string cacheKey = "admin:budget:overview";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancellationToken =>
            {
                // Get OpenRouter balance
                var openRouterBalance = await GetOpenRouterBalanceAsync(cancellationToken).ConfigureAwait(false);

                var now = DateTime.UtcNow;

                // Get daily spend (today)
                var dailySpend = await _costLogRepository.GetTotalCostAsync(
                    startDate: DateOnly.FromDateTime(now.Date),
                    endDate: DateOnly.FromDateTime(now),
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                // Get weekly spend (Monday 00:00 to now)
                var weekStart = GetWeekStart(now);
                var weeklySpend = await _costLogRepository.GetTotalCostAsync(
                    startDate: DateOnly.FromDateTime(weekStart),
                    endDate: DateOnly.FromDateTime(now),
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                // Calculate usage percentages
                var dailyPercent = dailySpend > 0
                    ? (int)Math.Round(dailySpend / DAILY_BUDGET_USD * 100)
                    : 0;

                var weeklyPercent = weeklySpend > 0
                    ? (int)Math.Round(weeklySpend / WEEKLY_BUDGET_USD * 100)
                    : 0;

                return new AdminBudgetOverview(
                    OpenRouterBalanceEuros: openRouterBalance,
                    DailySpendUsd: dailySpend,
                    WeeklySpendUsd: weeklySpend,
                    DailyBudgetUsd: DAILY_BUDGET_USD,
                    WeeklyBudgetUsd: WEEKLY_BUDGET_USD,
                    DailyUsagePercent: dailyPercent,
                    WeeklyUsagePercent: weeklyPercent);
            },
            ["budget", "admin"],
            TimeSpan.FromMinutes(5), // Cache for 5 minutes
            ct).ConfigureAwait(false);
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.Date.AddDays(-daysSinceMonday);
    }
}
