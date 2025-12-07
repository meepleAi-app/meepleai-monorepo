using System;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// ISSUE-1725: Background service for LLM budget monitoring and alerting
/// Checks hourly spend against configured thresholds and triggers alerts
/// </summary>
/// <remarks>
/// Configuration keys (appsettings.json):
/// - LlmBudgetAlerts:DailyBudgetUsd: Daily budget limit (default: 50.00)
/// - LlmBudgetAlerts:MonthlyBudgetUsd: Monthly budget limit (default: 1000.00)
/// - LlmBudgetAlerts:Thresholds:Warning: Warning threshold (default: 0.80)
/// - LlmBudgetAlerts:Thresholds:Critical: Critical threshold (default: 0.95)
/// - LlmBudgetAlerts:CheckIntervalMinutes: Check interval (default: 60)
///
/// Actions:
/// - Warning threshold: Send alert notification
/// - Critical threshold: Send alert + optional model downgrade
/// </remarks>
public class LlmBudgetMonitoringService : BackgroundService
{
    private readonly ILlmCostLogRepository _costLogRepository;
    private readonly IAlertingService _alertingService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LlmBudgetMonitoringService> _logger;

    // Configuration with defaults
    private decimal DailyBudgetUsd => _configuration.GetValue("LlmBudgetAlerts:DailyBudgetUsd", 50.00m);
    private decimal MonthlyBudgetUsd => _configuration.GetValue("LlmBudgetAlerts:MonthlyBudgetUsd", 1000.00m);
    private double WarningThreshold => _configuration.GetValue("LlmBudgetAlerts:Thresholds:Warning", 0.80);
    private double CriticalThreshold => _configuration.GetValue("LlmBudgetAlerts:Thresholds:Critical", 0.95);
    private int CheckIntervalMinutes => _configuration.GetValue("LlmBudgetAlerts:CheckIntervalMinutes", 60);

    public LlmBudgetMonitoringService(
        ILlmCostLogRepository costLogRepository,
        IAlertingService alertingService,
        IConfiguration configuration,
        ILogger<LlmBudgetMonitoringService> logger)
    {
        _costLogRepository = costLogRepository ?? throw new ArgumentNullException(nameof(costLogRepository));
        _alertingService = alertingService ?? throw new ArgumentNullException(nameof(alertingService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogInformation(
            "LlmBudgetMonitoringService initialized: Daily=${DailyBudget}, Monthly=${MonthlyBudget}, Warning={Warning}%, Critical={Critical}%, Interval={Interval}min",
            DailyBudgetUsd, MonthlyBudgetUsd, WarningThreshold * 100, CriticalThreshold * 100, CheckIntervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LLM Budget Monitoring Service starting...");

        // Initial delay to allow app to start
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckBudgetThresholdsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking budget thresholds");
            }

            await Task.Delay(TimeSpan.FromMinutes(CheckIntervalMinutes), stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("LLM Budget Monitoring Service stopping...");
    }

    private async Task CheckBudgetThresholdsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Check daily budget
        var dailySpend = await _costLogRepository.GetTotalCostAsync(
            startDate: DateOnly.FromDateTime(now.Date),
            endDate: DateOnly.FromDateTime(now),
            ct: ct).ConfigureAwait(false);

        var dailyPercentage = (double)(dailySpend / DailyBudgetUsd);
        await ProcessThresholdAsync("Daily", dailySpend, DailyBudgetUsd, dailyPercentage, ct).ConfigureAwait(false);

        // Check monthly budget
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthlySpend = await _costLogRepository.GetTotalCostAsync(
            startDate: DateOnly.FromDateTime(monthStart),
            endDate: DateOnly.FromDateTime(now),
            ct: ct).ConfigureAwait(false);

        var monthlyPercentage = (double)(monthlySpend / MonthlyBudgetUsd);
        await ProcessThresholdAsync("Monthly", monthlySpend, MonthlyBudgetUsd, monthlyPercentage, ct).ConfigureAwait(false);
    }

    private async Task ProcessThresholdAsync(
        string period,
        decimal actualSpend,
        decimal budgetLimit,
        double percentage,
        CancellationToken ct)
    {
        if (percentage >= CriticalThreshold)
        {
            await SendBudgetAlertAsync(
                severity: "Critical",
                period: period,
                actualSpend: actualSpend,
                budgetLimit: budgetLimit,
                percentage: percentage,
                ct: ct).ConfigureAwait(false);
        }
        else if (percentage >= WarningThreshold)
        {
            await SendBudgetAlertAsync(
                severity: "Warning",
                period: period,
                actualSpend: actualSpend,
                budgetLimit: budgetLimit,
                percentage: percentage,
                ct: ct).ConfigureAwait(false);
        }

        _logger.LogDebug(
            "{Period} budget status: ${Spend:F2} / ${Limit:F2} ({Percentage:P0})",
            period, actualSpend, budgetLimit, percentage);
    }

    private async Task SendBudgetAlertAsync(
        string severity,
        string period,
        decimal actualSpend,
        decimal budgetLimit,
        double percentage,
        CancellationToken ct)
    {
        var message = $"""
            🚨 LLM Budget Alert - {severity}

            Period: {period}
            Actual Spend: ${actualSpend:F2}
            Budget Limit: ${budgetLimit:F2}
            Usage: {percentage:P0}

            Threshold: {(severity == "Critical" ? CriticalThreshold : WarningThreshold):P0}
            """;

        _logger.LogWarning(
            "Budget alert triggered: {Severity} - {Period} spend ${Spend:F2} / ${Limit:F2} ({Percentage:P0})",
            severity, period, actualSpend, budgetLimit, percentage);

        await _alertingService.SendAlertAsync(
            alertType: $"LlmBudget{severity}",
            severity: severity == "Critical" ? "Error" : "Warning",
            message: message,
            metadata: new Dictionary<string, object>
            {
                { "period", period },
                { "actualSpend", actualSpend },
                { "budgetLimit", budgetLimit },
                { "percentage", percentage },
                { "source", "LlmBudgetMonitoring" }
            },
            cancellationToken: ct).ConfigureAwait(false);
    }
}
