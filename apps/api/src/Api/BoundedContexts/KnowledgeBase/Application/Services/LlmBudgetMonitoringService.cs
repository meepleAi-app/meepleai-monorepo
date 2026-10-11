using System;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// ISSUE-1725: Background service for LLM budget monitoring and alerting.
/// </summary>
/// <remarks>
/// <para>
/// #4166 (ADR-095 D6): tetti e soglia vengono da <c>LlmSystemConfig</c>, gli stessi che il gateway
/// applica prima di ogni chiamata. Prima venivano da <c>LlmBudgetAlerts</c> in appsettings
/// (50 $ / 1000 $), e alla soglia critica questo servizio passava tutte le richieste a modelli più
/// economici: un budget mode orario, che il degrado del gateway ha sostituito.
/// </para>
/// <para>
/// Qui restano gli allarmi: un warning quando il consumo raggiunge la soglia di degrado, un errore
/// quando raggiunge il tetto e il gateway rifiuta le chiamate. Un tetto a zero (AI a pagamento
/// spenta) non genera allarmi.
/// </para>
/// <para>Configurazione: <c>LlmBudgetAlerts:CheckIntervalMinutes</c> (default 60).</para>
/// </remarks>
internal class LlmBudgetMonitoringService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILlmSystemConfigProvider _configProvider;
    private readonly ILogger<LlmBudgetMonitoringService> _logger;

    private int CheckIntervalMinutes => _configuration.GetValue("LlmBudgetAlerts:CheckIntervalMinutes", 60);

    public LlmBudgetMonitoringService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILlmSystemConfigProvider configProvider,
        ILogger<LlmBudgetMonitoringService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LLM Budget Monitoring Service starting (interval {Interval} min)", CheckIntervalMinutes);

        // Initial delay to allow app to start
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckBudgetThresholdsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error checking budget thresholds");
            }

            await Task.Delay(TimeSpan.FromMinutes(CheckIntervalMinutes), stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("LLM Budget Monitoring Service stopping...");
    }

    internal async Task CheckBudgetThresholdsAsync(CancellationToken cancellationToken)
    {
        var policy = await _configProvider.GetBudgetPolicyAsync(cancellationToken).ConfigureAwait(false);
        var warningRatio = policy.DegradeThresholdPercent / 100.0;

        using var scope = _scopeFactory.CreateScope();
        var costLogRepository = scope.ServiceProvider.GetRequiredService<ILlmCostLogRepository>();
        var alertingService = scope.ServiceProvider.GetRequiredService<IAlertingService>();

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        var dailySpend = await costLogRepository
            .GetTotalCostAsync(today, today, cancellationToken).ConfigureAwait(false);
        await ProcessThresholdAsync(alertingService, "Daily", dailySpend, policy.DailyCapUsd, warningRatio, cancellationToken)
            .ConfigureAwait(false);

        var monthlySpend = await costLogRepository
            .GetTotalCostAsync(new DateOnly(today.Year, today.Month, 1), today, cancellationToken).ConfigureAwait(false);
        await ProcessThresholdAsync(alertingService, "Monthly", monthlySpend, policy.MonthlyCapUsd, warningRatio, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ProcessThresholdAsync(
        IAlertingService alertingService,
        string period,
        decimal actualSpend,
        decimal budgetLimit,
        double warningRatio,
        CancellationToken cancellationToken)
    {
        if (budgetLimit <= 0m)
        {
            _logger.LogDebug("{Period} LLM budget is zero (paid AI off): no alert", period);
            return;
        }

        var percentage = (double)(actualSpend / budgetLimit);
        if (percentage >= 1.0)
        {
            await SendBudgetAlertAsync(alertingService, "Critical", period, actualSpend, budgetLimit, percentage,
                "The LLM gateway is refusing paid calls until the budget resets.", cancellationToken).ConfigureAwait(false);
        }
        else if (percentage >= warningRatio)
        {
            await SendBudgetAlertAsync(alertingService, "Warning", period, actualSpend, budgetLimit, percentage,
                "Degrade threshold reached: answers use the reserve model of the agent profile, background work is paused.",
                cancellationToken).ConfigureAwait(false);
        }

        _logger.LogDebug(
            "{Period} budget status: ${Spend:F4} / ${Limit:F2} ({Percentage:P0})",
            period, actualSpend, budgetLimit, percentage);
    }

    private async Task SendBudgetAlertAsync(
        IAlertingService alertingService,
        string severity,
        string period,
        decimal actualSpend,
        decimal budgetLimit,
        double percentage,
        string effect,
        CancellationToken cancellationToken)
    {
        var message = $"""
            🚨 LLM Budget Alert - {severity}

            Period: {period}
            Actual Spend: ${actualSpend:F2}
            Budget Limit: ${budgetLimit:F2}
            Usage: {percentage:P0}

            {effect}
            """;

        _logger.LogWarning(
            "Budget alert triggered: {Severity} - {Period} spend ${Spend:F2} / ${Limit:F2} ({Percentage:P0})",
            severity, period, actualSpend, budgetLimit, percentage);

        await alertingService.SendAlertAsync(
            alertType: $"LlmBudget{severity}",
            severity: string.Equals(severity, "Critical", StringComparison.Ordinal) ? "Error" : "Warning",
            message: message,
            metadata: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "period", period },
                { "actualSpend", actualSpend },
                { "budgetLimit", budgetLimit },
                { "percentage", percentage },
                { "source", "LlmBudgetMonitoring" },
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
