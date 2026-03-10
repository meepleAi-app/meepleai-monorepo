using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job that cleans up old read notifications and processed emails.
/// Only deletes READ notifications older than retention period.
/// Unread notifications are NEVER deleted.
/// Issue #41: Notification cleanup with configurable TTL.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class NotificationCleanupJob : IJob
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationCleanupJob> _logger;

    private const int DefaultRetentionDays = 90;
    private const int BatchSize = 1000;

    public NotificationCleanupJob(
        MeepleAiDbContext dbContext,
        IConfiguration configuration,
        ILogger<NotificationCleanupJob> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting notification cleanup job");

        var retentionDays = _configuration.GetValue("Notifications:RetentionDays", DefaultRetentionDays);
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        try
        {
            // 1. Clean up read notifications older than retention period (batch delete)
            var totalNotificationsDeleted = 0;
            int batchDeleted;

            do
            {
                batchDeleted = await _dbContext.Set<NotificationEntity>()
                    .Where(n => n.IsRead && n.CreatedAt < cutoffDate)
                    .OrderBy(n => n.CreatedAt)
                    .Take(BatchSize)
                    .ExecuteDeleteAsync(context.CancellationToken)
                    .ConfigureAwait(false);

                totalNotificationsDeleted += batchDeleted;

                if (batchDeleted > 0)
                {
                    _logger.LogDebug("Deleted {Count} read notifications in batch", batchDeleted);
                }
            }
            while (batchDeleted == BatchSize && !context.CancellationToken.IsCancellationRequested);

            // 2. Clean up sent/dead_letter email queue items older than retention period
            var totalEmailsDeleted = 0;

            do
            {
                batchDeleted = await _dbContext.Set<EmailQueueEntity>()
                    .Where(e => (e.Status == "sent" || e.Status == "dead_letter") && e.CreatedAt < cutoffDate)
                    .OrderBy(e => e.CreatedAt)
                    .Take(BatchSize)
                    .ExecuteDeleteAsync(context.CancellationToken)
                    .ConfigureAwait(false);

                totalEmailsDeleted += batchDeleted;

                if (batchDeleted > 0)
                {
                    _logger.LogDebug("Deleted {Count} old email queue items in batch", batchDeleted);
                }
            }
            while (batchDeleted == BatchSize && !context.CancellationToken.IsCancellationRequested);

            _logger.LogInformation(
                "Notification cleanup completed: {NotificationsDeleted} read notifications, {EmailsDeleted} email queue items deleted (retention: {Days} days)",
                totalNotificationsDeleted, totalEmailsDeleted, retentionDays);

            context.Result = new
            {
                Success = true,
                NotificationsDeleted = totalNotificationsDeleted,
                EmailsDeleted = totalEmailsDeleted,
                RetentionDays = retentionDays
            };
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Notification cleanup job failed");
            context.Result = new { Success = false, Error = ex.Message };
        }
    }
}
