using System.Globalization;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

/// <summary>
/// ISSUE-5085: Quartz.NET job that sends a daily OpenRouter usage digest to all admins.
/// Runs every day at 08:00 UTC — summarises yesterday's spend, request count, and
/// whether the daily budget was exceeded.
///
/// Configuration (appsettings / env):
///   OpenRouterAlerts:DailyBudgetUsd — daily budget limit in USD (default: 10.00)
/// </summary>
[DisallowConcurrentExecution]
internal sealed class OpenRouterDailySummaryJob : IJob
{
    private readonly ILlmRequestLogRepository _logRepo;
    private readonly INotificationRepository _notificationRepo;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenRouterDailySummaryJob> _logger;

    private decimal DailyBudgetUsd =>
        _configuration.GetValue("OpenRouterAlerts:DailyBudgetUsd", 10.00m);

    public OpenRouterDailySummaryJob(
        ILlmRequestLogRepository logRepo,
        INotificationRepository notificationRepo,
        MeepleAiDbContext dbContext,
        IConfiguration configuration,
        ILogger<OpenRouterDailySummaryJob> logger)
    {
        _logRepo = logRepo ?? throw new ArgumentNullException(nameof(logRepo));
        _notificationRepo = notificationRepo ?? throw new ArgumentNullException(nameof(notificationRepo));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        // Yesterday's full UTC day
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var from = yesterday.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var until = yesterday.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        _logger.LogInformation(
            "OpenRouterDailySummaryJob: generating digest for {Date}",
            yesterday);

        try
        {
            var (byModel, _, _, totalCostUsd, totalRequests) = await _logRepo
                .GetCostBreakdownAsync(from, until, ct)
                .ConfigureAwait(false);

            var adminIds = await _dbContext.Set<UserEntity>()
                .AsNoTracking()
                .Where(u => u.Role == "admin")
                .Select(u => u.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (adminIds.Count == 0)
            {
                _logger.LogDebug("OpenRouterDailySummaryJob: no admin users to notify");
                return;
            }

            var dailyBudget = DailyBudgetUsd;
            var budgetExceeded = dailyBudget > 0 && totalCostUsd > dailyBudget;
            var usedPercent = dailyBudget > 0
                ? (double)(totalCostUsd / dailyBudget) * 100.0
                : 0.0;

            var severity = budgetExceeded
                ? NotificationSeverity.Warning
                : NotificationSeverity.Info;

            var title = budgetExceeded
                ? $"OpenRouter daily summary ({yesterday:yyyy-MM-dd}) — budget EXCEEDED: ${totalCostUsd:F2}"
                : $"OpenRouter daily summary ({yesterday:yyyy-MM-dd}) — ${totalCostUsd:F4} spent";

            // Build top-model breakdown (up to 3)
            var topModels = byModel
                .OrderByDescending(m => m.CostUsd)
                .Take(3)
                .Select(m => $"{m.ModelId}: ${m.CostUsd:F4} ({m.Requests} reqs)")
                .ToList();

            var modelBreakdown = topModels.Count > 0
                ? string.Join(", ", topModels)
                : "no requests";

            var message = dailyBudget > 0
                ? $"OpenRouter usage for {yesterday:yyyy-MM-dd}: ${totalCostUsd:F4} spent " +
                  $"({usedPercent:F0}% of ${dailyBudget:F2} daily budget), {totalRequests} requests. " +
                  $"Top models: {modelBreakdown}."
                : $"OpenRouter usage for {yesterday:yyyy-MM-dd}: ${totalCostUsd:F4} spent, " +
                  $"{totalRequests} requests. Top models: {modelBreakdown}.";

            var metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                date = yesterday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                totalCostUsd,
                totalRequests,
                dailyBudgetUsd = dailyBudget,
                usedPercent = Math.Round(usedPercent, 1),
                budgetExceeded,
                topModels
            });

            foreach (var adminId in adminIds)
            {
                var n = new Notification(
                    id: Guid.NewGuid(),
                    userId: adminId,
                    type: NotificationType.AdminOpenRouterDailySummary,
                    severity: severity,
                    title: title,
                    message: message,
                    link: "/admin/agents/usage",
                    metadata: metadata);

                await _notificationRepo.AddAsync(n, ct).ConfigureAwait(false);
            }

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation(
                "OpenRouterDailySummaryJob: sent digest to {Count} admins for {Date} — ${Spend:F4}, {Requests} requests",
                adminIds.Count, yesterday, totalCostUsd, totalRequests);

            context.Result = new
            {
                Success = true,
                Date = yesterday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                TotalCostUsd = totalCostUsd,
                TotalRequests = totalRequests,
                AdminsNotified = adminIds.Count
            };
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenRouterDailySummaryJob: failed to generate digest");
            context.Result = new { Success = false, Error = ex.Message };
        }
#pragma warning restore CA1031
    }
}
