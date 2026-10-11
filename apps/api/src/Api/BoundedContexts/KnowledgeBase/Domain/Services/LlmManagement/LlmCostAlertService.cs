using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.Services;
using System.Globalization;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for monitoring LLM costs and triggering alerts
/// ISSUE-960: BGAI-018 - Cost monitoring and alerting
/// </summary>
/// <remarks>
/// #4166 (ADR-095 D6): le soglie sono i tetti di <c>LlmSystemConfig</c>, gli stessi che il gateway
/// applica prima di ogni chiamata; la settimanale è sette volte il tetto giornaliero. Prima erano
/// costanti nel codice (100 $ / 500 $ / 3000 $, «ADR-004, 10K MAU»), cento volte il budget reale.
/// </remarks>
internal class LlmCostAlertService
{
    private readonly ILlmCostLogRepository _costLogRepository;
    private readonly IAlertingService _alertingService;
    private readonly ILogger<LlmCostAlertService> _logger;
    private readonly ILlmSystemConfigProvider _configProvider;

    /// <summary>Giorni di una settimana: la soglia settimanale è questo multiplo del tetto giornaliero.</summary>
    internal const int DaysPerWeek = 7;

    public LlmCostAlertService(
        ILlmCostLogRepository costLogRepository,
        IAlertingService alertingService,
        ILogger<LlmCostAlertService> logger,
        ILlmSystemConfigProvider configProvider)
    {
        _costLogRepository = costLogRepository ?? throw new ArgumentNullException(nameof(costLogRepository));
        _alertingService = alertingService ?? throw new ArgumentNullException(nameof(alertingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
    }

    /// <summary>
    /// Check daily cost and send alert if threshold exceeded
    /// </summary>
    public async Task CheckDailyCostThresholdAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dailyThreshold = (await _configProvider.GetBudgetPolicyAsync(cancellationToken).ConfigureAwait(false)).DailyCapUsd;
        var dailyCost = await _costLogRepository.GetDailyCostAsync(today, cancellationToken).ConfigureAwait(false);

        if (dailyCost > dailyThreshold)
        {
            _logger.LogWarning(
                "ALERT: Daily LLM cost ${DailyCost:F2} exceeds threshold ${Threshold:F2}",
                dailyCost, dailyThreshold);

            var costsByProvider = await _costLogRepository.GetCostsByProviderAsync(today, today, cancellationToken).ConfigureAwait(false);
            var providerBreakdown = string.Join(", ",
                costsByProvider.Select(kv => $"{kv.Key}: ${kv.Value:F2}"));

            await _alertingService.SendAlertAsync(
                alertType: "LLM_COST_THRESHOLD",
                severity: "warning",
                message: $"Daily LLM cost ${dailyCost:F2} exceeds threshold ${dailyThreshold:F2}. Breakdown: {providerBreakdown}",
                metadata: new Dictionary<string, object>
(StringComparer.Ordinal)
                {
                    ["date"] = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["daily_cost"] = dailyCost,
                    ["threshold"] = dailyThreshold,
                    ["exceeded_by"] = dailyCost - dailyThreshold,
                    ["provider_breakdown"] = providerBreakdown
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogDebug("Daily LLM cost ${DailyCost:F2} within threshold ${Threshold:F2}",
                dailyCost, dailyThreshold);
        }
    }

    /// <summary>
    /// Check weekly cost threshold
    /// </summary>
    public async Task CheckWeeklyCostThresholdAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekStart = today.AddDays(-7);
        var weeklyThreshold = (await _configProvider.GetBudgetPolicyAsync(cancellationToken).ConfigureAwait(false)).DailyCapUsd * DaysPerWeek;
        var weeklyCost = await _costLogRepository.GetTotalCostAsync(weekStart, today, cancellationToken).ConfigureAwait(false);

        if (weeklyCost > weeklyThreshold)
        {
            _logger.LogWarning(
                "ALERT: Weekly LLM cost ${WeeklyCost:F2} exceeds threshold ${Threshold:F2}",
                weeklyCost, weeklyThreshold);

            await _alertingService.SendAlertAsync(
                alertType: "LLM_WEEKLY_COST_THRESHOLD",
                severity: "warning",
                message: $"Weekly LLM cost ${weeklyCost:F2} exceeds threshold ${weeklyThreshold:F2} (Period: {weekStart} to {today})",
                metadata: new Dictionary<string, object>
(StringComparer.Ordinal)
                {
                    ["start_date"] = weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["end_date"] = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["weekly_cost"] = weeklyCost,
                    ["threshold"] = weeklyThreshold
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
        var monthlyThreshold = (await _configProvider.GetBudgetPolicyAsync(cancellationToken).ConfigureAwait(false)).MonthlyCapUsd;
        var monthCost = await _costLogRepository.GetTotalCostAsync(monthStart, today, cancellationToken).ConfigureAwait(false);

        // Project monthly cost based on current daily average
        var daysElapsed = (today.DayNumber - monthStart.DayNumber) + 1;
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        var projectedMonthlyCost = (monthCost / daysElapsed) * daysInMonth;

        if (projectedMonthlyCost > monthlyThreshold)
        {
            _logger.LogWarning(
                "ALERT: Projected monthly LLM cost ${ProjectedCost:F2} exceeds budget ${Threshold:F2}",
                projectedMonthlyCost, monthlyThreshold);

            await _alertingService.SendAlertAsync(
                alertType: "LLM_MONTHLY_COST_PROJECTION",
                severity: "warning",
                message: $"Projected monthly LLM cost ${projectedMonthlyCost:F2} exceeds budget ${monthlyThreshold:F2}. Current: ${monthCost:F2} ({daysElapsed}/{daysInMonth} days)",
                metadata: new Dictionary<string, object>
(StringComparer.Ordinal)
                {
                    ["month"] = today.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                    ["current_cost"] = monthCost,
                    ["projected_cost"] = projectedMonthlyCost,
                    ["threshold"] = monthlyThreshold,
                    ["days_elapsed"] = daysElapsed,
                    ["days_in_month"] = daysInMonth,
                    ["daily_average"] = monthCost / daysElapsed
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}

