using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Services;
using System.Globalization;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for monitoring LLM costs and triggering alerts
/// ISSUE-960: BGAI-018 - Cost monitoring and alerting
/// </summary>
internal class LlmCostAlertService
{
    private readonly ILlmCostLogRepository _costLogRepository;
    private readonly IAlertingService _alertingService;
    private readonly ILogger<LlmCostAlertService> _logger;

    // Thresholds from ADR-004
    private const decimal DailyThreshold = 100m;      // $100/day
    private const decimal WeeklyThreshold = 500m;     // $500/week
    private const decimal MonthlyThreshold = 3000m;   // $3000/month (10K MAU target)

    public LlmCostAlertService(
        ILlmCostLogRepository costLogRepository,
        IAlertingService alertingService,
        ILogger<LlmCostAlertService> logger)
    {
        _costLogRepository = costLogRepository ?? throw new ArgumentNullException(nameof(costLogRepository));
        _alertingService = alertingService ?? throw new ArgumentNullException(nameof(alertingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Check daily cost and send alert if threshold exceeded
    /// </summary>
    public async Task CheckDailyCostThresholdAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dailyCost = await _costLogRepository.GetDailyCostAsync(today, cancellationToken).ConfigureAwait(false);

        if (dailyCost > DailyThreshold)
        {
            _logger.LogWarning(
                "ALERT: Daily LLM cost ${DailyCost:F2} exceeds threshold ${Threshold:F2}",
                dailyCost, DailyThreshold);

            var costsByProvider = await _costLogRepository.GetCostsByProviderAsync(today, today, cancellationToken).ConfigureAwait(false);
            var providerBreakdown = string.Join(", ",
                costsByProvider.Select(kv => $"{kv.Key}: ${kv.Value:F2}"));

            await _alertingService.SendAlertAsync(
                alertType: "LLM_COST_THRESHOLD",
                severity: "warning",
                message: $"Daily LLM cost ${dailyCost:F2} exceeds threshold ${DailyThreshold:F2}. Breakdown: {providerBreakdown}",
                metadata: new Dictionary<string, object>
(StringComparer.Ordinal)
                {
                    ["date"] = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["daily_cost"] = dailyCost,
                    ["threshold"] = DailyThreshold,
                    ["exceeded_by"] = dailyCost - DailyThreshold,
                    ["provider_breakdown"] = providerBreakdown
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogDebug("Daily LLM cost ${DailyCost:F2} within threshold ${Threshold:F2}",
                dailyCost, DailyThreshold);
        }
    }

    /// <summary>
    /// Check weekly cost threshold
    /// </summary>
    public async Task CheckWeeklyCostThresholdAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekStart = today.AddDays(-7);
        var weeklyCost = await _costLogRepository.GetTotalCostAsync(weekStart, today, cancellationToken).ConfigureAwait(false);

        if (weeklyCost > WeeklyThreshold)
        {
            _logger.LogWarning(
                "ALERT: Weekly LLM cost ${WeeklyCost:F2} exceeds threshold ${Threshold:F2}",
                weeklyCost, WeeklyThreshold);

            await _alertingService.SendAlertAsync(
                alertType: "LLM_WEEKLY_COST_THRESHOLD",
                severity: "warning",
                message: $"Weekly LLM cost ${weeklyCost:F2} exceeds threshold ${WeeklyThreshold:F2} (Period: {weekStart} to {today})",
                metadata: new Dictionary<string, object>
(StringComparer.Ordinal)
                {
                    ["start_date"] = weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["end_date"] = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["weekly_cost"] = weeklyCost,
                    ["threshold"] = WeeklyThreshold
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Check monthly cost projection
    /// </summary>
    public async Task CheckMonthlyCostProjectionAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthCost = await _costLogRepository.GetTotalCostAsync(monthStart, today, cancellationToken).ConfigureAwait(false);

        // Project monthly cost based on current daily average
        var daysElapsed = (today.DayNumber - monthStart.DayNumber) + 1;
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        var projectedMonthlyCost = (monthCost / daysElapsed) * daysInMonth;

        if (projectedMonthlyCost > MonthlyThreshold)
        {
            _logger.LogWarning(
                "ALERT: Projected monthly LLM cost ${ProjectedCost:F2} exceeds budget ${Threshold:F2}",
                projectedMonthlyCost, MonthlyThreshold);

            await _alertingService.SendAlertAsync(
                alertType: "LLM_MONTHLY_COST_PROJECTION",
                severity: "warning",
                message: $"Projected monthly LLM cost ${projectedMonthlyCost:F2} exceeds budget ${MonthlyThreshold:F2}. Current: ${monthCost:F2} ({daysElapsed}/{daysInMonth} days)",
                metadata: new Dictionary<string, object>
(StringComparer.Ordinal)
                {
                    ["month"] = today.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                    ["current_cost"] = monthCost,
                    ["projected_cost"] = projectedMonthlyCost,
                    ["threshold"] = MonthlyThreshold,
                    ["days_elapsed"] = daysElapsed,
                    ["days_in_month"] = daysInMonth,
                    ["daily_average"] = monthCost / daysElapsed
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}

