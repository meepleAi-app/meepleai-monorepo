using System.Globalization;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// ISSUE-5085: Background service that monitors daily OpenRouter spend
/// and sends in-app admin notifications at configurable budget thresholds.
///
/// Configuration (appsettings / env):
///   OpenRouterAlerts:DailyBudgetUsd          — daily budget limit in USD (default: 10.00)
///   OpenRouterAlerts:BudgetAlertThresholds   — comma-separated threshold % list (default: "50,80,100")
///   OpenRouterAlerts:BudgetCheckIntervalMin  — polling interval in minutes (default: 10)
/// </summary>
internal sealed class OpenRouterBudgetAlertBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenRouterBudgetAlertBackgroundService> _logger;

    // Track which thresholds have been alerted today to avoid repeated notifications
    private readonly HashSet<int> _alertedThresholds = [];
    private DateOnly _alertedDate = DateOnly.MinValue;

    private decimal DailyBudgetUsd =>
        _configuration.GetValue("OpenRouterAlerts:DailyBudgetUsd", 10.00m);

    // S2365: Method instead of property — properties should not copy collections
    private int[] GetBudgetAlertThresholds()
    {
        var raw = _configuration.GetValue("OpenRouterAlerts:BudgetAlertThresholds", "50,80,100");
        return raw!
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : -1)
            .Where(v => v > 0)
            .Order()
            .ToArray();
    }

    private TimeSpan CheckInterval =>
        TimeSpan.FromMinutes(_configuration.GetValue("OpenRouterAlerts:BudgetCheckIntervalMin", 10));

    public OpenRouterBudgetAlertBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<OpenRouterBudgetAlertBackgroundService> logger)
    {
        _scopeFactory = scopeFactory
            ?? throw new ArgumentNullException(nameof(scopeFactory));
        _configuration = configuration
            ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OpenRouterBudgetAlertBackgroundService starting — daily budget=${Budget}, thresholds=[{Thresholds}]",
            DailyBudgetUsd, string.Join(",", GetBudgetAlertThresholds()));

        // Initial delay to let the application fully start
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckBudgetAsync(stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "OpenRouterBudgetAlertBackgroundService: error during budget check");
            }
#pragma warning restore CA1031

            await Task.Delay(CheckInterval, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("OpenRouterBudgetAlertBackgroundService stopped.");
    }

    private async Task CheckBudgetAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Reset alerted thresholds at the start of a new UTC day
        if (today != _alertedDate)
        {
            _alertedThresholds.Clear();
            _alertedDate = today;
        }

        using var scope = _scopeFactory.CreateScope();
        var logRepo = scope.ServiceProvider.GetRequiredService<ILlmRequestLogRepository>();

        var from = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var until = DateTime.UtcNow;

        var (_, _, _, totalCostUsd, _) = await logRepo
            .GetCostBreakdownAsync(from, until, cancellationToken)
            .ConfigureAwait(false);

        var dailyBudget = DailyBudgetUsd;
        if (dailyBudget <= 0)
            return;

        var usedPercent = (double)(totalCostUsd / dailyBudget) * 100.0;

        _logger.LogDebug(
            "Budget check: ${Spend:F4} / ${Budget:F2} ({Used:F0}%)",
            totalCostUsd, dailyBudget, usedPercent);

        foreach (var threshold in GetBudgetAlertThresholds())
        {
            if (usedPercent >= threshold && !_alertedThresholds.Contains(threshold))
            {
                await SendBudgetAlertAsync(
                    threshold, totalCostUsd, dailyBudget, usedPercent, scope, cancellationToken)
                    .ConfigureAwait(false);

                _alertedThresholds.Add(threshold);
            }
        }
    }

    private async Task SendBudgetAlertAsync(
        int threshold,
        decimal actualSpend,
        decimal budget,
        double usedPercent,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var notificationRepo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

        var adminIds = await dbContext.Set<UserEntity>()
            .AsNoTracking()
            .Where(u => u.Role == "admin")
            .Select(u => u.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (adminIds.Count == 0)
        {
            _logger.LogDebug("OpenRouterBudgetAlert: no admin users to notify");
            return;
        }

        var severity = threshold >= 100
            ? NotificationSeverity.Error
            : threshold >= 80
                ? NotificationSeverity.Warning
                : NotificationSeverity.Info;

        var title = threshold >= 100
            ? $"OpenRouter daily budget EXHAUSTED — ${actualSpend:F2} spent"
            : $"OpenRouter daily budget at {threshold}% — ${actualSpend:F2} / ${budget:F2}";

        var message = $"OpenRouter daily budget alert: ${actualSpend:F4} spent out of ${budget:F2} " +
                      $"({usedPercent:F0}% — {threshold}% threshold reached).";

        var metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            threshold,
            actualSpendUsd = actualSpend,
            budgetUsd = budget,
            usedPercent = Math.Round(usedPercent, 1),
            alertedAt = DateTime.UtcNow
        });

        foreach (var adminId in adminIds)
        {
            var n = new Notification(
                id: Guid.NewGuid(),
                userId: adminId,
                type: NotificationType.AdminOpenRouterBudgetAlert,
                severity: severity,
                title: title,
                message: message,
                link: "/admin/agents/usage",
                metadata: metadata);

            await notificationRepo.AddAsync(n, cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogWarning(
            "OpenRouter budget alert ({Threshold}%) sent to {Count} admins: ${Spend:F4} / ${Budget:F2}",
            threshold, adminIds.Count, actualSpend, budget);
    }
}
