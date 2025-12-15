using System;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
/// - Critical threshold: Send alert + enable budget mode (auto-downgrade to cheaper models)
/// - Below warning: Disable budget mode (restore normal models)
///
/// DI Pattern: Uses IServiceScopeFactory to resolve scoped services (ILlmCostLogRepository)
/// </remarks>
internal class LlmBudgetMonitoringService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILlmModelOverrideService _modelOverrideService;
    private readonly ILogger<LlmBudgetMonitoringService> _logger;

    // Configuration with defaults
    private decimal DailyBudgetUsd => _configuration.GetValue("LlmBudgetAlerts:DailyBudgetUsd", 50.00m);
    private decimal MonthlyBudgetUsd => _configuration.GetValue("LlmBudgetAlerts:MonthlyBudgetUsd", 1000.00m);
    private double WarningThreshold => _configuration.GetValue("LlmBudgetAlerts:Thresholds:Warning", 0.80);
    private double CriticalThreshold => _configuration.GetValue("LlmBudgetAlerts:Thresholds:Critical", 0.95);
    private int CheckIntervalMinutes => _configuration.GetValue("LlmBudgetAlerts:CheckIntervalMinutes", 60);

    public LlmBudgetMonitoringService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILlmModelOverrideService modelOverrideService,
        ILogger<LlmBudgetMonitoringService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _modelOverrideService = modelOverrideService ?? throw new ArgumentNullException(nameof(modelOverrideService));
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
        // Create scope to resolve scoped services
        using var scope = _scopeFactory.CreateScope();
        var costLogRepository = scope.ServiceProvider.GetRequiredService<ILlmCostLogRepository>();
        var alertingService = scope.ServiceProvider.GetRequiredService<IAlertingService>();

        var now = DateTime.UtcNow;

        // Check daily budget
        var dailySpend = await costLogRepository.GetTotalCostAsync(
            startDate: DateOnly.FromDateTime(now.Date),
            endDate: DateOnly.FromDateTime(now),
            ct: ct).ConfigureAwait(false);

        var dailyPercentage = (double)(dailySpend / DailyBudgetUsd);
        await ProcessThresholdAsync(alertingService, "Daily", dailySpend, DailyBudgetUsd, dailyPercentage, ct).ConfigureAwait(false);

        // Check monthly budget
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthlySpend = await costLogRepository.GetTotalCostAsync(
            startDate: DateOnly.FromDateTime(monthStart),
            endDate: DateOnly.FromDateTime(now),
            ct: ct).ConfigureAwait(false);

        var monthlyPercentage = (double)(monthlySpend / MonthlyBudgetUsd);
        await ProcessThresholdAsync(alertingService, "Monthly", monthlySpend, MonthlyBudgetUsd, monthlyPercentage, ct).ConfigureAwait(false);
    }

    private async Task ProcessThresholdAsync(
        IAlertingService alertingService,
        string period,
        decimal actualSpend,
        decimal budgetLimit,
        double percentage,
        CancellationToken ct)
    {
        if (percentage >= CriticalThreshold)
        {
            // ISSUE-1725: Enable budget mode (auto-downgrade to cheaper models)
            var reason = $"{period} budget {percentage:P0} exceeded (${actualSpend:F2} / ${budgetLimit:F2})";
            _modelOverrideService.EnableBudgetMode(reason);

            await SendBudgetAlertAsync(
                alertingService,
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
                alertingService,
                severity: "Warning",
                period: period,
                actualSpend: actualSpend,
                budgetLimit: budgetLimit,
                percentage: percentage,
                ct: ct).ConfigureAwait(false);
        }
        else if (_modelOverrideService.IsInBudgetMode())
        {
            // ISSUE-1725: Budget dropped below warning → disable budget mode
            _modelOverrideService.DisableBudgetMode();
            _logger.LogInformation(
                "{Period} budget recovered to {Percentage:P0} - budget mode disabled",
                period, percentage);
        }

        _logger.LogDebug(
            "{Period} budget status: ${Spend:F2} / ${Limit:F2} ({Percentage:P0})",
            period, actualSpend, budgetLimit, percentage);
    }

    private async Task SendBudgetAlertAsync(
        IAlertingService alertingService,
        string severity,
        string period,
        decimal actualSpend,
        decimal budgetLimit,
        double percentage,
        CancellationToken ct)
    {
        var budgetModeStatus = _modelOverrideService.GetBudgetModeStatus();
        var message = $"""
            🚨 LLM Budget Alert - {severity}

            Period: {period}
            Actual Spend: ${actualSpend:F2}
            Budget Limit: ${budgetLimit:F2}
            Usage: {percentage:P0}

            Threshold: {(string.Equals(severity, "Critical", StringComparison.Ordinal) ? CriticalThreshold : WarningThreshold):P0}
            
            {budgetModeStatus}
            """;

        _logger.LogWarning(
            "Budget alert triggered: {Severity} - {Period} spend ${Spend:F2} / ${Limit:F2} ({Percentage:P0})",
            severity, period, actualSpend, budgetLimit, percentage);

        await alertingService.SendAlertAsync(
            alertType: $"LlmBudget{severity}",
            severity: string.Equals(severity, "Critical", StringComparison.Ordinal) ? "Error" : "Warning",
            message: message,
            metadata: new Dictionary<string, object>
(StringComparer.Ordinal)
            {
                { "period", period },
                { "actualSpend", actualSpend },
                { "budgetLimit", budgetLimit },
                { "percentage", percentage },
                { "source", "LlmBudgetMonitoring" },
                { "budgetModeActive", _modelOverrideService.IsInBudgetMode() }
            },
            cancellationToken: ct).ConfigureAwait(false);
    }
}
